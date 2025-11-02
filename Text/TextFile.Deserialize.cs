using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PKMTextTranslator.Text;

public partial class TextFile
{
    public static TextFile Deserialize(ReadOnlySpan<byte> data, TextConfig? config = null, bool remapChars = false)
    {
        config ??= TextConfig.Default;
        
        var header = MemoryMarshal.Read<TextHeader>(data);
        header.Validate(data.Length);

        data = data[(int)header.SectionDataOffset..];

        var lineInfos = MemoryMarshal.Cast<byte, TextLineInfo>(data.Slice(sizeof(uint), header.LineCount * Unsafe.SizeOf<TextLineInfo>())).ToArray();
        
        string[] lines = DecryptLines(data, lineInfos, config, remapChars);
        ushort[] flags = lineInfos.Select(z => z.Flags).ToArray();
        
        Debug.Assert(lines.Length == flags.Length);
        var f = new TextFile(config);
        f.AddRange(lines.Zip(flags, (line, flag) => new TextLine(line, flag)));
        
        return f;
    }
    
    private static string[] DecryptLines(ReadOnlySpan<byte> data, TextLineInfo[] lineInfos, TextConfig config, bool remapChars)
    {
        var sb = new StringBuilder();
        string[] result = new string[lineInfos.Length];
        for (ushort i = 0; i < result.Length; i++)
        {
            ushort key = GetLineKey(i);
            (int offset, ushort length, _) = lineInfos[i];
            byte[] encryptedLineData = data.Slice(offset, length * 2).ToArray();

            byte[] decryptedLineData = CryptLineData(encryptedLineData, key);
            
            GetLineString(config, MemoryMarshal.Cast<byte, ushort>(decryptedLineData), remapChars, sb);
            
            result[i] = sb.ToString();
            sb.Clear();
        }
        return result;
    }

    private static ushort TryUnmapChar(ushort val, bool remapChars)
    {
        if (!remapChars)
            return val;
        return val switch
        {
            0xE07F => 0x202F, // nbsp
            0xE08D => 0x2026, // …
            0xE08E => 0x2642, // ♂
            0xE08F => 0x2640, // ♀
            _ => val,
        };
    }

    private static void GetLineString(TextConfig config, ReadOnlySpan<ushort> data, bool remapChars, StringBuilder s)
    {
        while (!data.IsEmpty)
        {
            ushort val = SplitAndAdvance(ref data);

            switch (val)
            {
                case KEY_VARIABLE: AppendVariableString(config, ref data, remapChars, s); break;
                case KEY_TERMINATOR: return;
                case '\n': s.Append(@"\n"); break;
                case '\\': s.Append(@"\\"); break;
                case '[': s.Append(@"\["); break;
                case '{': s.Append(@"\{"); break;
                default: s.Append((char)TryUnmapChar(val, remapChars)); break;
            }
        }
    }

    private static void AppendVariableString(TextConfig config, ref ReadOnlySpan<ushort> data, bool remapChars, StringBuilder s)
    {
        ushort count = SplitAndAdvance(ref data);
        ushort variable = SplitAndAdvance(ref data);

        switch (variable)
        {
            case KEY_TEXTRETURN: // "Wait button then scroll text \r"
                s.Append("\\r");
                return;
            case KEY_TEXTCLEAR: // "Wait button then clear text \c"
                s.Append("\\c");
                return;
            case KEY_TEXTWAIT: // Dramatic pause for a text line. New!
                ushort time = SplitAndAdvance(ref data);
                s.Append($"[WAIT {time}]");
                return;
            case KEY_TEXTNULL: // nullptr text, Includes linenum
                ushort line = SplitAndAdvance(ref data);
                s.Append($"[~ {line}]");
                return;
            case KEY_TEXTRUBY: // Ruby text/furigana for Japanese
                ushort baseLength = SplitAndAdvance(ref data);
                ushort rubyLength = SplitAndAdvance(ref data);

                var baseSpan1 = SplitAndAdvance(ref data, baseLength);
                var rubySpan = SplitAndAdvance(ref data, rubyLength);
                var baseSpan2 = SplitAndAdvance(ref data, baseLength);

                s.Append('{');
                GetLineString(config, baseSpan1, remapChars, s);
                s.Append('|');
                GetLineString(config, rubySpan, remapChars, s);
                if (!baseSpan1.SequenceEqual(baseSpan2))
                {
                    // basetext1 should duplicate basetext2, so this shouldn't occur
                    s.Append('|');
                    GetLineString(config, baseSpan2, remapChars, s);
                }
                s.Append('}');
                return;
        }

        string varName = config.GetVariableString(variable);
        s.Append("[VAR").Append(' ').Append(varName);
        if (count > 1)
        {
            s.Append('(');
            while (count > 1)
            {
                ushort arg = SplitAndAdvance(ref data);
                s.Append(arg.ToString("X4"));
                if (--count == 1)
                    break;
                s.Append(',');
            }
            s.Append(')');
        }
        s.Append(']');
    }
}

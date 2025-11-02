using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PKMTextTranslator.Text;

public partial class TextFile
{
    public void Serialize(BinaryWriter writer)
    {
        int metaStart = Unsafe.SizeOf<TextHeader>();
        int dataStart = metaStart + Count * Unsafe.SizeOf<TextLineInfo>();

        writer.Seek(dataStart, SeekOrigin.Begin);
    
        var lineInfos = new TextLineInfo[Count];
        for (ushort i = 0; i < Count; i++)
        {
            ushort key = GetLineKey(i);
            (string text, ushort flags) = this[i];
            
            text = text.Trim();
            if (text.Length == 0 && SetEmptyText) text = $"[~ {i}]";

            byte[] lineData = CryptLineData(GetLineData(text), key);
        
            long lineOffset = writer.BaseStream.Position - 0x10;
            
            writer.Write(lineData);
            if (lineData.Length % 4 == 2)
                writer.Write((ushort)0); // padding
        
            lineInfos[i] = new TextLineInfo
            {
                Offset = (int)lineOffset,
                Length = (ushort)(lineData.Length / 2),
                Flags = flags
            };
        }
        
        writer.Seek(metaStart, SeekOrigin.Begin);
        writer.Write(MemoryMarshal.Cast<TextLineInfo, byte>(lineInfos));
        Debug.Assert(writer.BaseStream.Position == dataStart);

        var header = new TextHeader((ushort)Count, (uint)(writer.BaseStream.Length - 0x10));
        header.Validate(writer.BaseStream.Length);
        
        writer.Seek(0, SeekOrigin.Begin);
        writer.Write(MemoryMarshal.Cast<TextHeader, byte>(MemoryMarshal.CreateReadOnlySpan(ref header, 1)));
    }

    private static ushort TryRemapChar(ushort val, bool remapChars)
    {
        if (!remapChars)
            return val;
        return val switch
        {
            0x202F => 0xE07F, // nbsp
            0x2026 => 0xE08D, // …
            0x2642 => 0xE08E, // ♂
            0x2640 => 0xE08F, // ♀
            _ => val,
        };
    }
    
    private byte[] GetLineData(ReadOnlySpan<char> line)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int i = 0;
        while (i < line.Length)
        {
            ushort val = TryRemapChar(line[i++], remapChars);
    
            switch (val)
            {
                case '[':
                    // grab the string
                    int bracket = line[i..].IndexOf(']');
                    if (bracket < 0)
                        throw new ArgumentException("Variable text is not capped properly: " + line.ToString());
                    var varText = line.Slice(i, bracket);
                    var varValues = GetVariableValues([], varText);
                    foreach (ushort v in varValues)
                        bw.Write(v);
                    i += 1 + varText.Length;
                    break;
                case '{':
                    int brace = line[i..].IndexOf('}');
                    if (brace < 0)
                        throw new ArgumentException("Ruby text is not capped properly: " + line.ToString());
                    var rubyText = line.Slice(i, brace);
                    List<ushort> rubyValues = [];
                    GetRubyValues(rubyText.ToString(), rubyValues);
                    foreach (ushort v in rubyValues)
                        bw.Write(v);
                    i += 1 + rubyText.Length;
                    break;
                case '\\':
                    var escapeValues = GetEscapeValues(line[i++]);
                    foreach (ushort v in escapeValues)
                        bw.Write(v);
                    break;
                default:
                    bw.Write(val);
                    break;
            }
        }
        bw.Write(KEY_TERMINATOR); // cap the line off
        return ms.ToArray();
    }
    
    private static IEnumerable<ushort> GetEscapeValues(char esc)
    {
        var vals = new List<ushort>();
        switch (esc)
        {
            case 'n': vals.Add('\n'); return vals;
            case '\\': vals.Add('\\'); return vals;
            case '[': vals.Add('['); return vals;
            case '{': vals.Add('{'); return vals;
            case 'r': vals.AddRange([KEY_VARIABLE, 1, KEY_TEXTRETURN]); return vals;
            case 'c': vals.AddRange([KEY_VARIABLE, 1, KEY_TEXTCLEAR]); return vals;
            default: throw new Exception($"Invalid terminated line: \\{esc}");
        }
    }
    
    private IEnumerable<ushort> GetVariableValues(List<ushort> vals, ReadOnlySpan<char> variable)
    {
        int spaceIndex = variable.IndexOf(' ');
        if (spaceIndex == -1)
            throw new ArgumentException($"Incorrectly formatted variable text: {variable}");
    
        var cmd = variable[..spaceIndex];
        var args = variable[(spaceIndex + 1)..];
    
        vals.Add(KEY_VARIABLE);
        switch (cmd)
        {
            case "~": // Blank Text Line Variable (nullptr text)
                vals.Add(1);
                vals.Add(KEY_TEXTNULL);
                vals.Add(ushort.Parse(args));
                break;
            case "WAIT": // Event pause Variable.
                vals.Add(1);
                vals.Add(KEY_TEXTWAIT);
                vals.Add(ushort.Parse(args));
                break;
            case "VAR": // Text Variable
                GetVariableParameters(config, args, vals);
                break;
            default: throw new Exception($"Unknown variable method type: {variable}");
        }
        return vals;
    }
    
    private void GetRubyValues(ReadOnlySpan<char> ruby, List<ushort> vals)
    {
        int split1 = ruby.IndexOf('|');
        if (split1 < 0)
            throw new ArgumentException($"Incorrectly formatted ruby text: {ruby}");
    
        var baseText1 = ruby[..split1];
        ruby = ruby[(split1 + 1)..];
        int split2 = ruby.IndexOf('|');
        ReadOnlySpan<char> rubyText, baseText2;
        if (split2 < 0)
        {
            rubyText = ruby;
            baseText2 = baseText1;
        }
        else
        {
            rubyText = ruby[..split2];
            baseText2 = ruby[(split2 + 1)..];
        }
        if (baseText1.Length != baseText2.Length)
            throw new ArgumentException($"Incorrectly formatted ruby text: {ruby}");
    
        vals.Add(KEY_VARIABLE);
        vals.Add(Convert.ToUInt16(3 + baseText1.Length + rubyText.Length));
        vals.Add(KEY_TEXTRUBY);
        vals.Add(Convert.ToUInt16(baseText1.Length));
        vals.Add(Convert.ToUInt16(rubyText.Length));
    
        ToU16(baseText1);
        ToU16(rubyText);
        ToU16(baseText2);
        return;

        void ToU16(ReadOnlySpan<char> text)
        {
            foreach (char c in text)
                vals.Add(TryRemapChar(c, remapChars));
        }
    }
    
    private static void GetVariableParameters(TextConfig config, ReadOnlySpan<char> text, List<ushort> vals)
    {
        int bracket = text.IndexOf('(');
        bool noArgs = bracket < 0;
        var variable = noArgs ? text : text[..bracket];
        ushort varVal = config.GetVariableNumber(variable.ToString());
    
        if (!noArgs)
        {
            int index = vals.Count;
            vals.Add(1); // change count later
            vals.Add(varVal);
            var args = text[(bracket + 1)..^1];
            // Add the hex args to the list, with a `,` separator. When done, revise the index to the final count.
            int count = 1;
            while (args.Length > 0)
            {
                int comma = args.IndexOf(',');
                if (comma == -1)
                    comma = args.Length;
                if (ushort.TryParse(args[..comma], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort result))
                    vals.Add(result);
                else
                    throw new ArgumentException($"Invalid hex value: {args[..comma]} in text: {text}");
                count++;
                int skip = comma + 1;
                if (skip >= args.Length)
                    break;
                args = args[skip..];
            }
            vals[index] = (ushort)count;
        }
        else
        {
            vals.Add(1);
            vals.Add(varVal);
        }
    }
}

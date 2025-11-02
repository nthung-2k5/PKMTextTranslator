using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PKMTextTranslator.Text;

public partial class TextFile(TextConfig config, bool remapChars = false): List<TextLine>
{
    public static bool SetEmptyText { get; set; } = true;

    // Text Formatting Config
    public const ushort KEY_BASE = 0x7C89;
    public const ushort KEY_ADVANCE = 0x2983;
    public const ushort KEY_VARIABLE = 0x0010;
    public const ushort KEY_TERMINATOR = 0x0000;
    public const ushort KEY_TEXTRETURN = 0xBE00;
    public const ushort KEY_TEXTCLEAR = 0xBE01;
    public const ushort KEY_TEXTWAIT = 0xBE02;
    public const ushort KEY_TEXTNULL = 0xBDFF;
    public const ushort KEY_TEXTRUBY = 0xFF01;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetLineKey(ushort index) => (ushort)(KEY_BASE + index * KEY_ADVANCE);
    
    private static byte[] CryptLineData(ReadOnlySpan<byte> data, ushort key)
    {
        byte[] result = data.ToArray();
        
        if (!BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < result.Length; i += 2)
            {
                result[i + 0] ^= (byte)key;
                result[i + 1] ^= (byte)(key >> 8);
                key = (ushort)(key << 3 | key >> 13);
            }

            return result;
        }

        foreach (ref ushort u16 in MemoryMarshal.Cast<byte, ushort>(result))
        {
            u16 ^= key;
            key = (ushort)(key << 3 | key >> 13);
        }
        
        return result;
    }
        
    private static T SplitAndAdvance<T>(ref ReadOnlySpan<T> span)
    {
        var val = span[0];
        span = span[1..];
        return val;
    }
    
    private static ReadOnlySpan<T> SplitAndAdvance<T>(ref ReadOnlySpan<T> span, int count)
    {
        var val = span[..count];
        span = span[count..];
        return val;
    }
    
    private readonly record struct TextLineInfo(int Offset, ushort Length, ushort Flags);

    private readonly record struct TextHeader(ushort TextSections, ushort LineCount, uint TotalLength, uint InitialKey, uint SectionDataOffset, uint SectionLength)
    {
        public TextHeader(ushort LineCount, uint TotalLength): this(1, LineCount, TotalLength, 0, 0x10, TotalLength) {}
        
        public void Validate(long fileLength)
        {
            if (InitialKey != 0)
                throw new Exception("Invalid initial key! Not 0?");
            if (SectionDataOffset + TotalLength != fileLength || TextSections != 1 || SectionDataOffset != 0x10)
                throw new Exception("Invalid Text File");
            if (SectionLength != TotalLength)
                throw new Exception("Section size and overall size do not match.");
        }
    }
}

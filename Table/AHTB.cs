using System.Diagnostics;

namespace PKMTextTranslator.Table;

/// <summary>
/// Asynchronous Hash TaBle containing file names and their corresponding hashes.
/// </summary>
/// <remarks>gfl::container::HashTable</remarks>
// ReSharper disable once InconsistentNaming
public class AHTB: List<AHTBEntry>
{
    public const uint MAGIC = 0x42544841; // AHTB

    public static AHTB Deserialize(BinaryReader reader)
    {
        uint magic = reader.ReadUInt32();
        Debug.Assert(magic == MAGIC);
        uint count = reader.ReadUInt32();

        var ret = new AHTB();
        ret.AddRange(Enumerable.Range(0, (int)count).Select(_ => AHTBEntry.Deserialize(reader)));
        
        return ret;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(MAGIC);
        writer.Write((uint)Count);

        foreach (var entry in this)
        {
            entry.Serialize(writer);
        }
    }
    
    public string[] ToKeys() => this.Select(z => z.ToString()).ToArray();
}
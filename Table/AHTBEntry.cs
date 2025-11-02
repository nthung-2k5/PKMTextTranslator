using System.Diagnostics;
using System.Text;

namespace PKMTextTranslator.Table;

// ReSharper disable once InconsistentNaming
public readonly record struct AHTBEntry(string Name)
{
    public ulong Hash => FnvHash.HashFnv1a_64(Name);
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Hash);
        byte[] bytes = Encoding.UTF8.GetBytes(Name);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
        writer.Write((byte)0); // \0 terminator
    }

    public static AHTBEntry Deserialize(BinaryReader reader)
    {
        ulong hash = reader.ReadUInt64();
        ushort nameLength = reader.ReadUInt16();
        string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength)[..^1]); // Remove null terminator
        Debug.Assert(hash == FnvHash.HashFnv1a_64(name), $"Hash mismatch for entry {name}: read 0x{hash:X16}, computed 0x{FnvHash.HashFnv1a_64(name):X16}");
       
        return new AHTBEntry(name);
    }

    public override string ToString() => Name;
}
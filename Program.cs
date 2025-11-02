// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommandDotNet;
using nietras.SeparatedValues;
using PKMTextTranslator.Table;
using PKMTextTranslator.Text;

namespace PKMTextTranslator;

// ReSharper disable once ClassNeverInstantiated.Global
[SuppressMessage("Performance", "CA1822:Mark members as static")]
internal class Program
{
    public static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    // ReSharper disable once UnusedMember.Global
    public void Deserialize(string path, string? exportDirectory = null)
    {
        var files = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.dat", SearchOption.AllDirectories) : [path];
        exportDirectory ??= Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        
        Parallel.ForEach(files, file => {
            byte[] data = File.ReadAllBytes(file);
            var textFile = TextFile.Deserialize(data, TextConfig.Default);

            string[]? keys = null;
            if (File.Exists(Path.ChangeExtension(file, "tbl")))
            {
                using var tableFile = File.OpenRead(Path.ChangeExtension(file, "tbl"));
                using var reader = new BinaryReader(tableFile);
                keys = AHTB.Deserialize(reader).ToKeys();
                
                Debug.Assert(keys.Length == textFile.Count + 1);
                Debug.Assert(keys[^1] == $"msg_{Path.GetFileNameWithoutExtension(file)}_max");
            }
            
            string exportPath = Path.Combine(exportDirectory, Path.GetRelativePath(path, Path.ChangeExtension(file, "csv")));
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);

            using var writer = Sep.Writer().ToFile(exportPath);

            for (int i = 0; i < textFile.Count; i++)
            {
                (string text, ushort flags) = textFile[i];

                using var row = writer.NewRow();

                if (keys != null) row["Key"].Set(keys[i]);
                row["Text"].Set(text);
                row["Flags"].Set(flags.ToString("X4"));
            }
        });
    }
    

    // ReSharper disable once UnusedMember.Global
    public void Serialize(string path, string? exportDirectory = null)
    {
        var files = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.csv", SearchOption.AllDirectories) : [path];
        exportDirectory ??= Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        
        Parallel.ForEach(files, file => {
            using var reader = Sep.Reader().FromFile(file);
            var textFile = new TextFile(TextConfig.Default);

            foreach (var row in reader)
            {
                string text = row["Text"].ToString();
                ushort flags = Convert.ToUInt16(row["Flags"].ToString(), 16);

                textFile.Add(new TextLine(text, flags));
            }

            using var dataStream = File.OpenWrite(Path.Combine(exportDirectory, Path.GetRelativePath(path, Path.ChangeExtension(file, "dat"))));
            using var dataWriter = new BinaryWriter(dataStream);
            textFile.Serialize(dataWriter);
        });
    }
}
# PKM Text Translator

A standalone tool for deserializing and serializing Pokémon game text files (.dat) to and from CSV format.

## Background

This project extracts the text file handling functionality from [kwsch/pkNX](https://github.com/kwsch/pkNX) repository and puts it in a separate,
focused project. The main pkNX repository hasn't been updated to support newer game versions yet (which at the time of
writing, Pokémon Legends: Z-A), so this standalone tool was created to enable translation work to continue independently.

## Requirements

- .NET 9.0 or later

## Usage

### Deserialize (Export to CSV)

Convert game text files to CSV format:

```bash
PKMTextTranslator Deserialize <path> [exportDirectory]
```

**Examples:**
```bash
# Single file
PKMTextTranslator Deserialize "C:\game\text\common.dat"

# Entire directory (recursive)
PKMTextTranslator Deserialize "C:\game\text" "C:\output"
```

### Serialize (Import from CSV)

Convert CSV files back to game format:

```bash
PKMTextTranslator Serialize <path> [exportDirectory]
```

**Examples:**
```bash
# Single file
PKMTextTranslator Serialize "C:\translation\common.csv"

# Entire directory (recursive)
PKMTextTranslator Serialize "C:\translation" "C:\game\text"
```

## CSV Format

The exported CSV files contain three columns:

- **Key**: Text entry identifier (if `.tbl` file is present)
- **Text**: The actual text content with special formatting codes
- **Flags**: Hexadecimal flags for text behavior (e.g., `0000`, `0001`)

## Dependencies

- [CommandDotNet](https://github.com/bilal-fazlani/commanddotnet) - Command-line interface framework
- [Sep](https://github.com/nietras/Sep) - High-performance CSV parser

## Todo
- Add support for mapping hexadecimal game variables to more meaningful names during serialization/deserialization

## Credits

The text file serialization/deserialization logic is based on code from [kwsch/pkNX](https://github.com/kwsch/pkNX), created by kwsch and contributors. This project exists as a standalone tool to support translation efforts while the main repository updates to support newer game versions.

## License

Please refer to the original pkNX repository for licensing information regarding the text file handling code.


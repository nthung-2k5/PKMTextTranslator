using System.Globalization;

namespace PKMTextTranslator.Text;

/// <summary>
/// Version specific text parsing configuration object to interact with text variable codes.
/// </summary>
public class TextConfig(GameVersion game)
{
    internal static readonly TextConfig Default = new(GameVersion.Any);
    private static readonly char[] TrimHex = ['0', 'x'];
    private readonly TextVariableCode[] variables = TextVariableCode.GetVariables(game);

    public IEnumerable<string> GetVariableList() => variables.Select(z => $"{z.Code:X4}={z.Name}");

    private TextVariableCode? GetCode(string name) => Array.Find(variables, v => v.Name == name);
    private TextVariableCode? GetName(ushort value) => Array.Find(variables, v => v.Code == value);

    /// <summary>
    /// Gets the machine-friendly variable instruction code to be written to the data.
    /// </summary>
    /// <param name="variable">Variable name</param>
    public ushort GetVariableNumber(string variable)
    {
        var v = GetCode(variable);
        if (v != null)
            return v.Code;
        return ushort.TryParse(variable.TrimStart(TrimHex), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort result) ? result : throw new ArgumentException($"Variable parse error: {variable}. Expected a hexadecimal value or standard variable code.");
    }

    /// <summary>
    /// Gets the human-friendly variable instruction name to be written to the output text line.
    /// </summary>
    /// <param name="variable">Variable code</param>
    public string GetVariableString(ushort variable)
    {
        var v = GetName(variable);
        return v?.Name ?? variable.ToString("X4");
    }
}

namespace EqGameLauncher.Config;

public class EqGameSettings
{
    /// <summary>
    ///     Example: C:\Games\P99
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    ///     Apply XP SP3 + RunAsAdmin registry flags.
    /// </summary>
    public bool ApplyCompatibilityFlags { get; set; } = true;

    /// <summary>
    ///     Patch eqclient.ini using IniValues.
    /// </summary>
    public bool PatchIni { get; set; } = true;

    /// <summary>
    ///     Key/value list of eqclient.ini enforced settings.
    ///     These override or append in the INI.
    /// </summary>
    public Dictionary<string, string> IniValues { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using EqGameLauncher.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace EqGameLauncher;

[SupportedOSPlatform("windows")]
public class EqGameLauncher
{
    private readonly ILogger<EqGameLauncher> _logger;
    private readonly EqGameSettings _settings;

    public EqGameLauncher(
        ILogger<EqGameLauncher> logger,
        IOptions<EqGameSettings> options)
    {
        _logger = logger;
        _settings = options.Value;
    }

    public async Task LaunchAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.InstallPath))
            throw new InvalidOperationException("EqGame InstallPath is not configured.");

        var eqFolder = _settings.InstallPath;
        var exePath = Path.Combine(eqFolder, "eqgame.exe");
        var iniPath = Path.Combine(eqFolder, "eqclient.ini");

        if (!File.Exists(exePath))
            throw new FileNotFoundException("eqgame.exe not found", exePath);

        if (!File.Exists(iniPath))
            _logger.LogWarning("eqclient.ini not found at {IniPath}. It will not be patched.", iniPath);

        _logger.LogInformation("Preparing to launch EverQuest from {Folder}", eqFolder);

        if (_settings.PatchIni && File.Exists(iniPath)) PatchEqClientIni(iniPath);

        if (_settings.ApplyCompatibilityFlags) ApplyCompatibilityFlags(exePath);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "patchme",
            WorkingDirectory = eqFolder,
            UseShellExecute = true, // required for Verb=runas
            Verb = "runas" // triggers UAC Run as Administrator
        };

        _logger.LogInformation("Launching eqgame.exe in XP SP3 compatibility mode as admin...");

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start eqgame.exe");

        _logger.LogInformation("EverQuest launched with PID {Pid}", process.Id);

        // Optional: wait a bit for the window to appear if you later want to
        // do borderless fullscreen/window management here.
        await Task.CompletedTask;
    }

    private void PatchEqClientIni(string iniPath)
    {
        _logger.LogInformation("Applying eqclient.ini overrides from configuration...");

        var lines = File.ReadAllLines(iniPath).ToList();

        foreach (var (key, value) in _settings.IniValues) SetOrUpdate(lines, key, value);

        File.WriteAllLines(iniPath, lines, Encoding.UTF8);

        _logger.LogInformation("INI patch complete: {Count} settings updated",
            _settings.IniValues.Count);
    }


    private void SetOrUpdate(List<string> lines, string key, string value)
    {
        var index = lines.FindIndex(l => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            lines[index] = $"{key}={value}";
        else
            lines.Add($"{key}={value}");
    }

    /// <summary>
    ///     Sets Windows XP SP3 + RunAsAdmin compatibility flags for eqgame.exe
    ///     under HKCU (per-user, no admin needed to set).
    ///     Registry path:
    ///     HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers
    ///     Value name: full path to eqgame.exe
    ///     Value data: "~ WINXPSP3 RUNASADMIN"
    /// </summary>
    private void ApplyCompatibilityFlags(string exePath)
    {
        const string layersKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string flags = "~ WINXPSP3 RUNASADMIN";

        _logger.LogInformation("Applying compatibility flags '{Flags}' for {ExePath}", flags, exePath);

        using var key = Registry.CurrentUser.CreateSubKey(layersKeyPath)
                        ?? throw new InvalidOperationException($"Unable to open or create HKCU\\{layersKeyPath}");

        key.SetValue(exePath, flags, RegistryValueKind.String);
    }
}
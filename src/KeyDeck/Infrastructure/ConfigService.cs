using System.Diagnostics;
using System.IO;
using System.Text.Json;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure.Config;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Persists application settings and profiles as JSON files under <c>%APPDATA%\KeyDeck</c>.
/// All file writes are <em>atomic</em>: data is written to a sibling <c>.tmp</c> file first
/// and then swapped into place via <see cref="File.Replace"/>, so the original is never
/// corrupted if the process is killed mid-write.
/// </summary>
public sealed class ConfigService : IConfigService
{
    // ── JSON serializer options ─────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // ── IConfigService — path properties ───────────────────────────────────

    /// <inheritdoc/>
    public string AppDataDirectory { get; }

    /// <inheritdoc/>
    public string ProfilesDirectory { get; }

    /// <inheritdoc/>
    public string IconsDirectory { get; }

    /// <inheritdoc/>
    public string SettingsFilePath { get; }

    // ── Constructor ────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises path properties and ensures all required directories exist.
    /// </summary>
    /// <param name="customAppDataDirectory">Optional override for the root application data directory (useful in tests).</param>
    /// <param name="environmentService">Optional environment abstraction for path and variable resolution.</param>
    public ConfigService(string? customAppDataDirectory = null, KeyDeck.Core.Abstractions.IEnvironmentService? environmentService = null)
    {
        var env = environmentService ?? KeyDeck.Infrastructure.Services.EnvironmentService.Instance;
        var appData = customAppDataDirectory ?? env.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataDirectory  = customAppDataDirectory != null ? customAppDataDirectory : Path.Combine(appData, AppConstants.AppDataFolderName);
        ProfilesDirectory = Path.Combine(AppDataDirectory, AppConstants.ProfilesFolderName);
        IconsDirectory    = Path.Combine(AppDataDirectory, AppConstants.IconsFolderName);
        SettingsFilePath  = Path.Combine(AppDataDirectory, AppConstants.SettingsFileName);

        EnsureDirectoriesExist();
    }

    // ── IConfigService — settings ──────────────────────────────────────────

    /// <inheritdoc/>
    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    return settings;
                }

                Trace.WriteLine("[ConfigService] Settings file is null after deserialization; using defaults.");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] LoadSettings failed, using defaults. Error: {ex.Message}");
        }

        var defaultSettings = new AppSettings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    /// <inheritdoc/>
    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            AtomicWriteFile(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] SaveSettings failed: {ex.Message}");
        }
    }

    // ── IConfigService — profiles ──────────────────────────────────────────

    /// <inheritdoc/>
    public List<Profile> LoadAllProfiles()
    {
        var profiles = new List<Profile>();

        try
        {
            var files = Directory.GetFiles(ProfilesDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json    = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
                    if (profile != null)
                    {
                        profiles.Add(profile);
                    }
                    else
                    {
                        Trace.WriteLine($"[ConfigService] Profile file '{file}' deserialized to null; skipping.");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[ConfigService] Skipping corrupt profile file '{file}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] Failed to enumerate profiles directory: {ex.Message}");
        }

        if (profiles.Count == 0)
        {
            Trace.WriteLine("[ConfigService] No profiles found; seeding defaults.");
            profiles = SeedDefaultProfiles();
        }

        return profiles;
    }

    /// <inheritdoc/>
    public void SaveProfile(Profile profile)
    {
        try
        {
            var filePath = Path.Combine(ProfilesDirectory, $"{profile.Id}.json");
            var json     = JsonSerializer.Serialize(profile, JsonOptions);
            AtomicWriteFile(filePath, json);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] SaveProfile '{profile.Id}' failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void DeleteProfile(string profileId)
    {
        try
        {
            var filePath = Path.Combine(ProfilesDirectory, $"{profileId}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] DeleteProfile '{profileId}' failed: {ex.Message}");
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            Directory.CreateDirectory(ProfilesDirectory);
            Directory.CreateDirectory(IconsDirectory);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] Failed to create application directories: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a sibling <c>.tmp</c> file and then atomically
    /// replaces <paramref name="destinationPath"/> using <see cref="File.Replace"/>.
    /// The original file is never touched until the write succeeds, so a crash
    /// mid-write leaves the previous data intact.
    /// </summary>
    /// <param name="destinationPath">The final target file path.</param>
    /// <param name="content">UTF-8 text content to write.</param>
    private static void AtomicWriteFile(string destinationPath, string content)
    {
        var tmpPath = destinationPath + ".tmp";

        // Write to tmp first — if this throws, destinationPath is untouched.
        File.WriteAllText(tmpPath, content, System.Text.Encoding.UTF8);

        if (File.Exists(destinationPath))
        {
            // Replace atomically; .bak is kept briefly then deleted.
            File.Replace(tmpPath, destinationPath, destinationPath + ".bak");
            // Clean up backup — ignore failure (OS may still have it locked briefly).
            try { File.Delete(destinationPath + ".bak"); } catch { /* non-critical */ }
        }
        else
        {
            // First-time save: move tmp into place.
            File.Move(tmpPath, destinationPath);
        }
    }

    // ── Default profile seeding ────────────────────────────────────────────

    /// <summary>
    /// Creates and persists three starter profiles (Productivity, Media &amp; OBS, Developer)
    /// on first run or when the profiles directory is empty.
    /// </summary>
    private List<Profile> SeedDefaultProfiles()
    {
        Profile? productivity = null;
        try
        {
            var bundledPath = Path.Combine(AppContext.BaseDirectory, "Resources", "default-profile.json");
            if (File.Exists(bundledPath))
            {
                var bundledJson = File.ReadAllText(bundledPath);
                productivity = JsonSerializer.Deserialize<Profile>(bundledJson, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ConfigService] Could not load bundled default-profile.json, using fallback: {ex.Message}");
        }

        if (productivity == null || productivity.Bindings.Count == 0)
        {
            productivity = new Profile("Productivity") { Id = "default-productivity" };
            productivity.SetAction(NumpadKeyId.Numpad7, new KeyAction { Type = ActionType.LaunchApp,    Label = "Notepad",      TargetPath = "notepad.exe" });
            productivity.SetAction(NumpadKeyId.Numpad8, new KeyAction { Type = ActionType.LaunchApp,    Label = "Calculator",   TargetPath = "calc.exe" });
            productivity.SetAction(NumpadKeyId.Numpad9, new KeyAction { Type = ActionType.OpenFolder,   Label = "Downloads",    TargetPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads" });
            productivity.SetAction(NumpadKeyId.Numpad4, new KeyAction { Type = ActionType.OpenUrl,      Label = "Google",       TargetPath = "https://www.google.com" });
            productivity.SetAction(NumpadKeyId.Numpad5, new KeyAction { Type = ActionType.OpenUrl,      Label = "GitHub",       TargetPath = "https://github.com" });
            productivity.SetAction(NumpadKeyId.Numpad6, new KeyAction { Type = ActionType.SystemAction, Label = "Screenshot",   SystemCommand = SystemCommand.TakeScreenshot });
            productivity.SetAction(NumpadKeyId.Numpad1, new KeyAction { Type = ActionType.SystemAction, Label = "Minimize All", SystemCommand = SystemCommand.MinimizeAll });
            productivity.SetAction(NumpadKeyId.Numpad2, new KeyAction { Type = ActionType.SystemAction, Label = "Lock PC",      SystemCommand = SystemCommand.LockWorkstation });
            productivity.SetAction(NumpadKeyId.Numpad3, new KeyAction { Type = ActionType.SystemAction, Label = "Empty Trash",  SystemCommand = SystemCommand.EmptyRecycleBin });
            productivity.SetAction(NumpadKeyId.Numpad0, new KeyAction { Type = ActionType.TextPaste,    Label = "Signature",    TextToPaste = "Best regards,\nSent via KeyDeck Macro Pad" });
            productivity.SetAction(NumpadKeyId.NumpadDecimal, new KeyAction { Type = ActionType.LaunchApp, Label = "Explorer",  TargetPath = "explorer.exe" });
        }

        var media = new Profile("Media & OBS") { Id = "default-media" };
        media.SetAction(NumpadKeyId.Numpad7, new KeyAction { Type = ActionType.MediaControl, Label = "Prev Track",  MediaCommand = MediaCommand.PrevTrack });
        media.SetAction(NumpadKeyId.Numpad8, new KeyAction { Type = ActionType.MediaControl, Label = "Play / Pause", MediaCommand = MediaCommand.PlayPause });
        media.SetAction(NumpadKeyId.Numpad9, new KeyAction { Type = ActionType.MediaControl, Label = "Next Track",  MediaCommand = MediaCommand.NextTrack });
        media.SetAction(NumpadKeyId.NumpadAdd,      new KeyAction { Type = ActionType.MediaControl, Label = "Volume Up",   MediaCommand = MediaCommand.VolumeUp });
        media.SetAction(NumpadKeyId.NumpadSubtract, new KeyAction { Type = ActionType.MediaControl, Label = "Volume Down", MediaCommand = MediaCommand.VolumeDown });
        media.SetAction(NumpadKeyId.NumpadMultiply, new KeyAction { Type = ActionType.MediaControl, Label = "Mute",        MediaCommand = MediaCommand.VolumeMute });

        var dev = new Profile("Developer") { Id = "default-developer" };
        dev.SetAction(NumpadKeyId.Numpad7, new KeyAction { Type = ActionType.LaunchApp, Label = "PowerShell", TargetPath = "powershell.exe" });
        dev.SetAction(NumpadKeyId.Numpad8, new KeyAction { Type = ActionType.LaunchApp, Label = "Cmd",        TargetPath = "cmd.exe" });
        dev.SetAction(NumpadKeyId.Numpad0, new KeyAction { Type = ActionType.TextPaste, Label = "git status", TextToPaste = "git status\n" });
        dev.SetAction(NumpadKeyId.Numpad1, new KeyAction { Type = ActionType.TextPaste, Label = "git commit", TextToPaste = "git commit -m \"\"" });

        SaveProfile(productivity);
        SaveProfile(media);
        SaveProfile(dev);

        return new List<Profile> { productivity, media, dev };
    }
}

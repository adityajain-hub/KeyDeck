using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Defines the persistence contract for application settings and profiles.
/// Consumers depend on this interface rather than the concrete <c>ConfigService</c>,
/// enabling testability and future storage-backend swaps.
/// </summary>
public interface IConfigService
{
    /// <summary>Gets the absolute path to the application data directory.</summary>
    string AppDataDirectory { get; }

    /// <summary>Gets the absolute path to the directory containing profile JSON files.</summary>
    string ProfilesDirectory { get; }

    /// <summary>Gets the absolute path to the directory containing cached icon PNG files.</summary>
    string IconsDirectory { get; }

    /// <summary>Gets the absolute path to the global settings JSON file.</summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Loads application settings from disk.
    /// Returns defaults and persists them when no settings file exists or the file is corrupt.
    /// </summary>
    AppSettings LoadSettings();

    /// <summary>
    /// Atomically persists <paramref name="settings"/> to disk.
    /// Writes to a sibling <c>.tmp</c> file first so the original is never
    /// corrupted if the process is interrupted mid-write.
    /// </summary>
    void SaveSettings(AppSettings settings);

    /// <summary>
    /// Loads all profiles from <see cref="ProfilesDirectory"/>.
    /// Seeds and returns the three built-in default profiles when the directory is empty.
    /// </summary>
    List<Profile> LoadAllProfiles();

    /// <summary>
    /// Atomically persists <paramref name="profile"/> to its own JSON file under
    /// <see cref="ProfilesDirectory"/>.
    /// </summary>
    void SaveProfile(Profile profile);

    /// <summary>
    /// Deletes the JSON file for the profile identified by <paramref name="profileId"/>.
    /// Silently succeeds if the file does not exist.
    /// </summary>
    void DeleteProfile(string profileId);
}

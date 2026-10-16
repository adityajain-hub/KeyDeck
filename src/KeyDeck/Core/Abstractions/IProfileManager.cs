using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Defines the contract for managing user profiles and their key-action bindings.
/// </summary>
public interface IProfileManager
{
    IReadOnlyList<Profile> Profiles { get; }

    Profile ActiveProfile { get; }

    event Action<Profile>? ActiveProfileChanged;

    event Action? ProfilesListChanged;

    event Action<NumpadKeyId, KeyAction>? BindingChanged;

    void SetActiveProfile(string profileId);

    Profile CreateProfile(string name);

    Profile DuplicateProfile(string sourceProfileId, string newName);

    void RenameProfile(string profileId, string newName);

    void DeleteProfile(string profileId);

    void UpdateBinding(NumpadKeyId keyId, KeyAction action);

    KeyAction GetActiveAction(NumpadKeyId keyId);

    /// <summary>
    /// Links or unlinks a foreground executable name (e.g., <c>Code.exe</c>) for automatic profile switching.
    /// </summary>
    void SetProfileLinkedApp(string profileId, string? linkedProcessName);

    /// <summary>
    /// Searches for a profile linked to <paramref name="processName"/> and activates it if found.
    /// </summary>
    bool TryActivateProfileForProcess(string processName);
}

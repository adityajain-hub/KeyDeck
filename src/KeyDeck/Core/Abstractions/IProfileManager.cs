using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Defines the contract for managing user profiles and their key-action bindings.
/// </summary>
public interface IProfileManager
{
    /// <summary>Gets a read-only snapshot of all loaded profiles.</summary>
    IReadOnlyList<Profile> Profiles { get; }

    /// <summary>Gets the currently active profile.</summary>
    Profile ActiveProfile { get; }

    /// <summary>Raised when the active profile changes. Carries the new active profile.</summary>
    event Action<Profile>? ActiveProfileChanged;

    /// <summary>Raised when the profiles list is structurally modified (add / remove / rename).</summary>
    event Action? ProfilesListChanged;

    /// <summary>
    /// Raised when a key binding in the active profile is updated.
    /// Carries the changed key and the new action.
    /// </summary>
    event Action<NumpadKeyId, KeyAction>? BindingChanged;

    /// <summary>
    /// Switches the active profile to the one identified by <paramref name="profileId"/>.
    /// No-ops if the profile is already active or does not exist.
    /// </summary>
    void SetActiveProfile(string profileId);

    /// <summary>
    /// Creates a new profile with <paramref name="name"/>, persists it, and activates it.
    /// </summary>
    Profile CreateProfile(string name);

    /// <summary>
    /// Creates a deep copy of the profile identified by <paramref name="sourceProfileId"/>
    /// under <paramref name="newName"/>, persists the duplicate, and activates it.
    /// </summary>
    Profile DuplicateProfile(string sourceProfileId, string newName);

    /// <summary>
    /// Renames the profile identified by <paramref name="profileId"/> and persists the change.
    /// </summary>
    void RenameProfile(string profileId, string newName);

    /// <summary>
    /// Deletes the profile identified by <paramref name="profileId"/>.
    /// Refuses to delete the last remaining profile.
    /// If the deleted profile was active, the first remaining profile becomes active.
    /// </summary>
    void DeleteProfile(string profileId);

    /// <summary>
    /// Updates the binding for <paramref name="keyId"/> in the active profile to
    /// <paramref name="action"/> and persists the change.
    /// </summary>
    void UpdateBinding(NumpadKeyId keyId, KeyAction action);

    /// <summary>
    /// Returns the <see cref="KeyAction"/> assigned to <paramref name="keyId"/>
    /// in the active profile, or an empty <c>None</c> action if no binding exists.
    /// </summary>
    KeyAction GetActiveAction(NumpadKeyId keyId);
}

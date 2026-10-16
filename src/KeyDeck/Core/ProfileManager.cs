using System.Diagnostics;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.Core;

/// <summary>
/// Manages the collection of user profiles, active profile selection, and
/// key-binding modifications for the active profile.
/// </summary>
/// <remarks>
/// <para>
/// All mutating operations persist changes to disk via <see cref="IConfigService"/>
/// immediately after updating in-memory state, so there is no separate "save" step.
/// </para>
/// <para>
/// Events are raised synchronously on the thread that performed the mutation.
/// UI subscribers must marshal to the dispatcher themselves if needed.
/// </para>
/// </remarks>
public sealed class ProfileManager : IProfileManager
{
    private readonly IConfigService _configService;
    private readonly AppSettings    _settings;
    private readonly List<Profile>  _profiles;
    private Profile                 _activeProfile;

    // ── IProfileManager — properties ───────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<Profile> Profiles => _profiles.AsReadOnly();

    /// <inheritdoc/>
    public Profile ActiveProfile => _activeProfile;

    // ── IProfileManager — events ───────────────────────────────────────────

    /// <inheritdoc/>
    public event Action<Profile>? ActiveProfileChanged;

    /// <inheritdoc/>
    public event Action? ProfilesListChanged;

    /// <inheritdoc/>
    public event Action<NumpadKeyId, KeyAction>? BindingChanged;

    // ── Constructor ────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the manager by loading all persisted profiles and restoring
    /// the previously active profile (falling back to the first profile).
    /// </summary>
    /// <param name="configService">Persistence service for profiles and settings.</param>
    /// <param name="settings">Global application settings that track the active profile ID.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configService"/> or <paramref name="settings"/> is <see langword="null"/>.
    /// </exception>
    public ProfileManager(IConfigService configService, AppSettings settings)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _settings      = settings      ?? throw new ArgumentNullException(nameof(settings));

        _profiles = _configService.LoadAllProfiles();

        var selected = _profiles.FirstOrDefault(p => p.Id == _settings.ActiveProfileId);
        _activeProfile = selected ?? _profiles.First();

        // Ensure settings always reflect the resolved active profile.
        _settings.ActiveProfileId = _activeProfile.Id;

        Trace.WriteLine($"[ProfileManager] Loaded {_profiles.Count} profile(s); active = '{_activeProfile.Name}'.");
    }

    // ── IProfileManager — profile switching ───────────────────────────────

    /// <inheritdoc/>
    public void SetActiveProfile(string profileId)
    {
        var target = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (target == null)
        {
            Trace.WriteLine($"[ProfileManager] SetActiveProfile: profile '{profileId}' not found.");
            return;
        }

        if (target == _activeProfile)
        {
            return;
        }

        _activeProfile = target;
        _settings.ActiveProfileId = target.Id;
        _configService.SaveSettings(_settings);

        Trace.WriteLine($"[ProfileManager] Active profile changed to '{_activeProfile.Name}'.");
        ActiveProfileChanged?.Invoke(_activeProfile);
    }

    // ── IProfileManager — CRUD ─────────────────────────────────────────────

    /// <inheritdoc/>
    public Profile CreateProfile(string name)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "New Profile" : name.Trim();
        var profile   = new Profile(cleanName);

        _profiles.Add(profile);
        _configService.SaveProfile(profile);

        Trace.WriteLine($"[ProfileManager] Created profile '{cleanName}' (id={profile.Id}).");
        ProfilesListChanged?.Invoke();
        SetActiveProfile(profile.Id);

        return profile;
    }

    /// <inheritdoc/>
    public Profile DuplicateProfile(string sourceProfileId, string newName)
    {
        var source    = _profiles.FirstOrDefault(p => p.Id == sourceProfileId) ?? _activeProfile;
        var duplicate = source.Clone(newName);

        _profiles.Add(duplicate);
        _configService.SaveProfile(duplicate);

        Trace.WriteLine($"[ProfileManager] Duplicated '{source.Name}' → '{newName}' (id={duplicate.Id}).");
        ProfilesListChanged?.Invoke();
        SetActiveProfile(duplicate.Id);

        return duplicate;
    }

    /// <inheritdoc/>
    public void RenameProfile(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            Trace.WriteLine($"[ProfileManager] RenameProfile: new name is empty; no-op.");
            return;
        }

        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            Trace.WriteLine($"[ProfileManager] RenameProfile: profile '{profileId}' not found.");
            return;
        }

        var oldName   = profile.Name;
        profile.Name  = newName.Trim();
        _configService.SaveProfile(profile);

        Trace.WriteLine($"[ProfileManager] Renamed '{oldName}' → '{profile.Name}'.");
        ProfilesListChanged?.Invoke();
    }

    /// <inheritdoc/>
    public void DeleteProfile(string profileId)
    {
        if (_profiles.Count <= 1)
        {
            Trace.WriteLine("[ProfileManager] DeleteProfile: refusing to delete the last profile.");
            return;
        }

        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            Trace.WriteLine($"[ProfileManager] DeleteProfile: profile '{profileId}' not found.");
            return;
        }

        _profiles.Remove(profile);
        _configService.DeleteProfile(profileId);

        Trace.WriteLine($"[ProfileManager] Deleted profile '{profile.Name}'.");

        if (_activeProfile.Id == profileId)
        {
            SetActiveProfile(_profiles.First().Id);
        }

        ProfilesListChanged?.Invoke();
    }

    // ── IProfileManager — binding management ──────────────────────────────

    /// <inheritdoc/>
    public void UpdateBinding(NumpadKeyId keyId, KeyAction action)
    {
        _activeProfile.SetAction(keyId, action);
        _configService.SaveProfile(_activeProfile);

        Trace.WriteLine($"[ProfileManager] Binding updated: key={keyId}, type={action.Type}, profile='{_activeProfile.Name}'.");
        BindingChanged?.Invoke(keyId, action);
    }

    /// <inheritdoc/>
    public KeyAction GetActiveAction(NumpadKeyId keyId)
    {
        // Pure read — if no binding exists, returns a default empty action without mutating bindings.
        return _activeProfile.GetAction(keyId);
    }
}

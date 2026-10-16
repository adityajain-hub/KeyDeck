using System.Diagnostics;
using System.IO;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.Core;

/// <summary>
/// Manages the collection of user profiles, active profile selection, per-app auto-switching,
/// and key-binding modifications for the active profile.
/// </summary>
public sealed class ProfileManager : IProfileManager
{
    private readonly IConfigService _configService;
    private readonly AppSettings    _settings;
    private readonly List<Profile>  _profiles;
    private Profile                 _activeProfile;

    public IReadOnlyList<Profile> Profiles => _profiles.AsReadOnly();

    public Profile ActiveProfile => _activeProfile;

    public event Action<Profile>? ActiveProfileChanged;

    public event Action? ProfilesListChanged;

    public event Action<NumpadKeyId, KeyAction>? BindingChanged;

    public ProfileManager(IConfigService configService, AppSettings settings)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _settings      = settings      ?? throw new ArgumentNullException(nameof(settings));

        _profiles = _configService.LoadAllProfiles();

        var selected = _profiles.FirstOrDefault(p => p.Id == _settings.ActiveProfileId);
        _activeProfile = selected ?? _profiles.First();

        _settings.ActiveProfileId = _activeProfile.Id;

        Trace.WriteLine($"[ProfileManager] Loaded {_profiles.Count} profile(s); active = '{_activeProfile.Name}'.");
    }

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

    public void RenameProfile(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            Trace.WriteLine("[ProfileManager] RenameProfile: new name is empty; no-op.");
            return;
        }

        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            Trace.WriteLine($"[ProfileManager] RenameProfile: profile '{profileId}' not found.");
            return;
        }

        var oldName  = profile.Name;
        profile.Name = newName.Trim();
        _configService.SaveProfile(profile);

        Trace.WriteLine($"[ProfileManager] Renamed '{oldName}' → '{profile.Name}'.");
        ProfilesListChanged?.Invoke();
    }

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

    public void UpdateBinding(NumpadKeyId keyId, KeyAction action)
    {
        _activeProfile.SetAction(keyId, action);
        _configService.SaveProfile(_activeProfile);

        Trace.WriteLine($"[ProfileManager] Binding updated: key={keyId}, type={action.Type}, profile='{_activeProfile.Name}'.");
        BindingChanged?.Invoke(keyId, action);
    }

    public KeyAction GetActiveAction(NumpadKeyId keyId)
    {
        return _activeProfile.GetAction(keyId);
    }

    public void SetProfileLinkedApp(string profileId, string? linkedProcessName)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            return;
        }

        var normalized = string.IsNullOrWhiteSpace(linkedProcessName)
            ? null
            : Path.GetFileName(linkedProcessName.Trim());

        if (string.Equals(profile.LinkedProcessName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        profile.LinkedProcessName = normalized;
        _configService.SaveProfile(profile);

        Trace.WriteLine($"[ProfileManager] Linked process for '{profile.Name}' set to '{normalized ?? "(none)"}'.");
        ProfilesListChanged?.Invoke();
    }

    public bool TryActivateProfileForProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var targetExe = Path.GetFileName(processName.Trim());
        var targetNoExt = Path.GetFileNameWithoutExtension(targetExe);

        foreach (var profile in _profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.LinkedProcessName))
            {
                continue;
            }

            // Support single process ("Code.exe") or comma/semicolon-separated list ("Code.exe; devenv.exe")
            var candidates = profile.LinkedProcessName.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawCandidate in candidates)
            {
                var candidate = Path.GetFileName(rawCandidate.Trim());
                var candidateNoExt = Path.GetFileNameWithoutExtension(candidate);

                if (string.Equals(candidate, targetExe, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidateNoExt, targetNoExt, StringComparison.OrdinalIgnoreCase))
                {
                    SetActiveProfile(profile.Id);
                    return true;
                }
            }
        }

        return false;
    }
}

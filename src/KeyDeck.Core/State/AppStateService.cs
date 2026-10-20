using System.Diagnostics;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.Core.State;

/// <summary>
/// Central reactive state service implementation.
/// Serves as the single source of truth for runtime application state,
/// synchronizing mutations to persistent storage and notifying UI consumers.
/// </summary>
public sealed class AppStateService : IAppStateService
{
    private readonly AppSettings _settings;
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
    private readonly object _syncRoot = new();

    public AppSettings Settings => _settings;

    public bool IsMacroEnabled => _settings.IsMacroEnabled;

    public bool IsAutoProfileSwitchEnabled => _settings.IsAutoProfileSwitchEnabled;

    public HudVisibilityMode HudMode => _settings.HudMode;

    public double HudOpacity => _settings.HudOpacity;

    public bool IsHudLocked => _settings.IsHudLocked;

    public AppTheme Theme => _settings.Theme;

    public Profile ActiveProfile => _profileManager.ActiveProfile;

    public event Action<AppStateChangedEventArgs>? StateChanged;
    public event Action? ResetHudPositionRequested;

    public AppStateService(
        AppSettings settings,
        IConfigService configService,
        IProfileManager profileManager)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

        _profileManager.ActiveProfileChanged += OnActiveProfileChanged;
    }

    private void OnActiveProfileChanged(Profile profile)
    {
        _settings.ActiveProfileId = profile.Id;
        SaveSettingsSafe();
        NotifyStateChanged(AppStateProperty.ActiveProfile, profile);
    }

    public void SetMacroEnabled(bool enabled)
    {
        lock (_syncRoot)
        {
            if (_settings.IsMacroEnabled == enabled) return;
            _settings.IsMacroEnabled = enabled;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.MacroInterception, enabled);
    }

    public void SetAutoProfileSwitchEnabled(bool enabled)
    {
        lock (_syncRoot)
        {
            if (_settings.IsAutoProfileSwitchEnabled == enabled) return;
            _settings.IsAutoProfileSwitchEnabled = enabled;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.AutoProfileSwitch, enabled);
    }

    public void SetHudMode(HudVisibilityMode mode)
    {
        lock (_syncRoot)
        {
            if (_settings.HudMode == mode) return;
            _settings.HudMode = mode;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.HudVisibilityMode, mode);
    }

    public void SetHudOpacity(double opacity)
    {
        var clamped = Math.Round(Math.Clamp(opacity, 0.20, 1.00), 2);
        lock (_syncRoot)
        {
            if (Math.Abs(_settings.HudOpacity - clamped) < 0.001) return;
            _settings.HudOpacity = clamped;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.HudOpacity, clamped);
    }

    public void SetHudLocked(bool isLocked)
    {
        lock (_syncRoot)
        {
            if (_settings.IsHudLocked == isLocked) return;
            _settings.IsHudLocked = isLocked;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.HudLockedState, isLocked);
    }

    public void SetHudPosition(double left, double top)
    {
        lock (_syncRoot)
        {
            _settings.HudLeft = left;
            _settings.HudTop = top;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.HudPosition, (left, top));
    }

    public void SetTheme(AppTheme theme)
    {
        lock (_syncRoot)
        {
            if (_settings.Theme == theme) return;
            _settings.Theme = theme;
            SaveSettingsSafe();
        }
        NotifyStateChanged(AppStateProperty.Theme, theme);
    }

    public void SetActiveProfile(string profileId)
    {
        _profileManager.SetActiveProfile(profileId);
    }

    public void RequestResetHudPosition()
    {
        ResetHudPositionRequested?.Invoke();
    }

    public void SaveSettings()
    {
        lock (_syncRoot)
        {
            SaveSettingsSafe();
        }
    }

    private void SaveSettingsSafe()
    {
        try
        {
            _configService.SaveSettings(_settings);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AppStateService] Error saving settings: {ex.Message}");
        }
    }

    private void NotifyStateChanged(AppStateProperty property, object? value)
    {
        StateChanged?.Invoke(new AppStateChangedEventArgs(property, value));
    }
}

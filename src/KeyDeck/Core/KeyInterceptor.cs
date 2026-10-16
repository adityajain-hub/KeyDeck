using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Core;

/// <summary>
/// Coordinates low-level keyboard hooking and Num Lock state to intercept numpad macro inputs.
/// </summary>
public sealed class KeyInterceptor : IKeyInterceptor
{
    private readonly KeyboardHook _hook;
    private readonly INumLockMonitor _numLockMonitor;
    private readonly AppSettings _settings;
    private bool _isDisposed;

    public bool IsTemporaryBypassEnabled { get; set; }

    /// <summary>
    /// Optional predicate evaluated on held keys to determine if typematic auto-repeat
    /// should re-trigger <see cref="NumpadKeyPressed"/> (used for VolumeUp/VolumeDown).
    /// </summary>
    public Func<NumpadKeyId, bool>? AllowKeyRepeatPredicate { get; set; }

    public bool IsMacroModeActive
    {
        get
        {
            if (!_settings.IsMacroEnabled)
            {
                return false;
            }

            var numLockOn = _numLockMonitor.IsNumLockOn;
            return _settings.NumLockMode == NumLockMacroMode.OffMeansMacro ? !numLockOn : numLockOn;
        }
    }

    public event Action<NumpadKeyId>? NumpadKeyPressed;
    public event Action<NumpadKeyId>? NumpadKeyReleased;
    public event Action<bool>? MacroModeStateChanged;

    public KeyInterceptor(AppSettings settings, INumLockMonitor numLockMonitor)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _numLockMonitor = numLockMonitor ?? throw new ArgumentNullException(nameof(numLockMonitor));

        _hook = new KeyboardHook
        {
            ShouldInterceptFilter = ShouldIntercept,
            AllowKeyRepeatFilter  = ShouldAllowRepeat
        };

        _hook.KeyPressed += keyId => NumpadKeyPressed?.Invoke(keyId);
        _hook.KeyReleased += keyId => NumpadKeyReleased?.Invoke(keyId);
        _hook.NumLockToggled += OnNumLockToggled;

        _numLockMonitor.NumLockChanged += _ => NotifyMacroState();
    }

    public void Start()
    {
        _hook.Install();
        NotifyMacroState();
    }

    public void Stop()
    {
        _hook.Uninstall();
    }

    private bool ShouldIntercept(NumpadKeyId keyId)
    {
        if (IsTemporaryBypassEnabled)
        {
            return false;
        }

        return IsMacroModeActive;
    }

    private bool ShouldAllowRepeat(NumpadKeyId keyId)
    {
        if (IsTemporaryBypassEnabled || !IsMacroModeActive)
        {
            return false;
        }

        return AllowKeyRepeatPredicate?.Invoke(keyId) ?? false;
    }

    private void OnNumLockToggled()
    {
        _numLockMonitor.Refresh();
        NotifyMacroState();
    }

    public void NotifyMacroState()
    {
        MacroModeStateChanged?.Invoke(IsMacroModeActive);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _hook.Dispose();
            _isDisposed = true;
        }
    }
}

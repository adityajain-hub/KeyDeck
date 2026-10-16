using KeyDeck.Core.Models;
using KeyDeck.Platform.Win32;

namespace KeyDeck.Core;

/// <summary>
/// Coordinates low-level keyboard hooking and Num Lock state to intercept numpad macro inputs.
/// </summary>
public sealed class KeyInterceptor : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly NumLockMonitor _numLockMonitor;
    private readonly AppSettings _settings;
    private bool _isDisposed;

    public bool IsTemporaryBypassEnabled { get; set; }

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

    public KeyInterceptor(AppSettings settings, NumLockMonitor numLockMonitor)
    {
        _settings = settings;
        _numLockMonitor = numLockMonitor;

        _hook = new KeyboardHook
        {
            ShouldInterceptFilter = ShouldIntercept
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

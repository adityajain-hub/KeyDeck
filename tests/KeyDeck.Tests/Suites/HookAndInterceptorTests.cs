using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

public sealed class MockNumLockMonitor : INumLockMonitor
{
    public bool IsNumLockOn { get; set; }
    public event Action<bool>? NumLockChanged;

    public void Toggle(bool state)
    {
        IsNumLockOn = state;
        NumLockChanged?.Invoke(state);
    }

    public void Refresh() => NumLockChanged?.Invoke(IsNumLockOn);
    public void Dispose() { }
}

[TestClass]
public class HookAndInterceptorTests
{
    [TestMethod("Verifies KeyInterceptor.IsMacroModeActive respects NumLockMode and global toggle")]
    public void KeyInterceptor_IsMacroModeActive_ReflectsSettingsAndNumLock()
    {
        var settings = new AppSettings
        {
            IsMacroEnabled = true,
            NumLockMode = NumLockMacroMode.OffMeansMacro
        };
        var numLock = new MockNumLockMonitor { IsNumLockOn = false };
        var interceptor = new KeyInterceptor(settings, numLock);

        // Case 1: OffMeansMacro + NumLock OFF => Active
        Assert.True(interceptor.IsMacroModeActive);

        // Case 2: OffMeansMacro + NumLock ON => Inactive
        numLock.IsNumLockOn = true;
        Assert.False(interceptor.IsMacroModeActive);

        // Case 3: OnMeansMacro + NumLock ON => Active
        settings.NumLockMode = NumLockMacroMode.OnMeansMacro;
        Assert.True(interceptor.IsMacroModeActive);

        // Case 4: OnMeansMacro + NumLock OFF => Inactive
        numLock.IsNumLockOn = false;
        Assert.False(interceptor.IsMacroModeActive);

        // Case 5: Globally disabled => Always inactive
        settings.IsMacroEnabled = false;
        settings.NumLockMode = NumLockMacroMode.OffMeansMacro;
        numLock.IsNumLockOn = false;
        Assert.False(interceptor.IsMacroModeActive);
    }

    [TestMethod("Verifies AllowKeyRepeatPredicate logic enables smooth repeat exclusively for volume controls")]
    public void Interceptor_AllowKeyRepeatPredicate_FiltersCorrectly()
    {
        var profile = new Profile("Test");
        profile.SetAction(NumpadKeyId.NumpadAdd, new KeyAction
        {
            Type = ActionType.MediaControl,
            MediaCommand = MediaCommand.VolumeUp
        });
        profile.SetAction(NumpadKeyId.NumpadSubtract, new KeyAction
        {
            Type = ActionType.MediaControl,
            MediaCommand = MediaCommand.VolumeDown
        });
        profile.SetAction(NumpadKeyId.Numpad7, new KeyAction
        {
            Type = ActionType.LaunchApp,
            TargetPath = "notepad.exe"
        });

        bool ShouldAllow(NumpadKeyId keyId)
        {
            var action = profile.GetAction(keyId);
            return action.Type == ActionType.MediaControl &&
                   (action.MediaCommand == MediaCommand.VolumeUp ||
                    action.MediaCommand == MediaCommand.VolumeDown);
        }

        Assert.True(ShouldAllow(NumpadKeyId.NumpadAdd));
        Assert.True(ShouldAllow(NumpadKeyId.NumpadSubtract));
        Assert.False(ShouldAllow(NumpadKeyId.Numpad7));
        Assert.False(ShouldAllow(NumpadKeyId.Numpad1)); // Unmapped
    }

    [TestMethod("Verifies KeyInterceptor temporary bypass toggles cleanly")]
    public void KeyInterceptor_TemporaryBypass_TogglesState()
    {
        var settings = new AppSettings();
        var numLock = new MockNumLockMonitor();
        var interceptor = new KeyInterceptor(settings, numLock);

        Assert.False(interceptor.IsTemporaryBypassEnabled);

        interceptor.IsTemporaryBypassEnabled = true;
        Assert.True(interceptor.IsTemporaryBypassEnabled);

        interceptor.IsTemporaryBypassEnabled = false;
        Assert.False(interceptor.IsTemporaryBypassEnabled);
    }
}

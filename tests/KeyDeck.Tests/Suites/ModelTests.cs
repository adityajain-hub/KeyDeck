using System.Text.Json;
using KeyDeck.Core.Models;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class ModelTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [TestMethod("Verifies that Profile.Clone deep-copies bindings and generates a fresh GUID ID")]
    public void Profile_Clone_CreatesUniqueIdAndClonesBindings()
    {
        var original = new Profile("Original")
        {
            Id = "orig-id",
            LinkedProcessName = "Code.exe"
        };
        original.SetAction(NumpadKeyId.Numpad7, new KeyAction
        {
            Type = ActionType.LaunchApp,
            Label = "Editor",
            TargetPath = "notepad.exe"
        });

        var clone = original.Clone("Cloned Profile");

        Assert.Equal("Cloned Profile", clone.Name);
        Assert.NotEqual("orig-id", clone.Id);
        Assert.Equal("Code.exe", clone.LinkedProcessName);
        Assert.Equal(1, clone.Bindings.Count);

        var clonedAction = clone.GetAction(NumpadKeyId.Numpad7);
        Assert.Equal(ActionType.LaunchApp, clonedAction.Type);
        Assert.Equal("Editor", clonedAction.Label);
        Assert.Equal("notepad.exe", clonedAction.TargetPath);

        // Verify deep copy: mutating clone does not mutate original
        clonedAction.Label = "Modified";
        Assert.Equal("Editor", original.GetAction(NumpadKeyId.Numpad7).Label);
    }

    [TestMethod("Verifies Profile.GetAction returns default ActionType.None when unmapped without mutating collection")]
    public void Profile_GetAction_ReturnsEmptyActionWhenKeyNotMapped()
    {
        var profile = new Profile("Empty");
        var action = profile.GetAction(NumpadKeyId.Numpad1);

        Assert.NotNull(action);
        Assert.Equal(ActionType.None, action.Type);
        Assert.Equal(0, profile.Bindings.Count);
    }

    [TestMethod("Verifies KeyAction.Clone deep copies sub-actions and target profile ID")]
    public void KeyAction_Clone_CopiesAllFieldsIncludingTargetProfileIdAndSubActions()
    {
        var action = new KeyAction
        {
            Type = ActionType.MultiAction,
            Label = "Multi Step",
            TargetProfileId = "target-prof-123",
            DelayMs = 250,
            SubActions = new List<KeyAction>
            {
                new() { Type = ActionType.LaunchApp, Label = "Step 1", TargetPath = "calc.exe" },
                new() { Type = ActionType.SwitchProfile, Label = "Step 2", TargetProfileId = "target-prof-456" }
            }
        };

        var clone = action.Clone();

        Assert.Equal(action.Type, clone.Type);
        Assert.Equal(action.Label, clone.Label);
        Assert.Equal("target-prof-123", clone.TargetProfileId);
        Assert.Equal(250, clone.DelayMs);
        Assert.Equal(2, clone.SubActions.Count);
        Assert.Equal("target-prof-456", clone.SubActions[1].TargetProfileId);

        // Verify deep independence
        clone.SubActions[0].Label = "Changed";
        Assert.Equal("Step 1", action.SubActions[0].Label);
    }

    [TestMethod("Verifies AppSettings defaults have sane values")]
    public void AppSettings_Defaults_HaveValidRanges()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsMacroEnabled);
        Assert.False(settings.IsAutoProfileSwitchEnabled);
        Assert.True(settings.IsHudLocked);
        Assert.True(settings.HudOpacity >= 0.20 && settings.HudOpacity <= 1.00);
        Assert.Equal(HudVisibilityMode.AlwaysOnWhenNumLockOff, settings.HudMode);
        Assert.Equal(NumLockMacroMode.OffMeansMacro, settings.NumLockMode);
        Assert.Equal(AppTheme.Dark, settings.Theme);
    }

    [TestMethod("Verifies Profile JSON roundtrip serialization")]
    public void JsonSerialization_Profile_RoundtripsSuccessfully()
    {
        var profile = new Profile("Productivity Test")
        {
            Id = "test-prof-777",
            LinkedProcessName = "devenv.exe; Code.exe"
        };
        profile.SetAction(NumpadKeyId.Numpad9, new KeyAction
        {
            Type = ActionType.OpenUrl,
            Label = "Google",
            TargetPath = "https://www.google.com"
        });

        var json = JsonSerializer.Serialize(profile, JsonOpts);
        var restored = JsonSerializer.Deserialize<Profile>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(profile.Id, restored!.Id);
        Assert.Equal(profile.Name, restored.Name);
        Assert.Equal(profile.LinkedProcessName, restored.LinkedProcessName);
        Assert.Equal(ActionType.OpenUrl, restored.GetAction(NumpadKeyId.Numpad9).Type);
        Assert.Equal("https://www.google.com", restored.GetAction(NumpadKeyId.Numpad9).TargetPath);
    }

    [TestMethod("Verifies AppSettings JSON roundtrip serialization")]
    public void JsonSerialization_AppSettings_RoundtripsSuccessfully()
    {
        var settings = new AppSettings
        {
            HudOpacity = 0.95,
            HudLeft = 100.5,
            HudTop = 200.5,
            IsHudLocked = false,
            IsAutoProfileSwitchEnabled = true,
            Theme = AppTheme.Light
        };

        var json = JsonSerializer.Serialize(settings, JsonOpts);
        var restored = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(0.95, restored!.HudOpacity);
        Assert.Equal(100.5, restored.HudLeft);
        Assert.Equal(200.5, restored.HudTop);
        Assert.False(restored.IsHudLocked);
        Assert.True(restored.IsAutoProfileSwitchEnabled);
        Assert.Equal(AppTheme.Light, restored.Theme);
    }
}

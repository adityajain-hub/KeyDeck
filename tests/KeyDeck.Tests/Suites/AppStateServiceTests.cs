using System.IO;
using KeyDeck.Core;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;
using KeyDeck.Core.State;
using KeyDeck.Infrastructure;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class AppStateServiceTests
{
    private static string CreateTempDir()
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyDeckTest_State_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static void DeleteTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    [TestMethod("Verifies AppStateService.SetMacroEnabled updates state, saves settings, and fires StateChanged")]
    public void AppStateService_SetMacroEnabled_UpdatesSettingsAndRaisesEvent()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings { IsMacroEnabled = true };
            var manager = new ProfileManager(config, settings);
            var stateService = new AppStateService(settings, config, manager);

            AppStateChangedEventArgs? eventArgs = null;
            stateService.StateChanged += e => eventArgs = e;

            stateService.SetMacroEnabled(false);

            Assert.False(stateService.IsMacroEnabled);
            Assert.False(settings.IsMacroEnabled);
            Assert.NotNull(eventArgs);
            Assert.Equal(AppStateProperty.MacroInterception, eventArgs!.Property);
            Assert.Equal(false, eventArgs.NewValue);

            // Verify persistence
            var reloaded = config.LoadSettings();
            Assert.False(reloaded.IsMacroEnabled);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies AppStateService.SetHudOpacity clamps value between 0.20 and 1.00")]
    public void AppStateService_SetHudOpacity_ClampsBetween20And100Percent()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings { HudOpacity = 0.85 };
            var manager = new ProfileManager(config, settings);
            var stateService = new AppStateService(settings, config, manager);

            // Test under-clamp
            stateService.SetHudOpacity(0.05);
            Assert.Equal(0.20, stateService.HudOpacity);

            // Test over-clamp
            stateService.SetHudOpacity(1.50);
            Assert.Equal(1.00, stateService.HudOpacity);

            // Test normal valid value
            stateService.SetHudOpacity(0.60);
            Assert.Equal(0.60, stateService.HudOpacity);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies AppStateService.RequestResetHudPosition triggers ResetHudPositionRequested")]
    public void AppStateService_RequestResetHudPosition_FiresResetEvent()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);
            var stateService = new AppStateService(settings, config, manager);

            bool fired = false;
            stateService.ResetHudPositionRequested += () => fired = true;

            stateService.RequestResetHudPosition();

            Assert.True(fired);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies AppStateService reacts to ProfileManager active profile switches")]
    public void AppStateService_ActiveProfileChanged_SynchronizesSettingsAndRaisesEvent()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);
            var stateService = new AppStateService(settings, config, manager);

            var initialProfile = manager.ActiveProfile;
            var newProfile = manager.CreateProfile("Design Profile");

            AppStateChangedEventArgs? eventArgs = null;
            stateService.StateChanged += e => eventArgs = e;

            stateService.SetActiveProfile(initialProfile.Id);

            Assert.Equal(initialProfile.Id, stateService.ActiveProfile.Id);
            Assert.NotNull(eventArgs);
            Assert.Equal(AppStateProperty.ActiveProfile, eventArgs!.Property);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }
}

using System.IO;
using KeyDeck.Core;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Platform.Linux.Native;
using KeyDeck.Platform.Linux.Services;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class LinuxPlatformTests
{
    private static string CreateTempDir()
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyDeckTest_LNX_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static void DeleteTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    [TestMethod("Verifies LinuxInputCodes.TryMapToNumpadKey correctly maps kernel keycodes to NumpadKeyId")]
    public void LinuxInputCodes_TryMapToNumpadKey_MapsAllNumpadKeys()
    {
        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP0, out var k0));
        Assert.Equal(NumpadKeyId.Numpad0, k0);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP1, out var k1));
        Assert.Equal(NumpadKeyId.Numpad1, k1);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP2, out var k2));
        Assert.Equal(NumpadKeyId.Numpad2, k2);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP3, out var k3));
        Assert.Equal(NumpadKeyId.Numpad3, k3);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP4, out var k4));
        Assert.Equal(NumpadKeyId.Numpad4, k4);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP5, out var k5));
        Assert.Equal(NumpadKeyId.Numpad5, k5);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP6, out var k6));
        Assert.Equal(NumpadKeyId.Numpad6, k6);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP7, out var k7));
        Assert.Equal(NumpadKeyId.Numpad7, k7);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP8, out var k8));
        Assert.Equal(NumpadKeyId.Numpad8, k8);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KP9, out var k9));
        Assert.Equal(NumpadKeyId.Numpad9, k9);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPDOT, out var kDot));
        Assert.Equal(NumpadKeyId.NumpadDecimal, kDot);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPPLUS, out var kPlus));
        Assert.Equal(NumpadKeyId.NumpadAdd, kPlus);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPMINUS, out var kMinus));
        Assert.Equal(NumpadKeyId.NumpadSubtract, kMinus);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPASTERISK, out var kMul));
        Assert.Equal(NumpadKeyId.NumpadMultiply, kMul);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPSLASH, out var kDiv));
        Assert.Equal(NumpadKeyId.NumpadDivide, kDiv);

        Assert.True(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_KPENTER, out var kEnt));
        Assert.Equal(NumpadKeyId.NumpadEnter, kEnt);

        // Non-numpad key must return false
        Assert.False(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_A, out _));
        Assert.False(LinuxInputCodes.TryMapToNumpadKey(LinuxInputCodes.KEY_SPACE, out _));
    }

    [TestMethod("Verifies LinuxInputCodes.MapWinVkToLinuxKey accurately translates Windows VK chords to Linux evdev")]
    public void LinuxInputCodes_MapWinVkToLinuxKey_TranslatesModifiersAndKeys()
    {
        // Modifiers
        Assert.Equal(LinuxInputCodes.KEY_LEFTSHIFT, LinuxInputCodes.MapWinVkToLinuxKey(0x10));
        Assert.Equal(LinuxInputCodes.KEY_LEFTCTRL, LinuxInputCodes.MapWinVkToLinuxKey(0x11));
        Assert.Equal(LinuxInputCodes.KEY_LEFTALT, LinuxInputCodes.MapWinVkToLinuxKey(0x12));
        Assert.Equal(LinuxInputCodes.KEY_LEFTMETA, LinuxInputCodes.MapWinVkToLinuxKey(0x5B)); // Windows / Super key

        // Navigation
        Assert.Equal(LinuxInputCodes.KEY_ESC, LinuxInputCodes.MapWinVkToLinuxKey(0x1B));
        Assert.Equal(LinuxInputCodes.KEY_ENTER, LinuxInputCodes.MapWinVkToLinuxKey(0x0D));
        Assert.Equal(LinuxInputCodes.KEY_DELETE, LinuxInputCodes.MapWinVkToLinuxKey(0x2E));

        // Media
        Assert.Equal(LinuxInputCodes.KEY_VOLUMEUP, LinuxInputCodes.MapWinVkToLinuxKey(0xAF));
        Assert.Equal(LinuxInputCodes.KEY_VOLUMEDOWN, LinuxInputCodes.MapWinVkToLinuxKey(0xAE));
        Assert.Equal(LinuxInputCodes.KEY_PLAYPAUSE, LinuxInputCodes.MapWinVkToLinuxKey(0xB3));

        // Letters
        Assert.Equal(LinuxInputCodes.KEY_A, LinuxInputCodes.MapWinVkToLinuxKey(0x41));
        Assert.Equal(LinuxInputCodes.KEY_Z, LinuxInputCodes.MapWinVkToLinuxKey(0x5A));
    }

    [TestMethod("Verifies LinuxActionExecutor handles empty, null, and SwitchProfile actions cleanly")]
    public async Task LinuxActionExecutor_ExecutesSafely()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);
            var executor = new LinuxActionExecutor(virtualKeyboard: null, profileManager: manager);

            // Null & None
            await executor.ExecuteAsync(null);
            await executor.ExecuteAsync(new KeyAction { Type = ActionType.None });

            // Switch Profile
            var p1 = manager.CreateProfile("Linux Test Profile");
            manager.SetActiveProfile(manager.Profiles.First(p => p.Id != p1.Id).Id);
            Assert.NotEqual(p1.Id, manager.ActiveProfile.Id);

            await executor.ExecuteAsync(new KeyAction
            {
                Type = ActionType.SwitchProfile,
                TargetProfileId = p1.Id
            });

            Assert.Equal(p1.Id, manager.ActiveProfile.Id);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }
}

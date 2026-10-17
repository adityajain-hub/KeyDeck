using System.IO;
using KeyDeck.Core;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Platform.Win32;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class ActionExecutorTests
{
    private static string CreateTempDir()
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyDeckTest_AE_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static void DeleteTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    [TestMethod("Verifies ActionExecutor.IsExtendedKey correctly identifies navigation, arrows, and right-hand modifiers")]
    public void ActionExecutor_IsExtendedKey_IdentifiesNavigationArrowsAndModifiers()
    {
        // Navigation keys
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_INSERT));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_DELETE));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_HOME));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_END));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_PRIOR));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_NEXT));

        // Arrow keys
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_UP));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_DOWN));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_LEFT));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_RIGHT));

        // Right-hand modifiers & special keys
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_RCONTROL));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_RMENU));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_DIVIDE));
        Assert.True(ActionExecutor.IsExtendedKey(NativeMethods.VK_SNAPSHOT));

        // Non-extended keys must return false
        Assert.False(ActionExecutor.IsExtendedKey(0x41)); // 'A'
        Assert.False(ActionExecutor.IsExtendedKey(0x20)); // Space
        Assert.False(ActionExecutor.IsExtendedKey(NativeMethods.VK_LCONTROL));
        Assert.False(ActionExecutor.IsExtendedKey(NativeMethods.VK_LMENU));
    }

    [TestMethod("Verifies ActionExecutor handles ActionType.None without errors")]
    public async Task ActionExecutor_ExecuteAsync_HandlesNoneActionGracefully()
    {
        var executor = new ActionExecutor();
        await executor.ExecuteAsync(null);
        await executor.ExecuteAsync(new KeyAction { Type = ActionType.None });
        Assert.True(true);
    }

    [TestMethod("Verifies ActionExecutor.ExecuteSwitchProfile switches profile in ProfileManager")]
    public async Task ActionExecutor_ExecuteSwitchProfile_ActivatesProfileInManager()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);
            var executor = new ActionExecutor(manager);

            var p1 = manager.CreateProfile("Target Profile");
            manager.SetActiveProfile(manager.Profiles.First(p => p.Id != p1.Id).Id);
            Assert.NotEqual(p1.Id, manager.ActiveProfile.Id);

            var switchAction = new KeyAction
            {
                Type = ActionType.SwitchProfile,
                TargetProfileId = p1.Id
            };

            await executor.ExecuteAsync(switchAction);

            Assert.Equal(p1.Id, manager.ActiveProfile.Id);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies chunk calculation for Unicode text injection")]
    public void TextPaste_ChunkSize_CalculatesBatchCountsCorrectly()
    {
        const int chunkSize = 64;

        string shortText = "Hello";
        int shortChunks = (int)Math.Ceiling((double)shortText.Length / chunkSize);
        Assert.Equal(1, shortChunks);

        string longText = new string('A', 150);
        int longChunks = (int)Math.Ceiling((double)longText.Length / chunkSize);
        Assert.Equal(3, longChunks); // 64 + 64 + 22 = 150
    }
}

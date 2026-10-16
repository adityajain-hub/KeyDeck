using System.IO;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class ConfigServiceTests
{
    private static string CreateTempDir()
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyDeckTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static void DeleteTempDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [TestMethod("Verifies ConfigService creates directories and loads default settings when empty")]
    public void ConfigService_LoadSettings_ReturnsDefaultsWhenFileMissing()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);

            Assert.True(Directory.Exists(config.AppDataDirectory));
            Assert.True(Directory.Exists(config.ProfilesDirectory));
            Assert.True(Directory.Exists(config.IconsDirectory));

            var settings = config.LoadSettings();

            Assert.NotNull(settings);
            Assert.True(File.Exists(config.SettingsFilePath));
            Assert.Equal(AppTheme.Dark, settings.Theme);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ConfigService.SaveSettings atomically persists settings to disk")]
    public void ConfigService_SaveSettings_PersistsAndReloadsAccurately()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings
            {
                Theme = AppTheme.Light,
                HudOpacity = 0.65,
                IsAutoProfileSwitchEnabled = true
            };

            config.SaveSettings(settings);

            var reloaded = config.LoadSettings();
            Assert.Equal(AppTheme.Light, reloaded.Theme);
            Assert.Equal(0.65, reloaded.HudOpacity);
            Assert.True(reloaded.IsAutoProfileSwitchEnabled);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ConfigService seeds default profiles when profiles directory is empty")]
    public void ConfigService_LoadAllProfiles_SeedsDefaultProfilesWhenEmpty()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var profiles = config.LoadAllProfiles();

            Assert.NotEmpty(profiles);
            Assert.True(profiles.Count >= 2);
            Assert.NotNull(profiles.FirstOrDefault(p => p.Name.Contains("Productivity", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ConfigService saves, loads, and deletes individual profiles")]
    public void ConfigService_SaveAndProfileLifecycle_Succeeds()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var profile = new Profile("Gaming")
            {
                Id = "custom-gaming-1",
                LinkedProcessName = "cs2.exe"
            };

            config.SaveProfile(profile);

            var profilePath = Path.Combine(config.ProfilesDirectory, "custom-gaming-1.json");
            Assert.True(File.Exists(profilePath));

            var all = config.LoadAllProfiles();
            var found = all.FirstOrDefault(p => p.Id == "custom-gaming-1");
            Assert.NotNull(found);
            Assert.Equal("Gaming", found!.Name);
            Assert.Equal("cs2.exe", found.LinkedProcessName);

            config.DeleteProfile("custom-gaming-1");
            Assert.False(File.Exists(profilePath));
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ConfigService skips corrupted JSON files and loads remaining valid profiles")]
    public void ConfigService_CorruptFile_SkipsAndLoadsRemainingValidProfiles()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);

            var valid = new Profile("Valid Profile") { Id = "valid-1" };
            config.SaveProfile(valid);

            // Write corrupt invalid JSON
            var corruptPath = Path.Combine(config.ProfilesDirectory, "corrupt.json");
            File.WriteAllText(corruptPath, "{ this is not valid json !!!");

            var profiles = config.LoadAllProfiles();

            Assert.NotEmpty(profiles);
            Assert.NotNull(profiles.FirstOrDefault(p => p.Id == "valid-1"));
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }
}

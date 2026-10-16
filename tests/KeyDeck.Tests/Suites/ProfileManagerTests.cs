using System.IO;
using KeyDeck.Core;
using KeyDeck.Core.Models;
using KeyDeck.Infrastructure;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests.Suites;

[TestClass]
public class ProfileManagerTests
{
    private static string CreateTempDir()
    {
        var temp = Path.Combine(Path.GetTempPath(), "KeyDeckTest_PM_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    private static void DeleteTempDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    [TestMethod("Verifies ProfileManager initializes with active profile and can switch active profile")]
    public void ProfileManager_SetActiveProfile_SwitchesActiveProfileAndFiresEvent()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);

            Assert.NotEmpty(manager.Profiles);
            var initialActive = manager.ActiveProfile;
            var target = manager.Profiles.FirstOrDefault(p => p.Id != initialActive.Id);
            Assert.NotNull(target);

            Profile? eventProfile = null;
            manager.ActiveProfileChanged += p => eventProfile = p;

            manager.SetActiveProfile(target!.Id);

            Assert.Equal(target.Id, manager.ActiveProfile.Id);
            Assert.Equal(target.Id, settings.ActiveProfileId);
            Assert.NotNull(eventProfile);
            Assert.Equal(target.Id, eventProfile!.Id);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ProfileManager.CreateProfile creates, activates, and persists a new profile")]
    public void ProfileManager_CreateProfile_AddsAndActivatesNewProfile()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);

            int initialCount = manager.Profiles.Count;
            var created = manager.CreateProfile("Streaming Setup");

            Assert.Equal("Streaming Setup", created.Name);
            Assert.Equal(initialCount + 1, manager.Profiles.Count);
            Assert.Equal(created.Id, manager.ActiveProfile.Id);
            Assert.True(File.Exists(Path.Combine(config.ProfilesDirectory, $"{created.Id}.json")));
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ProfileManager refuses to delete the last remaining profile")]
    public void ProfileManager_DeleteProfile_RejectsDeletingLastRemainingProfile()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);

            // Delete down to 1
            while (manager.Profiles.Count > 1)
            {
                manager.DeleteProfile(manager.Profiles[1].Id);
            }

            Assert.Equal(1, manager.Profiles.Count);
            var lastId = manager.Profiles[0].Id;

            // Attempt to delete last remaining
            manager.DeleteProfile(lastId);

            Assert.Equal(1, manager.Profiles.Count);
            Assert.Equal(lastId, manager.Profiles[0].Id);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ProfileManager.SetProfileLinkedApp normalizes path to exe and persists")]
    public void ProfileManager_SetProfileLinkedApp_NormalizesAndPersists()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);

            var active = manager.ActiveProfile;
            manager.SetProfileLinkedApp(active.Id, @"C:\Program Files\Microsoft VS Code\Code.exe");

            Assert.Equal("Code.exe", active.LinkedProcessName);

            // Reload from disk to verify persistence
            var reloadedAll = config.LoadAllProfiles();
            var reloadedActive = reloadedAll.First(p => p.Id == active.Id);
            Assert.Equal("Code.exe", reloadedActive.LinkedProcessName);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }

    [TestMethod("Verifies ProfileManager.TryActivateProfileForProcess matches single, extensionless, case-insensitive, and delimited lists")]
    public void ProfileManager_TryActivateProfileForProcess_MatchesAllVariants()
    {
        var temp = CreateTempDir();
        try
        {
            var config = new ConfigService(temp);
            var settings = new AppSettings();
            var manager = new ProfileManager(config, settings);

            var devProfile = manager.CreateProfile("Dev");
            manager.SetProfileLinkedApp(devProfile.Id, "devenv.exe; Code.exe, Rider64.exe");

            var obsProfile = manager.CreateProfile("OBS");
            manager.SetProfileLinkedApp(obsProfile.Id, "obs64.exe");

            // 1. Exact match
            bool match1 = manager.TryActivateProfileForProcess("Code.exe");
            Assert.True(match1);
            Assert.Equal(devProfile.Id, manager.ActiveProfile.Id);

            // 2. Case-insensitive match
            bool match2 = manager.TryActivateProfileForProcess("code.exe");
            Assert.True(match2);
            Assert.Equal(devProfile.Id, manager.ActiveProfile.Id);

            // 3. Extensionless match
            bool match3 = manager.TryActivateProfileForProcess("devenv");
            Assert.True(match3);
            Assert.Equal(devProfile.Id, manager.ActiveProfile.Id);

            // 4. Match second profile in delimited list
            bool match4 = manager.TryActivateProfileForProcess("Rider64");
            Assert.True(match4);
            Assert.Equal(devProfile.Id, manager.ActiveProfile.Id);

            // 5. Match other profile
            bool match5 = manager.TryActivateProfileForProcess("obs64.exe");
            Assert.True(match5);
            Assert.Equal(obsProfile.Id, manager.ActiveProfile.Id);

            // 6. No match
            bool match6 = manager.TryActivateProfileForProcess("chrome.exe");
            Assert.False(match6);
        }
        finally
        {
            DeleteTempDir(temp);
        }
    }
}

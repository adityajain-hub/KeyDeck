using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KeyDeck.Core.Abstractions;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Avalonia.Views;

public partial class ProfileListControl : UserControl
{
    private IProfileManager? _profileManager;
    private bool _isInternalSelection;

    public event Action<Profile>? ProfileSelected;

    public ProfileListControl()
    {
        InitializeComponent();
    }

    public void Initialize(IProfileManager profileManager)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _profileManager.ProfilesListChanged += RefreshList;
        _profileManager.ActiveProfileChanged += OnActiveProfileChanged;

        RefreshList();
    }

    private void RefreshList()
    {
        if (_profileManager == null) return;

        _isInternalSelection = true;
        ProfilesListBox.ItemsSource = _profileManager.Profiles.ToList();
        ProfilesListBox.SelectedItem = _profileManager.ActiveProfile;
        _isInternalSelection = false;
    }

    private void OnActiveProfileChanged(Profile profile)
    {
        _isInternalSelection = true;
        ProfilesListBox.SelectedItem = profile;
        _isInternalSelection = false;
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInternalSelection || _profileManager == null) return;

        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            _profileManager.SetActiveProfile(selected.Id);
            ProfileSelected?.Invoke(selected);
        }
    }

    private void OnAddProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null) return;

        var count = _profileManager.Profiles.Count + 1;
        var newProfile = _profileManager.CreateProfile($"Profile {count}");
        RefreshList();
        _profileManager.SetActiveProfile(newProfile.Id);
    }

    private void OnDuplicateClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null) return;

        var current = _profileManager.ActiveProfile;
        var duplicate = _profileManager.DuplicateProfile(current.Id, $"{current.Name} (Copy)");
        RefreshList();
        _profileManager.SetActiveProfile(duplicate.Id);
    }

    private void OnRenameClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null) return;

        var current = _profileManager.ActiveProfile;
        // Prompt or toggle inline edit
        var newName = $"{current.Name}*";
        _profileManager.RenameProfile(current.Id, newName);
        RefreshList();
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null || _profileManager.Profiles.Count <= 1) return;

        var current = _profileManager.ActiveProfile;
        _profileManager.DeleteProfile(current.Id);
        RefreshList();
    }
}

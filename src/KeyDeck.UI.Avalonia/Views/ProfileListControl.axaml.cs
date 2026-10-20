using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
        Dispatcher.UIThread.Post(() =>
        {
            _isInternalSelection = true;
            ProfilesListBox.SelectedItem = profile;
            _isInternalSelection = false;
        });
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

    private async void OnRenameClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null) return;
        var current = _profileManager.ActiveProfile;

        var dialog = new Window
        {
            Title = "Rename Profile",
            Width = 340,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = SolidColorBrush.Parse("#1E1E22"),
            CanResize = false
        };

        var textBox = new TextBox
        {
            Text = current.Name,
            Margin = new Thickness(16, 16, 16, 12),
            FontSize = 13
        };

        var okButton = new Button
        {
            Content = "Save",
            Classes = { "primary" },
            Margin = new Thickness(0, 0, 8, 0),
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "secondary" },
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        okButton.Click += (_, _) =>
        {
            var text = textBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _profileManager.RenameProfile(current.Id, text);
                RefreshList();
            }
            dialog.Close();
        };

        cancelButton.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16),
            Children = { okButton, cancelButton }
        };

        dialog.Content = new StackPanel
        {
            Children = { textBox, buttons }
        };

        var parentWindow = VisualRoot as Window;
        if (parentWindow != null)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileManager == null || _profileManager.Profiles.Count <= 1) return;

        var current = _profileManager.ActiveProfile;
        _profileManager.DeleteProfile(current.Id);
        RefreshList();
    }
}

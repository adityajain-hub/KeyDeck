using System.Windows;
using System.Windows.Controls;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Controls;

public partial class ProfileListPanel : UserControl
{
    private bool _suppressSelectionEvent;

    public event Action<Profile>? ProfileSelected;
    public event Action? NewProfileRequested;
    public event Action<Profile>? DuplicateProfileRequested;
    public event Action<Profile>? RenameProfileRequested;
    public event Action<Profile>? DeleteProfileRequested;

    public ProfileListPanel()
    {
        InitializeComponent();
    }

    public void SetProfiles(IReadOnlyList<Profile> profiles, Profile activeProfile)
    {
        _suppressSelectionEvent = true;

        ProfilesListBox.ItemsSource = null;
        ProfilesListBox.ItemsSource = profiles;
        ProfilesListBox.SelectedItem = activeProfile;

        _suppressSelectionEvent = false;

        DeleteButton.IsEnabled = profiles.Count > 1;
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvent)
        {
            return;
        }

        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            ProfileSelected?.Invoke(selected);
        }
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        NewProfileRequested?.Invoke();
    }

    private void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            DuplicateProfileRequested?.Invoke(selected);
        }
    }

    private void OnRenameProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            RenameProfileRequested?.Invoke(selected);
        }
    }

    private void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            DeleteProfileRequested?.Invoke(selected);
        }
    }
}

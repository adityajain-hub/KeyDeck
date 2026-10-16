using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyDeck.Core.Models;

namespace KeyDeck.UI.Controls;

public partial class ProfileListPanel : UserControl
{
    private bool _suppressSelectionEvent;
    private bool _suppressAutoSwitchEvent;

    public event Action<Profile>? ProfileSelected;
    public event Action? NewProfileRequested;
    public event Action<Profile>? DuplicateProfileRequested;
    public event Action<Profile>? RenameProfileRequested;
    public event Action<Profile>? DeleteProfileRequested;
    public event Action<bool>? AutoSwitchToggled;
    public event Action<Profile, string?>? ProfileLinkedAppChanged;

    public ProfileListPanel()
    {
        InitializeComponent();
    }

    public void SetProfiles(IReadOnlyList<Profile> profiles, Profile activeProfile, bool? isAutoSwitchEnabled = null)
    {
        _suppressSelectionEvent = true;

        ProfilesListBox.ItemsSource = null;
        ProfilesListBox.ItemsSource = profiles;
        ProfilesListBox.SelectedItem = activeProfile;
        LinkedAppTextBox.Text = activeProfile.LinkedProcessName ?? string.Empty;

        _suppressSelectionEvent = false;

        if (isAutoSwitchEnabled.HasValue)
        {
            _suppressAutoSwitchEvent = true;
            AutoSwitchEnabledCheckBox.IsChecked = isAutoSwitchEnabled.Value;
            _suppressAutoSwitchEvent = false;
        }

        DeleteButton.IsEnabled = profiles.Count > 1;
    }

    public void SetAutoSwitchChecked(bool isEnabled)
    {
        _suppressAutoSwitchEvent = true;
        AutoSwitchEnabledCheckBox.IsChecked = isEnabled;
        _suppressAutoSwitchEvent = false;
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvent)
        {
            return;
        }

        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            LinkedAppTextBox.Text = selected.LinkedProcessName ?? string.Empty;
            ProfileSelected?.Invoke(selected);
        }
    }

    private void OnAutoSwitchCheckBoxChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoSwitchEvent) return;
        AutoSwitchToggled?.Invoke(AutoSwitchEnabledCheckBox.IsChecked == true);
    }

    private void CommitLinkedAppText()
    {
        if (ProfilesListBox.SelectedItem is Profile selected)
        {
            var raw = LinkedAppTextBox.Text.Trim();
            var normalized = string.IsNullOrWhiteSpace(raw) ? null : Path.GetFileName(raw);
            LinkedAppTextBox.Text = normalized ?? string.Empty;
            ProfileLinkedAppChanged?.Invoke(selected, normalized);
        }
    }

    private void OnLinkedAppLostFocus(object sender, RoutedEventArgs e)
    {
        CommitLinkedAppText();
    }

    private void OnLinkedAppKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitLinkedAppText();
            e.Handled = true;
        }
    }

    private void OnBrowseLinkedAppClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not Profile selected) return;

        var dlg = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = $"Select Application to Link with '{selected.Name}'"
        };

        if (dlg.ShowDialog() == true)
        {
            var exeName = Path.GetFileName(dlg.FileName);
            LinkedAppTextBox.Text = exeName;
            ProfileLinkedAppChanged?.Invoke(selected, exeName);
        }
    }

    private void OnClearLinkedAppClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not Profile selected) return;

        LinkedAppTextBox.Text = string.Empty;
        ProfileLinkedAppChanged?.Invoke(selected, null);
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using RdpManager.Models;
using RdpManager.Services;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace RdpManager
{
    public partial class MainWindow : Window
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;
        private const int WmGetMinMaxInfo = 0x0024;
        private const uint MonitorDefaultToNearest = 0x00000002;
        private const int EntriesPageSize = 10;
        private const int HealthCheckTimeoutMilliseconds = 1500;
        private const string AllGroupsFilterOption = "All groups";

        private ObservableCollection<RdpEntry> _entries = new ObservableCollection<RdpEntry>();
        private readonly ObservableCollection<RdpEntry> _pagedEntries = new ObservableCollection<RdpEntry>();
        private readonly ObservableCollection<JumpHostProfile> _jumpHostProfiles = new ObservableCollection<JumpHostProfile>();
        private readonly ObservableCollection<ProxyOption> _entryProxyOptions = new ObservableCollection<ProxyOption>();
        private readonly ObservableCollection<string> _connectionGroupOptions = new ObservableCollection<string>();
        private string _currentFilePath;
        private string _currentCsvExchangePath;
        private bool _isDirty;
        private bool _isCreatingNew = true;
        private bool _editorDirty;
        private bool _isPopulatingForm;
        private bool _isRebuildingEntriesPage;
        private bool _isUpdatingFilterSelectors;
        private bool _isUpdatingGroupFilter;
        private int _currentEntriesPage = 1;
        private int _filteredEntriesCount;
        private readonly ObservableCollection<CloudminiSyncPreviewItem> _cloudminiPreviewItems = new ObservableCollection<CloudminiSyncPreviewItem>();
        private readonly List<CloudminiVps> _cloudminiRemoteItems = new List<CloudminiVps>();
        private NavigationFilter _currentNavigationFilter = NavigationFilter.AllConnections;
        private PlatformFilter _currentConnectionsPlatformFilter = PlatformFilter.All;
        private StatusFilter _currentConnectionsStatusFilter = StatusFilter.All;
        private string _currentConnectionsGroupFilter = string.Empty;
        private PlatformFilter _currentCloudminiPlatformFilter = PlatformFilter.All;
        private StatusFilter _currentCloudminiStatusFilter = StatusFilter.All;
        private AppSection _currentAppSection = AppSection.Connections;
        private RdpEntry _editingEntry;
        private JumpHostProfile _editingJumpHostProfile;
        private AppSettings _settings;
        private string _sessionCloudminiToken;
        private Forms.NotifyIcon _trayIcon;
        private bool _trayExitRequested;
        private bool _trayHintShown;
        private bool _restoreToPseudoMaximized;
        private bool _isHandlingWindowStateChange;
        private bool _isPseudoMaximized;
        private System.Windows.Rect _restoreBounds = System.Windows.Rect.Empty;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public Point Reserved;
            public Point MaxSize;
            public Point MaxPosition;
            public Point MinTrackSize;
            public Point MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MonitorInfo
        {
            public int Size = Marshal.SizeOf(typeof(MonitorInfo));
            public Rect Monitor;
            public Rect WorkArea;
            public int Flags;
        }

        public ObservableCollection<ProxyOption> EntryProxyOptions
        {
            get { return _entryProxyOptions; }
        }

        public MainWindow()
        {
            InitializeComponent();

            EntriesGrid.ItemsSource = _pagedEntries;
            CloudminiPreviewGrid.ItemsSource = _cloudminiPreviewItems;
            ConnectionsGroupFilterComboBox.ItemsSource = _connectionGroupOptions;
            JumpHostProfilesListBox.ItemsSource = _jumpHostProfiles;
            EntryProxyComboBox.ItemsSource = _entryProxyOptions;
            _currentFilePath = SqliteStorage.GetDatabasePath();
            _currentCsvExchangePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clients.csv");
            _settings = SettingsStorage.Load();

            ApplySettingsToUi();
            SqliteStorage.EnsureInitialized(_currentFilePath);
            SqliteStorage.MigrateLegacyDataIfNeeded(_currentFilePath, _currentCsvExchangePath, JumpHostProfileStorage.GetLegacyProfilesPath());
            LoadJumpHostProfiles();
            UpdateVersionText();

            LoadEntriesFromDatabase();
            UpdateWindowTitle();
            RdpLauncher.CleanupTemporaryFiles();
            SshTunnelManager.CleanupTemporaryFiles();
            InitializeTrayIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(WindowProc);
            }
            ApplyWindowFrameTheme();
            ApplyWindowBoundsForCurrentState();
            UpdateMaximizeRestoreButtonState();
        }

        private void OpenCsvButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardIfNeeded())
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                CheckFileExists = false,
                FileName = Path.GetFileName(_currentCsvExchangePath),
                InitialDirectory = GetInitialDirectory(_currentCsvExchangePath)
            };

            if (dialog.ShowDialog(this) == true)
            {
                CsvStorage.EnsureFileExists(dialog.FileName);
                ImportEntriesFromCsv(dialog.FileName);
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveDatabase();
        }

        private void SaveAsButton_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = Path.GetFileName(_currentCsvExchangePath),
                InitialDirectory = GetInitialDirectory(_currentCsvExchangePath),
                DefaultExt = ".csv",
                AddExtension = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                ExportEntriesToCsv(dialog.FileName);
            }
        }

        private void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartNewEntry();
        }

        private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            var selected = EntriesGrid.SelectedItem as RdpEntry;
            if (selected == null)
            {
                MessageBox.Show(this, "Select an entry to delete.", "Delete Entry", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DeleteEntry(selected);
        }

        private void ApplyButton_OnClick(object sender, RoutedEventArgs e)
        {
            var wasCreatingNew = _isCreatingNew || _editingEntry == null;
            var entry = ApplyEditorToCollection();
            if (entry == null)
            {
                return;
            }

            if (wasCreatingNew)
            {
                EntriesGrid.ScrollIntoView(entry);
                StartNewEntry();
            }
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartNewEntry();
        }

        private void ConnectButton_OnClick(object sender, RoutedEventArgs e)
        {
            ConnectCurrentEntry();
        }

        private void AllConnectionsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetNavigationFilter(NavigationFilter.AllConnections);
        }

        private void FavoritesNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetNavigationFilter(NavigationFilter.Favorites);
        }

        private void RecentNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetNavigationFilter(NavigationFilter.Recent);
        }

        private void CloudminiSyncNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetAppSection(AppSection.CloudminiSync);
        }

        private void SettingsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetAppSection(AppSection.Settings);
        }

        private void FavoriteButton_OnClick(object sender, RoutedEventArgs e)
        {
            var entry = _editingEntry;
            if (entry == null)
            {
                return;
            }

            entry.IsFavorite = !entry.IsFavorite;
            MetadataStorage.Save(_currentFilePath, _entries);
            RefreshEntriesView();
            UpdateSummary();
        }

        private void TestCloudminiButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                CloudminiStatusTextBlock.Text = "Testing Cloudmini token...";

                var token = GetCloudminiToken();
                var account = CloudminiClient.GetAccountSummary(token);
                _sessionCloudminiToken = token;

                SettingsStorage.SaveCloudminiToken(_settings, token, RememberCloudminiTokenCheckBox.IsChecked == true);
                SettingsStorage.Save(_settings);

                CloudminiAccountSummaryTextBlock.Text = string.Format("Balance: {0} | Credit: {1}", account.Balance, account.Credit);
                CloudminiStatusTextBlock.Text = "Cloudmini connection successful.";
                UpdateSettingsSummary();
            }
            catch (Exception ex)
            {
                CloudminiAccountSummaryTextBlock.Text = "Not connected";
                CloudminiStatusTextBlock.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Cloudmini Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void FetchCloudminiButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                CloudminiStatusTextBlock.Text = "Fetching VPS from Cloudmini...";

                var token = GetCloudminiToken();
                var vpsItems = CloudminiClient.GetVps(token);

                _sessionCloudminiToken = token;
                _cloudminiRemoteItems.Clear();
                _cloudminiRemoteItems.AddRange(vpsItems);

                SettingsStorage.SaveCloudminiToken(_settings, token, RememberCloudminiTokenCheckBox.IsChecked == true);
                SettingsStorage.Save(_settings);

                RebuildCloudminiPreview();
                CloudminiStatusTextBlock.Text = string.Format("Loaded {0} VPS from Cloudmini.", _cloudminiRemoteItems.Count);
                UpdateSettingsSummary();
            }
            catch (Exception ex)
            {
                CloudminiStatusTextBlock.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Cloudmini Fetch Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void CloudminiOptionCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            SyncCloudminiOptionStateToSettings();
            RebuildCloudminiPreview();
        }

        private void ConnectionsPlatformFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterSelectors)
            {
                return;
            }

            _currentConnectionsPlatformFilter = ParsePlatformFilter(GetSelectedComboTag(ConnectionsPlatformFilterComboBox));
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void ConnectionsStatusFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterSelectors)
            {
                return;
            }

            _currentConnectionsStatusFilter = ParseStatusFilter(GetSelectedComboTag(ConnectionsStatusFilterComboBox));
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void ConnectionsGroupFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingGroupFilter)
            {
                return;
            }

            var selected = ConnectionsGroupFilterComboBox.SelectedItem as string;
            _currentConnectionsGroupFilter = string.IsNullOrWhiteSpace(selected) || string.Equals(selected, AllGroupsFilterOption, StringComparison.Ordinal)
                ? string.Empty
                : selected;
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void CloudminiPlatformFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterSelectors)
            {
                return;
            }

            _currentCloudminiPlatformFilter = ParsePlatformFilter(GetSelectedComboTag(CloudminiPlatformFilterComboBox));
            RebuildCloudminiPreview();
        }

        private void CloudminiStatusFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterSelectors)
            {
                return;
            }

            _currentCloudminiStatusFilter = ParseStatusFilter(GetSelectedComboTag(CloudminiStatusFilterComboBox));
            RebuildCloudminiPreview();
        }

        private void SyncSelectedCloudminiButton_OnClick(object sender, RoutedEventArgs e)
        {
            ApplyCloudminiSync(onlySelected: true);
        }

        private void SyncAllCloudminiButton_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var item in _cloudminiPreviewItems)
            {
                item.IsSelected = true;
            }

            ApplyCloudminiSync(onlySelected: false);
        }

        private void SaveSettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            _settings.KeepLocalHostName = SettingsKeepLocalHostNameCheckBox.IsChecked == true;
            _settings.OverwritePasswordFromProvider = SettingsOverwritePasswordCheckBox.IsChecked == true;
            _settings.ImportOnlyOnline = SettingsImportOnlyOnlineCheckBox.IsChecked == true;

            KeepLocalHostNameCheckBox.IsChecked = _settings.KeepLocalHostName;
            OverwritePasswordCheckBox.IsChecked = _settings.OverwritePasswordFromProvider;
            ImportOnlyOnlineCheckBox.IsChecked = _settings.ImportOnlyOnline;

            SettingsStorage.SaveCloudminiToken(_settings, CloudminiTokenPasswordBox.Password, RememberCloudminiTokenCheckBox.IsChecked == true);
            SettingsStorage.Save(_settings);
            UpdateSettingsSummary();
            RebuildCloudminiPreview();
            MessageBox.Show(this, "Settings saved.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearSavedTokenButton_OnClick(object sender, RoutedEventArgs e)
        {
            _settings.RememberCloudminiToken = false;
            _settings.EncryptedCloudminiToken = null;
            SettingsStorage.Save(_settings);

            RememberCloudminiTokenCheckBox.IsChecked = false;
            CloudminiTokenPasswordBox.Password = string.Empty;
            _sessionCloudminiToken = string.Empty;

            UpdateSettingsSummary();
            MessageBox.Show(this, "Saved Cloudmini token cleared.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddJumpHostProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartNewJumpHostProfile();
        }

        private void DeleteJumpHostProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_editingJumpHostProfile == null || string.IsNullOrWhiteSpace(_editingJumpHostProfile.Id))
            {
                MessageBox.Show(this, "Select a proxy server profile to delete.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                this,
                string.Format("Delete proxy server profile '{0}'?", _editingJumpHostProfile.DisplayName),
                "Proxy Servers",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_editingJumpHostProfile.SecretRefId))
            {
                SecretVault.DeleteSecret(_editingJumpHostProfile.SecretRefId);
            }

            if (!string.IsNullOrWhiteSpace(_editingJumpHostProfile.PassphraseSecretRefId))
            {
                SecretVault.DeleteSecret(_editingJumpHostProfile.PassphraseSecretRefId);
            }

            var remainingProfiles = _jumpHostProfiles
                .Where(profile => !string.Equals(profile.Id, _editingJumpHostProfile.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            JumpHostProfileStorage.Save(remainingProfiles);

            foreach (var entry in _entries.Where(entry => string.Equals(entry.JumpHostProfileId, _editingJumpHostProfile.Id, StringComparison.OrdinalIgnoreCase)))
            {
                entry.JumpHostProfileId = string.Empty;
                if (entry.TransportMode == TransportMode.SshTunnel)
                {
                    entry.TransportMode = TransportMode.Direct;
                }
            }

            MetadataStorage.Save(_currentFilePath, _entries);
            LoadJumpHostProfiles();
            StartNewJumpHostProfile();
            if (_editingEntry != null)
            {
                PopulateForm(_editingEntry);
            }
            UpdateSettingsSummary();
        }

        private void SaveJumpHostProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var profile = BuildJumpHostProfileFromEditor();
            if (profile == null)
            {
                return;
            }

            if (!TryPersistJumpHostSecret(profile))
            {
                return;
            }

            if (_editingJumpHostProfile != null &&
                !string.IsNullOrWhiteSpace(_editingJumpHostProfile.SecretRefId) &&
                profile.AuthMode == JumpHostAuthMode.Agent)
            {
                SecretVault.DeleteSecret(_editingJumpHostProfile.SecretRefId);
                profile.SecretRefId = string.Empty;
                profile.ImportedKeyLabel = string.Empty;
            }

            var profiles = _jumpHostProfiles.ToList();
            var existingIndex = profiles.FindIndex(item => string.Equals(item.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                profiles[existingIndex] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            JumpHostProfileStorage.Save(profiles);
            LoadJumpHostProfiles(profile.Id);
            UpdateSettingsSummary();
            MessageBox.Show(this, "Proxy server profile saved.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportJumpHostKeyButton_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "This build uses SSH password authentication only.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearJumpHostKeyButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_editingJumpHostProfile == null || string.IsNullOrWhiteSpace(_editingJumpHostProfile.Id))
            {
                MessageBox.Show(this, "Select a saved proxy server profile first.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var authMode = GetSelectedJumpHostAuthMode();
            var secretKind = SecretVault.GetSecretKind(_editingJumpHostProfile.SecretRefId);
            if (string.IsNullOrWhiteSpace(_editingJumpHostProfile.SecretRefId) || !SecretVault.HasSecret(_editingJumpHostProfile.SecretRefId))
            {
                MessageBox.Show(this, authMode == JumpHostAuthMode.Password ? "This profile does not have a stored SSH password." : "This profile does not have a stored private key.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (authMode == JumpHostAuthMode.Password && secretKind != SecretKind.SshPassword)
            {
                MessageBox.Show(this, "This profile does not have a stored SSH password.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (authMode == JumpHostAuthMode.EmbeddedPrivateKey && secretKind != SecretKind.SshPrivateKey)
            {
                MessageBox.Show(this, "This profile does not have a stored private key.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SecretVault.DeleteSecret(_editingJumpHostProfile.SecretRefId);
            _editingJumpHostProfile.SecretRefId = string.Empty;
            _editingJumpHostProfile.ImportedKeyLabel = string.Empty;
            if (JumpHostPasswordBox != null)
            {
                JumpHostPasswordBox.Password = string.Empty;
            }

            SaveJumpHostProfileFromCurrentSelection();
        }

        private void TestJumpHostProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var profile = BuildJumpHostProfileFromEditor();
            if (profile == null)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                SetJumpHostTestStatus("Testing SSH tunnel profile...", new SolidColorBrush(Color.FromRgb(105, 120, 142)));
                var message = SshTunnelManager.TestProfile(profile);
                SetJumpHostTestStatus(message, new SolidColorBrush(Color.FromRgb(47, 125, 50)));
                MessageBox.Show(this, message, "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetJumpHostTestStatus(ex.Message, new SolidColorBrush(Color.FromRgb(179, 56, 56)));
                MessageBox.Show(this, ex.Message, "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void JumpHostProfilesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var profile = JumpHostProfilesListBox.SelectedItem as JumpHostProfile;
            if (profile == null)
            {
                return;
            }

            PopulateJumpHostEditor(profile);
        }

        private void JumpHostAuthModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateJumpHostEditorState();
        }

        private void EntryProxyComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingForm)
            {
                return;
            }

            _editorDirty = true;
            UpdateTransportEditorState();
        }

        private void EntryGridProxyComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingForm || _isRebuildingEntriesPage)
            {
                return;
            }

            var comboBox = sender as ComboBox;
            var entry = comboBox == null ? null : comboBox.DataContext as RdpEntry;
            var option = comboBox == null ? null : comboBox.SelectedItem as ProxyOption;
            if (entry == null || option == null)
            {
                return;
            }

            var currentProfileId = entry.JumpHostProfileId ?? string.Empty;
            var nextProfileId = option.JumpHostProfileId ?? string.Empty;
            var nextDisplayName = option.TransportMode == TransportMode.SshTunnel ? option.DisplayName ?? string.Empty : string.Empty;
            if (entry.TransportMode == option.TransportMode &&
                string.Equals(currentProfileId, nextProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.ProxyDisplayName ?? string.Empty, nextDisplayName, StringComparison.Ordinal))
            {
                return;
            }

            entry.TransportMode = option.TransportMode;
            entry.JumpHostProfileId = option.TransportMode == TransportMode.SshTunnel ? nextProfileId : string.Empty;
            entry.ProxyDisplayName = nextDisplayName;

            if (ReferenceEquals(_editingEntry, entry))
            {
                PopulateForm(entry);
            }

            MarkDirty();
        }

        private void BackToConnectionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetNavigationFilter(NavigationFilter.AllConnections);
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestoreWindow();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestoreWindow();
        }

        private void CloseWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_OnStateChanged(object sender, EventArgs e)
        {
            if (_isHandlingWindowStateChange)
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                _isHandlingWindowStateChange = true;
                try
                {
                    WindowState = WindowState.Normal;
                    MaximizeWindowToWorkArea(true);
                }
                finally
                {
                    _isHandlingWindowStateChange = false;
                }

                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                HideToTray();
                return;
            }

            ApplyWindowBoundsForCurrentState();
            UpdateMaximizeRestoreButtonState();
        }

        private void RowConnectButton_OnClick(object sender, RoutedEventArgs e)
        {
            var entry = GetEntryFromSender(sender);
            if (entry == null)
            {
                return;
            }

            EntriesGrid.SelectedItem = entry;
            ConnectEntry(entry);
        }

        private void RowEditButton_OnClick(object sender, RoutedEventArgs e)
        {
            var entry = GetEntryFromSender(sender);
            if (entry == null)
            {
                return;
            }

            EntriesGrid.SelectedItem = entry;
            EntriesGrid.ScrollIntoView(entry);
            _editingEntry = entry;
            PopulateForm(entry);
            _isCreatingNew = false;
            HostNameTextBox.Focus();
            HostNameTextBox.SelectAll();
        }

        private void RowDeleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            var entry = GetEntryFromSender(sender);
            if (entry == null)
            {
                return;
            }

            DeleteEntry(entry);
        }

        private void CheckSelectedButton_OnClick(object sender, RoutedEventArgs e)
        {
            var selected = EntriesGrid.SelectedItem as RdpEntry ?? _editingEntry;
            if (selected == null)
            {
                MessageBox.Show(this, "Select an entry to check first.", "Health Check", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RunHealthCheck(new[] { selected }, "selected connection");
        }

        private void EntriesGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isRebuildingEntriesPage)
            {
                return;
            }

            var selected = EntriesGrid.SelectedItem as RdpEntry;
            if (selected == null)
            {
                _editingEntry = null;
                UpdateSummary();
                return;
            }

            _editingEntry = selected;
            _isCreatingNew = false;
            PopulateForm(selected);
            UpdateSummary();
        }

        private void PreviousPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_currentEntriesPage <= 1)
            {
                return;
            }

            _currentEntriesPage--;
            RebuildEntriesPage();
            UpdateSummary();
        }

        private void NextPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            var totalPages = GetTotalEntriesPages();
            if (_currentEntriesPage >= totalPages)
            {
                return;
            }

            _currentEntriesPage++;
            RebuildEntriesPage();
            UpdateSummary();
        }

        private void CheckPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_pagedEntries.Count == 0)
            {
                MessageBox.Show(this, "There are no visible connections to check on this page.", "Health Check", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RunHealthCheck(_pagedEntries.ToList(), "current page");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_trayExitRequested && WindowState == WindowState.Minimized)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            if (!ConfirmDiscardIfNeeded())
            {
                e.Cancel = true;
                return;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            SshTunnelManager.ShutdownActiveSessions();
            base.OnClosing(e);
        }

        private void ConnectCurrentEntry()
        {
            try
            {
                var entry = ResolveEntryForConnection();
                if (entry == null)
                {
                    MessageBox.Show(this, "Select an entry or fill the form first.", "Connect", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ConnectEntry(entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "RDP Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Editor_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isPopulatingForm)
            {
                return;
            }

            _editorDirty = true;
        }

        private void SearchTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void CopyValueTextBlock_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var value = element == null ? null : element.Tag as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Clipboard.SetText(value);
            e.Handled = true;
        }

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isPopulatingForm)
            {
                return;
            }

            _editorDirty = true;
        }

        private RdpEntry ApplyEditorToCollection()
        {
            var hostName = (HostNameTextBox.Text ?? string.Empty).Trim();
            var host = (HostTextBox.Text ?? string.Empty).Trim();
            var port = NormalizePort(PortTextBox.Text);
            var user = (UserTextBox.Text ?? string.Empty).Trim();
            var password = PasswordBox.Password ?? string.Empty;
            var transportMode = GetSelectedEntryTransportMode();
            var jumpHostProfileId = GetSelectedJumpHostProfileId();
            var tunnelTargetHostOverride = string.Empty;
            var tunnelTargetPortOverride = string.Empty;
            var groupName = (GroupTextBox.Text ?? string.Empty).Trim();
            var tags = (TagsTextBox.Text ?? string.Empty).Trim();
            var notes = (NotesTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Host cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                HostTextBox.Focus();
                return null;
            }

            if (transportMode == TransportMode.SshTunnel && string.IsNullOrWhiteSpace(jumpHostProfileId))
            {
                MessageBox.Show(this, "Select a proxy server when proxy mode is enabled.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                EntryProxyComboBox.Focus();
                return null;
            }

            var entry = _editingEntry;
            if (_isCreatingNew || entry == null)
            {
                entry = new RdpEntry();
                _entries.Add(entry);
                _editingEntry = entry;
            }

            entry.HostName = hostName;
            entry.Host = host;
            entry.Port = port;
            entry.User = user;
            entry.Password = password;
            entry.TransportMode = transportMode;
            entry.JumpHostProfileId = jumpHostProfileId;
            entry.ProxyDisplayName = transportMode == TransportMode.SshTunnel
                ? GetProxyDisplayName(jumpHostProfileId)
                : string.Empty;
            entry.TunnelTargetHostOverride = tunnelTargetHostOverride;
            entry.TunnelTargetPortOverride = tunnelTargetPortOverride;
            entry.GroupName = groupName;
            entry.Tags = tags;
            entry.Notes = notes;

            _isCreatingNew = false;
            _editorDirty = false;
            MarkDirty();
            RefreshEntriesView();
            NavigateToEntry(entry);

            return entry;
        }

        private void PopulateForm(RdpEntry entry)
        {
            _isPopulatingForm = true;
            HostNameTextBox.Text = entry.HostName;
            HostTextBox.Text = entry.Host;
            PortTextBox.Text = string.IsNullOrWhiteSpace(entry.Port) ? "3389" : entry.Port;
            UserTextBox.Text = entry.User;
            PasswordBox.Password = entry.Password ?? string.Empty;
            EntryProxyComboBox.SelectedItem = FindProxyOption(entry.TransportMode, entry.JumpHostProfileId);
            GroupTextBox.Text = entry.GroupName ?? string.Empty;
            TagsTextBox.Text = entry.Tags ?? string.Empty;
            NotesTextBox.Text = entry.Notes ?? string.Empty;
            _isPopulatingForm = false;
            _editorDirty = false;
            UpdateTransportEditorState();
        }

        private void StartNewEntry()
        {
            _isPopulatingForm = true;
            _editingEntry = null;
            EntriesGrid.UnselectAll();
            _isCreatingNew = true;
            HostNameTextBox.Text = string.Empty;
            HostTextBox.Text = string.Empty;
            PortTextBox.Text = "3389";
            UserTextBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
            EntryProxyComboBox.SelectedItem = FindProxyOption(TransportMode.Direct, string.Empty);
            GroupTextBox.Text = string.Empty;
            TagsTextBox.Text = string.Empty;
            NotesTextBox.Text = string.Empty;
            _isPopulatingForm = false;
            _editorDirty = false;
            HostTextBox.Focus();
            UpdateTransportEditorState();
            UpdateSummary();
        }

        private void ApplySettingsToUi()
        {
            if (_settings == null)
            {
                _settings = new AppSettings();
            }

            var savedToken = SettingsStorage.LoadCloudminiToken(_settings);
            _sessionCloudminiToken = savedToken;

            RememberCloudminiTokenCheckBox.IsChecked = _settings.RememberCloudminiToken;
            CloudminiTokenPasswordBox.Password = savedToken;

            KeepLocalHostNameCheckBox.IsChecked = _settings.KeepLocalHostName;
            OverwritePasswordCheckBox.IsChecked = _settings.OverwritePasswordFromProvider;
            ImportOnlyOnlineCheckBox.IsChecked = _settings.ImportOnlyOnline;

            SettingsKeepLocalHostNameCheckBox.IsChecked = _settings.KeepLocalHostName;
            SettingsOverwritePasswordCheckBox.IsChecked = _settings.OverwritePasswordFromProvider;
            SettingsImportOnlyOnlineCheckBox.IsChecked = _settings.ImportOnlyOnline;

            RebuildEntryProxyOptions();
            UpdateSettingsSummary();
        }

        private void LoadEntriesFromDatabase()
        {
            _entries = SqliteStorage.LoadConnections(_currentFilePath);
            UpdateEntryProxyLabels();
            _currentEntriesPage = 1;
            ConfigureEntriesView();
            _isDirty = false;
            _isCreatingNew = true;
            StartNewEntry();
            UpdateWindowTitle();
            UpdateSettingsSummary();
        }

        private void ImportEntriesFromCsv(string path)
        {
            var importedEntries = CsvStorage.Load(path);
            MetadataStorage.Apply(path, importedEntries);
            _entries = importedEntries;
            _currentCsvExchangePath = path;
            SqliteStorage.SaveConnections(_currentFilePath, _entries);
            UpdateEntryProxyLabels();
            _currentEntriesPage = 1;
            ConfigureEntriesView();
            _isDirty = false;
            _isCreatingNew = true;
            StartNewEntry();
            UpdateWindowTitle();
            UpdateSettingsSummary();
        }

        private void SaveDatabase()
        {
            ApplyPendingFormChangesIfNeeded();
            SqliteStorage.SaveConnections(_currentFilePath, _entries);
            _isDirty = false;
            UpdateWindowTitle();
            UpdateSummary();
        }

        private void ExportEntriesToCsv(string path)
        {
            SaveDatabase();
            CsvStorage.Save(_entries, path);
            MetadataStorage.Save(path, _entries);
            _currentCsvExchangePath = path;
            UpdateSettingsSummary();
        }

        private void ApplyPendingFormChangesIfNeeded()
        {
            if (!HasPendingEditorChanges())
            {
                return;
            }

            ApplyEditorToCollection();
        }

        private bool HasPendingEditorChanges()
        {
            if (!_editorDirty)
            {
                return false;
            }

            if (_isCreatingNew || _editingEntry == null)
            {
                return HasMeaningfulFormInput();
            }

            var editorChanged =
                !string.Equals(_editingEntry.HostName ?? string.Empty, HostNameTextBox.Text ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.Host ?? string.Empty, HostTextBox.Text ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(NormalizePort(_editingEntry.Port), NormalizePort(PortTextBox.Text), StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.User ?? string.Empty, UserTextBox.Text ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.Password ?? string.Empty, PasswordBox.Password ?? string.Empty, StringComparison.Ordinal) ||
                _editingEntry.TransportMode != GetSelectedEntryTransportMode() ||
                !string.Equals(_editingEntry.JumpHostProfileId ?? string.Empty, GetSelectedJumpHostProfileId() ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.GroupName ?? string.Empty, GroupTextBox.Text ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.Tags ?? string.Empty, TagsTextBox.Text ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_editingEntry.Notes ?? string.Empty, NotesTextBox.Text ?? string.Empty, StringComparison.Ordinal);

            return editorChanged;
        }

        private bool ConfirmDiscardIfNeeded()
        {
            if (!_isDirty && !HasPendingEditorChanges())
            {
                return true;
            }

            var result = MessageBox.Show(
                this,
                "You have unsaved changes. Save before continuing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (result == MessageBoxResult.Yes)
            {
                SaveDatabase();
            }

            return true;
        }

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateWindowTitle();
            UpdateSummary();
        }

        private void UpdateWindowTitle()
        {
            var fileName = Path.GetFileName(_currentFilePath);
            var versionTag = GetDisplayVersion();
            Title = _isDirty
                ? string.Format("RDP Manager {0} - {1} *", versionTag, fileName)
                : string.Format("RDP Manager {0} - {1}", versionTag, fileName);
            UpdateWindowTitleText();
        }

        private void UpdateWindowTitleText()
        {
            if (WindowTitleTextBlock != null)
            {
                WindowTitleTextBlock.Text = Title;
            }
        }

        private void ToggleMaximizeRestoreWindow()
        {
            if (ResizeMode == ResizeMode.NoResize)
            {
                return;
            }

            if (_isPseudoMaximized)
            {
                RestoreWindowFromPseudoMaximized();
                return;
            }

            MaximizeWindowToWorkArea(true);
        }

        private void UpdateMaximizeRestoreButtonState()
        {
            if (MaximizeRestoreIconTextBlock == null)
            {
                return;
            }

            MaximizeRestoreIconTextBlock.Text = _isPseudoMaximized
                ? "\uE923"
                : "\uE922";

            if (MaximizeRestoreWindowButton != null)
            {
                MaximizeRestoreWindowButton.ToolTip = _isPseudoMaximized
                    ? "Restore down"
                    : "Maximize";
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmGetMinMaxInfo)
            {
                ApplyMaximizedWindowBounds(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static void ApplyMaximizedWindowBounds(IntPtr hwnd, IntPtr lParam)
        {
            var minMaxInfo = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            var monitorHandle = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitorHandle != IntPtr.Zero)
            {
                var monitorInfo = new MonitorInfo();
                if (GetMonitorInfo(monitorHandle, ref monitorInfo))
                {
                    var workArea = monitorInfo.WorkArea;
                    var monitorArea = monitorInfo.Monitor;

                    minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
                    minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
                    minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
                    minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
                }
            }

            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        private void ApplyWindowBoundsForCurrentState()
        {
            if (WindowFrameBorder == null)
            {
                return;
            }

            WindowFrameBorder.Margin = new Thickness(0);
        }

        private void MaximizeWindowToWorkArea(bool saveRestoreBounds)
        {
            if (saveRestoreBounds && !_isPseudoMaximized)
            {
                _restoreBounds = new System.Windows.Rect(Left, Top, Width, Height);
            }

            var handle = new WindowInteropHelper(this).Handle;
            var screen = handle == IntPtr.Zero
                ? Forms.Screen.PrimaryScreen
                : Forms.Screen.FromHandle(handle);
            var workingArea = screen == null ? Drawing.Rectangle.Empty : screen.WorkingArea;

            if (!workingArea.IsEmpty)
            {
                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;
            }

            _isPseudoMaximized = true;
            ApplyWindowBoundsForCurrentState();
            UpdateMaximizeRestoreButtonState();
        }

        private void RestoreWindowFromPseudoMaximized()
        {
            _isPseudoMaximized = false;

            if (!_restoreBounds.IsEmpty)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }

            ApplyWindowBoundsForCurrentState();
            UpdateMaximizeRestoreButtonState();
        }

        private void InitializeTrayIcon()
        {
            if (_trayIcon != null)
            {
                return;
            }

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { RestoreFromTray(); });
            menu.Items.Add("Exit", null, delegate { ExitFromTray(); });

            var icon = Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) ?? Drawing.SystemIcons.Application;
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "RDP Manager",
                Icon = icon,
                Visible = false,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += delegate { RestoreFromTray(); };
        }

        private void HideToTray()
        {
            if (_trayIcon == null)
            {
                InitializeTrayIcon();
            }

            _restoreToPseudoMaximized = _isPseudoMaximized;
            ShowInTaskbar = false;
            Hide();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = true;
                if (!_trayHintShown)
                {
                    _trayIcon.BalloonTipTitle = "RDP Manager";
                    _trayIcon.BalloonTipText = "The app is running in the tray. Double-click the tray icon to restore it.";
                    _trayIcon.ShowBalloonTip(2000);
                    _trayHintShown = true;
                }
            }
        }

        private void RestoreFromTray()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
            }

            ShowInTaskbar = true;
            Show();
            Activate();

            _isHandlingWindowStateChange = true;
            try
            {
                WindowState = WindowState.Normal;
            }
            finally
            {
                _isHandlingWindowStateChange = false;
            }

            if (_restoreToPseudoMaximized)
            {
                MaximizeWindowToWorkArea(false);
            }
            else
            {
                _isPseudoMaximized = false;
                ApplyWindowBoundsForCurrentState();
                UpdateMaximizeRestoreButtonState();
            }
        }

        private void ExitFromTray()
        {
            _trayExitRequested = true;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
            }

            Close();
        }

        private void LoadJumpHostProfiles(string selectedProfileId = null)
        {
            var profiles = JumpHostProfileStorage.Load();
            _jumpHostProfiles.Clear();
            foreach (var profile in profiles)
            {
                _jumpHostProfiles.Add(profile);
            }

            if (!string.IsNullOrWhiteSpace(selectedProfileId))
            {
                var selectedProfile = FindJumpHostProfile(selectedProfileId);
                if (JumpHostProfilesListBox != null)
                {
                    JumpHostProfilesListBox.SelectedItem = selectedProfile;
                }

                if (selectedProfile != null)
                {
                    PopulateJumpHostEditor(selectedProfile);
                }
            }
            else if (_editingJumpHostProfile == null)
            {
                StartNewJumpHostProfile();
            }

            RebuildEntryProxyOptions();
            UpdateEntryProxyLabels();
            UpdateTransportEditorState();
            UpdateJumpHostEditorState();
            UpdateSettingsSummary();
        }

        private void PopulateJumpHostEditor(JumpHostProfile profile)
        {
            _editingJumpHostProfile = profile;
            JumpHostProfileNameTextBox.Text = profile == null ? string.Empty : profile.Name ?? string.Empty;
            JumpHostHostTextBox.Text = profile == null ? string.Empty : profile.Host ?? string.Empty;
            JumpHostPortTextBox.Text = profile == null ? "22" : NormalizePositiveNumber(profile.Port, 22).ToString();
            JumpHostUserTextBox.Text = profile == null ? string.Empty : profile.User ?? string.Empty;
            SetComboSelectedTag(JumpHostAuthModeComboBox, "Password");
            JumpHostHostKeyFingerprintTextBox.Text = profile == null ? string.Empty : profile.HostKeyFingerprint ?? string.Empty;
            JumpHostConnectTimeoutTextBox.Text = profile == null ? "10" : NormalizePositiveNumber(profile.ConnectTimeoutSeconds, 10).ToString();
            JumpHostKeepAliveTextBox.Text = profile == null ? "30" : NormalizePositiveNumber(profile.KeepAliveSeconds, 30).ToString();
            if (JumpHostPasswordBox != null)
            {
                JumpHostPasswordBox.Password = string.Empty;
            }
            SetJumpHostTestStatus("SSH test not run yet.", new SolidColorBrush(Color.FromRgb(105, 120, 142)));
            UpdateJumpHostEditorState();
        }

        private void StartNewJumpHostProfile()
        {
            if (JumpHostProfilesListBox != null)
            {
                JumpHostProfilesListBox.SelectedItem = null;
            }

            _editingJumpHostProfile = null;
            JumpHostProfileNameTextBox.Text = string.Empty;
            JumpHostHostTextBox.Text = string.Empty;
            JumpHostPortTextBox.Text = "22";
            JumpHostUserTextBox.Text = string.Empty;
            SetComboSelectedTag(JumpHostAuthModeComboBox, "Password");
            JumpHostHostKeyFingerprintTextBox.Text = string.Empty;
            JumpHostConnectTimeoutTextBox.Text = "10";
            JumpHostKeepAliveTextBox.Text = "30";
            if (JumpHostPasswordBox != null)
            {
                JumpHostPasswordBox.Password = string.Empty;
            }
            SetJumpHostTestStatus("SSH test not run yet.", new SolidColorBrush(Color.FromRgb(105, 120, 142)));
            UpdateJumpHostEditorState();
        }

        private JumpHostProfile BuildJumpHostProfileFromEditor()
        {
            var host = (JumpHostHostTextBox.Text ?? string.Empty).Trim();
            var user = (JumpHostUserTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Proxy server host cannot be empty.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Warning);
                JumpHostHostTextBox.Focus();
                return null;
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                MessageBox.Show(this, "Proxy server user cannot be empty.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Warning);
                JumpHostUserTextBox.Focus();
                return null;
            }

            var profile = _editingJumpHostProfile == null
                ? new JumpHostProfile()
                : new JumpHostProfile
                {
                    Id = _editingJumpHostProfile.Id,
                    SecretRefId = _editingJumpHostProfile.SecretRefId,
                    PassphraseSecretRefId = _editingJumpHostProfile.PassphraseSecretRefId,
                    ImportedKeyLabel = _editingJumpHostProfile.ImportedKeyLabel
                };

            profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id;
            profile.Name = (JumpHostProfileNameTextBox.Text ?? string.Empty).Trim();
            profile.Host = host;
            profile.Port = NormalizePositiveNumber(JumpHostPortTextBox.Text, 22);
            profile.User = user;
            profile.AuthMode = JumpHostAuthMode.Password;
            profile.UseAgent = false;
            profile.StrictHostKeyCheckingMode = "Ask";
            profile.HostKeyFingerprint = (JumpHostHostKeyFingerprintTextBox.Text ?? string.Empty).Trim();
            profile.ConnectTimeoutSeconds = NormalizePositiveNumber(JumpHostConnectTimeoutTextBox.Text, 10);
            profile.KeepAliveSeconds = NormalizePositiveNumber(JumpHostKeepAliveTextBox.Text, 30);
            profile.RuntimePassword = GetJumpHostPasswordInput();
            profile.ImportedKeyLabel = string.Empty;

            return profile;
        }

        private void SaveJumpHostProfileFromCurrentSelection()
        {
            if (_editingJumpHostProfile == null || string.IsNullOrWhiteSpace(_editingJumpHostProfile.Id))
            {
                return;
            }

            var profiles = _jumpHostProfiles.ToList();
            var existingIndex = profiles.FindIndex(profile => string.Equals(profile.Id, _editingJumpHostProfile.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                profiles[existingIndex] = _editingJumpHostProfile;
            }
            else
            {
                profiles.Add(_editingJumpHostProfile);
            }

            JumpHostProfileStorage.Save(profiles);
            LoadJumpHostProfiles(_editingJumpHostProfile.Id);
            UpdateSettingsSummary();
        }

        private void UpdateJumpHostEditorState()
        {
            var hasSelectedProfile = _editingJumpHostProfile != null && !string.IsNullOrWhiteSpace(_editingJumpHostProfile.Id);
            var hasStoredSecret = hasSelectedProfile &&
                                  !string.IsNullOrWhiteSpace(_editingJumpHostProfile.SecretRefId) &&
                                  SecretVault.HasSecret(_editingJumpHostProfile.SecretRefId);
            var hasStoredPassword = hasStoredSecret && SecretVault.GetSecretKind(_editingJumpHostProfile.SecretRefId) == SecretKind.SshPassword;

            if (JumpHostKeyStatusTextBlock != null)
            {
                JumpHostKeyStatusTextBlock.Text = hasStoredPassword
                    ? "Password stored securely for this proxy server."
                    : hasSelectedProfile
                        ? "No SSH password stored for this profile."
                        : "Save the profile first, then enter an SSH password.";
            }

            if (JumpHostPasswordPanel != null)
            {
                JumpHostPasswordPanel.Visibility = Visibility.Visible;
            }

            if (JumpHostPasswordHintTextBlock != null)
            {
                JumpHostPasswordHintTextBlock.Text = hasStoredPassword
                    ? "Leave blank to keep the stored password, or enter a new password to rotate it."
                    : "Enter the SSH password, then save the profile.";
            }

            if (ImportJumpHostKeyButton != null)
            {
                ImportJumpHostKeyButton.IsEnabled = false;
                ImportJumpHostKeyButton.Visibility = Visibility.Collapsed;
            }

            if (ClearJumpHostKeyButton != null)
            {
                ClearJumpHostKeyButton.Content = "Clear password";
                ClearJumpHostKeyButton.IsEnabled = hasStoredPassword;
            }

            if (TestJumpHostProfileButton != null)
            {
                TestJumpHostProfileButton.IsEnabled = true;
            }
        }

        private void SetJumpHostTestStatus(string message, Brush brush)
        {
            if (JumpHostTestStatusTextBlock == null)
            {
                return;
            }

            JumpHostTestStatusTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "SSH test not run yet." : message;
            JumpHostTestStatusTextBlock.Foreground = brush ?? new SolidColorBrush(Color.FromRgb(105, 120, 142));
        }

        private void UpdateTransportEditorState()
        {
            if (EntryProxyComboBox != null)
            {
                EntryProxyComboBox.IsEnabled = true;
            }
        }

        private JumpHostProfile FindJumpHostProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return null;
            }

            return _jumpHostProfiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private TransportMode GetSelectedEntryTransportMode()
        {
            var selectedOption = EntryProxyComboBox == null ? null : EntryProxyComboBox.SelectedItem as ProxyOption;
            return selectedOption == null ? TransportMode.Direct : selectedOption.TransportMode;
        }

        private string GetSelectedJumpHostProfileId()
        {
            var selectedOption = EntryProxyComboBox == null ? null : EntryProxyComboBox.SelectedItem as ProxyOption;
            return selectedOption == null ? string.Empty : selectedOption.JumpHostProfileId ?? string.Empty;
        }

        private void RebuildEntryProxyOptions()
        {
            var selectedTransportMode = GetSelectedEntryTransportMode();
            var selectedProfileId = GetSelectedJumpHostProfileId();

            _entryProxyOptions.Clear();
            _entryProxyOptions.Add(new ProxyOption
            {
                DisplayName = "Direct",
                TransportMode = TransportMode.Direct,
                JumpHostProfileId = string.Empty
            });

            foreach (var profile in _jumpHostProfiles)
            {
                _entryProxyOptions.Add(new ProxyOption
                {
                    DisplayName = profile.DisplayName,
                    TransportMode = TransportMode.SshTunnel,
                    JumpHostProfileId = profile.Id ?? string.Empty
                });
            }

            if (EntryProxyComboBox != null)
            {
                EntryProxyComboBox.SelectedItem = FindProxyOption(selectedTransportMode, selectedProfileId) ??
                                                  FindProxyOption(TransportMode.Direct, string.Empty);
            }
        }

        private ProxyOption FindProxyOption(TransportMode transportMode, string jumpHostProfileId)
        {
            if (transportMode != TransportMode.SshTunnel)
            {
                return _entryProxyOptions.FirstOrDefault(option => option.TransportMode == TransportMode.Direct);
            }

            return _entryProxyOptions.FirstOrDefault(option =>
                option.TransportMode == TransportMode.SshTunnel &&
                string.Equals(option.JumpHostProfileId ?? string.Empty, jumpHostProfileId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        private string GetProxyDisplayName(string jumpHostProfileId)
        {
            var profile = FindJumpHostProfile(jumpHostProfileId);
            return profile == null ? string.Empty : profile.DisplayName;
        }

        private void UpdateEntryProxyLabels()
        {
            foreach (var entry in _entries)
            {
                entry.ProxyDisplayName = entry.TransportMode == TransportMode.SshTunnel
                    ? GetProxyDisplayName(entry.JumpHostProfileId)
                    : string.Empty;
            }
        }

        private JumpHostAuthMode GetSelectedJumpHostAuthMode()
        {
            return JumpHostAuthMode.Password;
        }

        private string GetJumpHostPasswordInput()
        {
            return JumpHostPasswordBox == null ? string.Empty : (JumpHostPasswordBox.Password ?? string.Empty);
        }

        private bool TryPersistJumpHostSecret(JumpHostProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (profile.AuthMode == JumpHostAuthMode.Agent)
            {
                profile.RuntimePassword = string.Empty;
                return true;
            }

            var secretKind = SecretVault.GetSecretKind(profile.SecretRefId);
            if (profile.AuthMode == JumpHostAuthMode.Password)
            {
                var password = profile.RuntimePassword ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(password))
                {
                    profile.SecretRefId = SecretVault.SaveSecret(profile.SecretRefId, SecretKind.SshPassword, password);
                    profile.RuntimePassword = string.Empty;
                    if (JumpHostPasswordBox != null)
                    {
                        JumpHostPasswordBox.Password = string.Empty;
                    }

                    return true;
                }

                if (!string.IsNullOrWhiteSpace(profile.SecretRefId) &&
                    SecretVault.HasSecret(profile.SecretRefId) &&
                    secretKind == SecretKind.SshPassword)
                {
                    return true;
                }

                MessageBox.Show(this, "Enter an SSH password before saving this profile.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (JumpHostPasswordBox != null)
                {
                    JumpHostPasswordBox.Focus();
                }

                return false;
            }

            if (!string.IsNullOrWhiteSpace(profile.SecretRefId) &&
                SecretVault.HasSecret(profile.SecretRefId) &&
                secretKind == SecretKind.SshPrivateKey)
            {
                return true;
            }

            MessageBox.Show(this, "Import a private key before saving this profile.", "Proxy Servers", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (ImportJumpHostKeyButton != null)
            {
                ImportJumpHostKeyButton.Focus();
            }

            return false;
        }

        private static string NormalizePort(string rawPort)
        {
            int port;
            if (int.TryParse((rawPort ?? string.Empty).Trim(), out port) && port > 0 && port <= 65535)
            {
                return port.ToString();
            }

            return "3389";
        }

        private static int NormalizePositiveNumber(object rawValue, int defaultValue)
        {
            int parsed;
            return int.TryParse(Convert.ToString(rawValue), out parsed) && parsed > 0
                ? parsed
                : defaultValue;
        }

        private static string GetInitialDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(directory) ? AppDomain.CurrentDomain.BaseDirectory : directory;
        }

        private bool HasMeaningfulFormInput()
        {
            return
                !string.IsNullOrWhiteSpace(HostNameTextBox.Text) ||
                !string.IsNullOrWhiteSpace(HostTextBox.Text) ||
                !string.IsNullOrWhiteSpace(UserTextBox.Text) ||
                !string.IsNullOrWhiteSpace(PasswordBox.Password) ||
                GetSelectedEntryTransportMode() != TransportMode.Direct ||
                !string.IsNullOrWhiteSpace(GetSelectedJumpHostProfileId()) ||
                !string.IsNullOrWhiteSpace(GroupTextBox.Text) ||
                !string.IsNullOrWhiteSpace(TagsTextBox.Text) ||
                !string.IsNullOrWhiteSpace(NotesTextBox.Text) ||
                !string.Equals(NormalizePort(PortTextBox.Text), "3389", StringComparison.Ordinal);
        }

        private RdpEntry ResolveEntryForConnection()
        {
            var selected = EntriesGrid.SelectedItem as RdpEntry;
            if (selected != null)
            {
                return selected;
            }

            if (_editorDirty || _isCreatingNew)
            {
                return HasMeaningfulFormInput() ? ApplyEditorToCollection() : null;
            }

            return _editingEntry != null && _pagedEntries.Contains(_editingEntry)
                ? _editingEntry
                : null;
        }

        private static string GetEntryDisplayName(RdpEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.HostName))
            {
                return entry.HostName;
            }

            return entry.Host;
        }

        private void ConfigureEntriesView()
        {
            RefreshEntriesView();
        }

        private void RefreshEntriesView()
        {
            UpdateConnectionsGroupOptions();
            RebuildEntriesPage();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            UpdateNavigationVisuals();
            UpdateViewState();
            UpdateFilterSelectors();
            UpdateTransportEditorState();
            UpdateJumpHostEditorState();

            if (_currentAppSection == AppSection.Connections)
            {
                UpdateFavoriteButtonState();
                UpdateEmptyState();
            }

            UpdateCloudminiEmptyState();
            UpdateSettingsSummary();
        }

        private void ApplyWindowFrameTheme()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            SetDwmAttribute(handle, DwmUseImmersiveDarkMode, 0);
            SetDwmAttribute(handle, DwmUseImmersiveDarkModeLegacy, 0);
            SetDwmAttribute(handle, DwmCaptionColor, ToColorRef(0xFB, 0xFB, 0xFD));
            SetDwmAttribute(handle, DwmBorderColor, ToColorRef(0xD9, 0xE1, 0xEC));
            SetDwmAttribute(handle, DwmTextColor, ToColorRef(0x20, 0x27, 0x33));
        }

        private static void SetDwmAttribute(IntPtr handle, int attribute, int value)
        {
            try
            {
                DwmSetWindowAttribute(handle, attribute, ref value, Marshal.SizeOf(typeof(int)));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static int ToColorRef(byte red, byte green, byte blue)
        {
            return red | (green << 8) | (blue << 16);
        }

        private void DeleteEntry(RdpEntry entry)
        {
            var result = MessageBox.Show(
                this,
                string.Format("Delete RDP entry for {0}?", GetEntryDisplayName(entry)),
                "Delete Entry",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _entries.Remove(entry);
            if (ReferenceEquals(_editingEntry, entry))
            {
                _editingEntry = null;
            }

            MarkDirty();
            StartNewEntry();
            RefreshEntriesView();
        }

        private void ConnectEntry(RdpEntry entry)
        {
            entry.LastConnectedUtc = DateTime.UtcNow;
            MetadataStorage.Save(_currentFilePath, _entries);
            RefreshEntriesView();

            if (entry.TransportMode == TransportMode.SshTunnel)
            {
                var profile = FindJumpHostProfile(entry.JumpHostProfileId);
                if (profile == null)
                {
                    throw new InvalidOperationException("The selected proxy server profile could not be found.");
                }

                SshTunnelManager.Launch(entry, profile);
                return;
            }

            RdpLauncher.Launch(entry);
        }

        private static RdpEntry GetEntryFromSender(object sender)
        {
            var button = sender as Button;
            return button == null ? null : button.Tag as RdpEntry;
        }

        private void SetAppSection(AppSection section)
        {
            _currentAppSection = section;
            UpdateSummary();
        }

        private void SetNavigationFilter(NavigationFilter filter)
        {
            _currentAppSection = AppSection.Connections;
            _currentNavigationFilter = filter;
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void UpdateViewState()
        {
            if (ConnectionsView != null)
            {
                ConnectionsView.Visibility = _currentAppSection == AppSection.Connections ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CloudminiView != null)
            {
                CloudminiView.Visibility = _currentAppSection == AppSection.CloudminiSync ? Visibility.Visible : Visibility.Collapsed;
            }

            if (SettingsView != null)
            {
                SettingsView.Visibility = _currentAppSection == AppSection.Settings ? Visibility.Visible : Visibility.Collapsed;
            }

            if (SearchTextBox != null)
            {
                SearchTextBox.Visibility = Visibility.Visible;
                SearchTextBox.IsEnabled = _currentAppSection == AppSection.Connections;
                SearchTextBox.Opacity = _currentAppSection == AppSection.Connections ? 1.0 : 0.55;
            }

            if (ConnectionsActionsPanel != null)
            {
                ConnectionsActionsPanel.Visibility = Visibility.Visible;
                ConnectionsActionsPanel.IsEnabled = _currentAppSection == AppSection.Connections;
                ConnectionsActionsPanel.Opacity = _currentAppSection == AppSection.Connections ? 1.0 : 0.55;
            }
        }

        private void UpdateNavigationVisuals()
        {
            SetNavigationVisual(AllConnectionsNavButton, AllConnectionsNavIcon, AllConnectionsNavText, _currentAppSection == AppSection.Connections && _currentNavigationFilter == NavigationFilter.AllConnections);
            SetNavigationVisual(FavoritesNavButton, FavoritesNavIcon, FavoritesNavText, _currentAppSection == AppSection.Connections && _currentNavigationFilter == NavigationFilter.Favorites);
            SetNavigationVisual(RecentNavButton, RecentNavIcon, RecentNavText, _currentAppSection == AppSection.Connections && _currentNavigationFilter == NavigationFilter.Recent);
            SetNavigationVisual(CloudminiSyncNavButton, CloudminiSyncNavIcon, CloudminiSyncNavText, _currentAppSection == AppSection.CloudminiSync);
            SetNavigationVisual(SettingsNavButton, SettingsNavIcon, SettingsNavText, _currentAppSection == AppSection.Settings);
        }

        private void SetNavigationVisual(Control control, TextBlock icon, TextBlock label, bool isActive)
        {
            var activeBackground = FindResource("AccentSoftBrush") as Brush;
            var inactiveBackground = FindResource("SurfaceBrush") as Brush;
            var activeBorder = FindResource("AccentSoftBorderBrush") as Brush;
            var inactiveBorder = FindResource("CardBorderBrush") as Brush;
            var activeForeground = FindResource("TextPrimaryBrush") as Brush;
            var inactiveForeground = FindResource("TextPrimaryBrush") as Brush;

            if (control != null)
            {
                control.Background = isActive ? activeBackground : inactiveBackground;
                control.BorderBrush = isActive ? activeBorder : inactiveBorder;
            }

            if (icon != null)
            {
                icon.Foreground = isActive ? activeForeground : inactiveForeground;
            }

            if (label != null)
            {
                label.Foreground = isActive ? activeForeground : inactiveForeground;
            }
        }

        private void UpdateEmptyState()
        {
            if (EmptyStateTextBlock == null)
            {
                return;
            }

            if (_filteredEntriesCount > 0)
            {
                EmptyStateTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyStateTextBlock.Text = ConnectionListService.GetEmptyStateMessage(
                _currentNavigationFilter,
                _currentConnectionsPlatformFilter,
                _currentConnectionsStatusFilter,
                _currentConnectionsGroupFilter);
            EmptyStateTextBlock.Visibility = Visibility.Visible;
        }

        private void UpdateCloudminiEmptyState()
        {
            if (CloudminiEmptyStateTextBlock == null)
            {
                return;
            }

            if (_currentAppSection != AppSection.CloudminiSync)
            {
                CloudminiEmptyStateTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            if (_cloudminiPreviewItems.Count == 0)
            {
                CloudminiEmptyStateTextBlock.Text = CloudminiFilterService.GetEmptyStateMessage(
                    _currentCloudminiPlatformFilter,
                    _currentCloudminiStatusFilter);
                CloudminiEmptyStateTextBlock.Visibility = Visibility.Visible;
                return;
            }

            CloudminiEmptyStateTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SyncCloudminiOptionStateToSettings()
        {
            if (_settings == null)
            {
                _settings = new AppSettings();
            }

            _settings.KeepLocalHostName = KeepLocalHostNameCheckBox.IsChecked == true;
            _settings.OverwritePasswordFromProvider = OverwritePasswordCheckBox.IsChecked == true;
            _settings.ImportOnlyOnline = ImportOnlyOnlineCheckBox.IsChecked == true;

            if (SettingsKeepLocalHostNameCheckBox != null)
            {
                SettingsKeepLocalHostNameCheckBox.IsChecked = _settings.KeepLocalHostName;
            }

            if (SettingsOverwritePasswordCheckBox != null)
            {
                SettingsOverwritePasswordCheckBox.IsChecked = _settings.OverwritePasswordFromProvider;
            }

            if (SettingsImportOnlyOnlineCheckBox != null)
            {
                SettingsImportOnlyOnlineCheckBox.IsChecked = _settings.ImportOnlyOnline;
            }
        }

        private CloudminiSyncOptions BuildCurrentSyncOptions()
        {
            return new CloudminiSyncOptions
            {
                KeepLocalHostName = KeepLocalHostNameCheckBox.IsChecked == true,
                OverwritePasswordFromProvider = OverwritePasswordCheckBox.IsChecked == true,
                ImportOnlyOnline = ImportOnlyOnlineCheckBox.IsChecked == true
            };
        }

        private void RebuildCloudminiPreview()
        {
            if (_cloudminiRemoteItems.Count == 0)
            {
                _cloudminiPreviewItems.Clear();
                UpdateCloudminiEmptyState();
                return;
            }

            var filteredRemoteItems = CloudminiFilterService.Filter(
                _cloudminiRemoteItems,
                _currentCloudminiPlatformFilter,
                _currentCloudminiStatusFilter);
            var preview = CloudminiSyncService.BuildPreview(_entries, filteredRemoteItems, BuildCurrentSyncOptions());
            _cloudminiPreviewItems.Clear();
            foreach (var item in preview)
            {
                _cloudminiPreviewItems.Add(item);
            }

            UpdateCloudminiEmptyState();
        }

        private void UpdateFilterSelectors()
        {
            _isUpdatingFilterSelectors = true;
            try
            {
                SetComboSelectedTag(ConnectionsPlatformFilterComboBox, _currentConnectionsPlatformFilter.ToString());
                SetComboSelectedTag(ConnectionsStatusFilterComboBox, _currentConnectionsStatusFilter.ToString());
                SetComboSelectedTag(CloudminiPlatformFilterComboBox, _currentCloudminiPlatformFilter.ToString());
                SetComboSelectedTag(CloudminiStatusFilterComboBox, _currentCloudminiStatusFilter.ToString());
            }
            finally
            {
                _isUpdatingFilterSelectors = false;
            }
        }

        private static string GetSelectedComboTag(ComboBox comboBox)
        {
            var selectedItem = comboBox == null ? null : comboBox.SelectedItem as ComboBoxItem;
            return selectedItem == null ? string.Empty : (selectedItem.Tag as string ?? string.Empty);
        }

        private static void SetComboSelectedTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (var item in comboBox.Items)
            {
                var comboBoxItem = item as ComboBoxItem;
                if (comboBoxItem != null && string.Equals(comboBoxItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static PlatformFilter ParsePlatformFilter(string value)
        {
            if (string.Equals(value, "Windows", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformFilter.Windows;
            }

            if (string.Equals(value, "Linux", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformFilter.Linux;
            }

            return PlatformFilter.All;
        }

        private static StatusFilter ParseStatusFilter(string value)
        {
            if (string.Equals(value, "Online", StringComparison.OrdinalIgnoreCase))
            {
                return StatusFilter.Online;
            }

            if (string.Equals(value, "Offline", StringComparison.OrdinalIgnoreCase))
            {
                return StatusFilter.Offline;
            }

            if (string.Equals(value, "Other", StringComparison.OrdinalIgnoreCase))
            {
                return StatusFilter.Other;
            }

            return StatusFilter.All;
        }

        private void UpdateConnectionsGroupOptions()
        {
            if (ConnectionsGroupFilterComboBox == null)
            {
                return;
            }

            var groups = _entries
                .Select(entry => entry == null ? string.Empty : (entry.GroupName ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _isUpdatingGroupFilter = true;
            try
            {
                _connectionGroupOptions.Clear();
                _connectionGroupOptions.Add(AllGroupsFilterOption);
                foreach (var group in groups)
                {
                    _connectionGroupOptions.Add(group);
                }

                if (!string.IsNullOrWhiteSpace(_currentConnectionsGroupFilter) &&
                    !groups.Any(group => string.Equals(group, _currentConnectionsGroupFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    _currentConnectionsGroupFilter = string.Empty;
                }

                ConnectionsGroupFilterComboBox.SelectedItem = string.IsNullOrWhiteSpace(_currentConnectionsGroupFilter)
                    ? AllGroupsFilterOption
                    : _connectionGroupOptions.FirstOrDefault(option =>
                        string.Equals(option, _currentConnectionsGroupFilter, StringComparison.OrdinalIgnoreCase)) ?? AllGroupsFilterOption;
            }
            finally
            {
                _isUpdatingGroupFilter = false;
            }
        }

        private void RunHealthCheck(IList<RdpEntry> entries, string scopeLabel)
        {
            var targets = (entries ?? new List<RdpEntry>())
                .Where(entry => entry != null)
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var statusSummary = new List<string>();
                foreach (var entry in targets)
                {
                    statusSummary.Add(ConnectionHealthService.CheckAndApply(entry, HealthCheckTimeoutMilliseconds));
                }

                MetadataStorage.Save(_currentFilePath, _entries);
                RefreshEntriesView();

                var summary = ConnectionHealthService.SummarizeStatuses(statusSummary);
                var summaryText = summary.Count == 0
                    ? "No results."
                    : string.Join(", ", summary.Select(item => string.Format("{0}: {1}", item.Key, item.Value)));

                MessageBox.Show(
                    this,
                    string.Format("Health check completed for {0}.\n{1}", scopeLabel, summaryText),
                    "Health Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void RebuildEntriesPage()
        {
            var pageResult = ConnectionListService.BuildPage(
                _entries,
                SearchTextBox == null ? string.Empty : SearchTextBox.Text,
                _currentNavigationFilter,
                _currentConnectionsPlatformFilter,
                _currentConnectionsStatusFilter,
                _currentConnectionsGroupFilter,
                _currentEntriesPage,
                EntriesPageSize);

            _filteredEntriesCount = pageResult.TotalCount;
            _currentEntriesPage = pageResult.CurrentPage;

            var currentSelection = EntriesGrid == null ? null : EntriesGrid.SelectedItem as RdpEntry;
            var pageEntries = pageResult.Items;

            _isRebuildingEntriesPage = true;
            try
            {
                _pagedEntries.Clear();
                foreach (var entry in pageEntries)
                {
                    _pagedEntries.Add(entry);
                }

                if (EntriesGrid != null)
                {
                    if (currentSelection != null && pageEntries.Contains(currentSelection))
                    {
                        EntriesGrid.SelectedItem = currentSelection;
                    }
                    else if (_editingEntry != null && !_isCreatingNew && pageEntries.Contains(_editingEntry))
                    {
                        EntriesGrid.SelectedItem = _editingEntry;
                    }
                    else
                    {
                        EntriesGrid.SelectedItem = null;
                    }
                }
            }
            finally
            {
                _isRebuildingEntriesPage = false;
            }

            UpdateEntriesPaginationControls();
        }

        private void UpdateEntriesPaginationControls()
        {
            if (PageInfoTextBlock == null || PageCountSummaryTextBlock == null || PreviousPageButton == null || NextPageButton == null)
            {
                return;
            }

            var totalPages = GetTotalEntriesPages();
            PageInfoTextBlock.Text = string.Format("Page {0} / {1}", totalPages == 0 ? 1 : _currentEntriesPage, totalPages);
            PageCountSummaryTextBlock.Text = _filteredEntriesCount == 1
                ? "1 item"
                : string.Format("{0} items", _filteredEntriesCount);

            PreviousPageButton.IsEnabled = totalPages > 1 && _currentEntriesPage > 1;
            NextPageButton.IsEnabled = totalPages > 1 && _currentEntriesPage < totalPages;
        }

        private int GetTotalEntriesPages()
        {
            return Math.Max(1, (int)Math.Ceiling(_filteredEntriesCount / (double)EntriesPageSize));
        }

        private void NavigateToEntry(RdpEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var filteredEntries = ConnectionListService.BuildFilteredEntries(
                _entries,
                SearchTextBox == null ? string.Empty : SearchTextBox.Text,
                _currentNavigationFilter,
                _currentConnectionsPlatformFilter,
                _currentConnectionsStatusFilter,
                _currentConnectionsGroupFilter);
            var index = filteredEntries.IndexOf(entry);
            if (index < 0)
            {
                return;
            }

            _currentEntriesPage = (index / EntriesPageSize) + 1;
            RebuildEntriesPage();

            if (EntriesGrid != null && _pagedEntries.Contains(entry))
            {
                EntriesGrid.SelectedItem = entry;
                EntriesGrid.ScrollIntoView(entry);
            }
        }

        private void ApplyCloudminiSync(bool onlySelected)
        {
            if (_cloudminiPreviewItems.Count == 0)
            {
                MessageBox.Show(this, "Fetch Cloudmini VPS first.", "Cloudmini Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedItems = _cloudminiPreviewItems.Where(item => item.IsSelected).ToList();
            if (onlySelected && selectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select at least one VPS to sync.", "Cloudmini Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string backupPath = null;
                if (selectedItems.Count > 1)
                {
                    backupPath = BackupStorage.CreatePreSyncBackup(_currentFilePath, _entries);
                }

                var result = CloudminiSyncService.ApplySync(_entries, _cloudminiPreviewItems, BuildCurrentSyncOptions());

                _settings.LastCloudminiSyncUtc = DateTime.UtcNow;
                _settings.LastCloudminiSyncSummary = string.Format(
                    "Created {0}, updated {1}, skipped {2}, conflicts {3}",
                    result.CreatedCount,
                    result.UpdatedCount,
                    result.SkippedCount,
                    result.ConflictCount);
                SettingsStorage.SaveCloudminiToken(_settings, CloudminiTokenPasswordBox.Password, RememberCloudminiTokenCheckBox.IsChecked == true);
                SettingsStorage.Save(_settings);

                MarkDirty();
                RefreshEntriesView();
                RebuildCloudminiPreview();
                UpdateSettingsSummary();
                CloudminiStatusTextBlock.Text = string.IsNullOrWhiteSpace(backupPath)
                    ? _settings.LastCloudminiSyncSummary
                    : _settings.LastCloudminiSyncSummary + " | Backup created";

                MessageBox.Show(
                    this,
                    string.Format(
                        "Cloudmini sync completed.\nCreated: {0}\nUpdated: {1}\nSkipped: {2}\nConflicts: {3}{4}",
                        result.CreatedCount,
                        result.UpdatedCount,
                        result.SkippedCount,
                        result.ConflictCount,
                        string.IsNullOrWhiteSpace(backupPath)
                            ? string.Empty
                            : "\nBackup: " + backupPath),
                    "Cloudmini Sync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private string GetCloudminiToken()
        {
            var token = (CloudminiTokenPasswordBox.Password ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                token = (_sessionCloudminiToken ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Cloudmini token cannot be empty.");
            }

            return token;
        }

        private void UpdateSettingsSummary()
        {
            if (CurrentCsvPathTextBlock == null || SettingsPathTextBlock == null || SavedTokenStatusTextBlock == null || CloudminiLastSyncTextBlock == null)
            {
                return;
            }

            CurrentCsvPathTextBlock.Text = "Database: " + (_currentFilePath ?? "-");
            SettingsPathTextBlock.Text = "Settings: " + SettingsStorage.GetSettingsPath();
            if (JumpHostsPathTextBlock != null)
            {
                JumpHostsPathTextBlock.Text = "Proxy servers: " + JumpHostProfileStorage.GetProfilesPath() + " (table)";
            }

            if (SecretVaultPathTextBlock != null)
            {
                SecretVaultPathTextBlock.Text = "Secrets: " + SecretVault.GetSecretsPath();
            }

            SavedTokenStatusTextBlock.Text = _settings != null && _settings.RememberCloudminiToken && !string.IsNullOrWhiteSpace(_settings.EncryptedCloudminiToken)
                ? "Saved token: stored for current Windows user"
                : "Saved token: not stored";
            if (JumpHostCountTextBlock != null)
            {
                JumpHostCountTextBlock.Text = string.Format("Proxy server profiles: {0}", _jumpHostProfiles.Count);
            }

            if (AppVersionTextBlock != null)
            {
                AppVersionTextBlock.Text = "Version: " + GetDisplayVersion();
            }

            if (_settings != null && _settings.LastCloudminiSyncUtc.HasValue)
            {
                CloudminiLastSyncTextBlock.Text = string.Format(
                    "Last sync: {0} ({1})",
                    _settings.LastCloudminiSyncUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    _settings.LastCloudminiSyncSummary ?? "completed");
            }
            else
            {
                CloudminiLastSyncTextBlock.Text = "Last sync: never";
            }
        }

        private void UpdateVersionText()
        {
            var versionText = GetDisplayVersion();
            if (VersionTagTextBlock != null)
            {
                VersionTagTextBlock.Text = versionText;
            }

            if (AppVersionTextBlock != null)
            {
                AppVersionTextBlock.Text = "Version: " + versionText;
            }
        }

        private static string GetDisplayVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .Select(attribute => attribute.InformationalVersion)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return "v" + informationalVersion;
            }

            var version = assembly.GetName().Version;
            if (version == null)
            {
                return "v0.0.0";
            }

            return string.Format("v{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        private void UpdateFavoriteButtonState()
        {
            if (FavoriteButton == null)
            {
                return;
            }

            var entry = _editingEntry;
            FavoriteButton.IsEnabled = entry != null;
            FavoriteButton.Content = entry != null && entry.IsFavorite ? "\uE735" : "\uE734";
            FavoriteButton.ToolTip = entry == null
                ? "Select an entry first"
                : entry.IsFavorite ? "Remove from favorites" : "Add to favorites";
        }
    }
}

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
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager
{
    public enum AppSection
    {
        Connections,
        CloudminiSync,
        Settings
    }

    public enum NavigationFilter
    {
        AllConnections,
        Favorites,
        Recent
    }

    public enum CloudminiPlatformFilter
    {
        All,
        Windows,
        Linux
    }

    public partial class MainWindow : Window
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;
        private const int EntriesPageSize = 10;

        private ObservableCollection<RdpEntry> _entries = new ObservableCollection<RdpEntry>();
        private readonly ObservableCollection<RdpEntry> _pagedEntries = new ObservableCollection<RdpEntry>();
        private string _currentFilePath;
        private bool _isDirty;
        private bool _isCreatingNew = true;
        private bool _editorDirty;
        private bool _isPopulatingForm;
        private bool _isRebuildingEntriesPage;
        private ICollectionView _entriesView;
        private int _currentEntriesPage = 1;
        private int _filteredEntriesCount;
        private readonly ObservableCollection<CloudminiSyncPreviewItem> _cloudminiPreviewItems = new ObservableCollection<CloudminiSyncPreviewItem>();
        private readonly List<CloudminiVps> _cloudminiRemoteItems = new List<CloudminiVps>();
        private NavigationFilter _currentNavigationFilter = NavigationFilter.AllConnections;
        private CloudminiPlatformFilter _currentConnectionsPlatformFilter = CloudminiPlatformFilter.All;
        private CloudminiPlatformFilter _currentCloudminiPlatformFilter = CloudminiPlatformFilter.All;
        private AppSection _currentAppSection = AppSection.Connections;
        private RdpEntry _editingEntry;
        private AppSettings _settings;
        private string _sessionCloudminiToken;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public MainWindow()
        {
            InitializeComponent();

            EntriesGrid.ItemsSource = _pagedEntries;
            CloudminiPreviewGrid.ItemsSource = _cloudminiPreviewItems;
            _currentFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clients.csv");
            _settings = SettingsStorage.Load();

            ApplySettingsToUi();
            UpdateVersionText();

            CsvStorage.EnsureFileExists(_currentFilePath);
            LoadEntries(_currentFilePath);
            UpdateWindowTitle();
            RdpLauncher.CleanupTemporaryFiles();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyWindowFrameTheme();
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
                FileName = Path.GetFileName(_currentFilePath),
                InitialDirectory = GetInitialDirectory(_currentFilePath)
            };

            if (dialog.ShowDialog(this) == true)
            {
                CsvStorage.EnsureFileExists(dialog.FileName);
                LoadEntries(dialog.FileName);
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveEntries(_currentFilePath);
        }

        private void SaveAsButton_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = Path.GetFileName(_currentFilePath),
                InitialDirectory = GetInitialDirectory(_currentFilePath),
                DefaultExt = ".csv",
                AddExtension = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                SaveEntries(dialog.FileName);
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

        private void ConnectionsAllPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentConnectionsPlatformFilter = CloudminiPlatformFilter.All;
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void ConnectionsWindowsPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentConnectionsPlatformFilter = CloudminiPlatformFilter.Windows;
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void ConnectionsLinuxPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentConnectionsPlatformFilter = CloudminiPlatformFilter.Linux;
            _currentEntriesPage = 1;
            RefreshEntriesView();
        }

        private void CloudminiAllPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentCloudminiPlatformFilter = CloudminiPlatformFilter.All;
            RebuildCloudminiPreview();
        }

        private void CloudminiWindowsPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentCloudminiPlatformFilter = CloudminiPlatformFilter.Windows;
            RebuildCloudminiPreview();
        }

        private void CloudminiLinuxPlatformFilterButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentCloudminiPlatformFilter = CloudminiPlatformFilter.Linux;
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

        private void BackToConnectionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            SetNavigationFilter(NavigationFilter.AllConnections);
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

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!ConfirmDiscardIfNeeded())
            {
                e.Cancel = true;
            }

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

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Host cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                HostTextBox.Focus();
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
            _isPopulatingForm = false;
            _editorDirty = false;
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
            _isPopulatingForm = false;
            _editorDirty = false;
            HostTextBox.Focus();
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

            UpdateSettingsSummary();
        }

        private void LoadEntries(string path)
        {
            _entries = CsvStorage.Load(path);
            MetadataStorage.Apply(path, _entries);
            _currentEntriesPage = 1;
            ConfigureEntriesView();
            _currentFilePath = path;
            _isDirty = false;
            _isCreatingNew = true;
            StartNewEntry();
            UpdateWindowTitle();
            UpdateSettingsSummary();
        }

        private void SaveEntries(string path)
        {
            ApplyPendingFormChangesIfNeeded();
            CsvStorage.Save(_entries, path);
            MetadataStorage.Save(path, _entries);
            _currentFilePath = path;
            _isDirty = false;
            UpdateWindowTitle();
            UpdateSummary();
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
                !string.Equals(_editingEntry.Password ?? string.Empty, PasswordBox.Password ?? string.Empty, StringComparison.Ordinal);

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
                SaveEntries(_currentFilePath);
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
            _entriesView = CollectionViewSource.GetDefaultView(_entries);
            if (_entriesView != null)
            {
                _entriesView.Filter = FilterEntry;
            }

            ApplyViewState();
            RebuildEntriesPage();
            UpdateSummary();
        }

        private void RefreshEntriesView()
        {
            ApplyViewState();
            RebuildEntriesPage();
            UpdateSummary();
        }

        private bool FilterEntry(object item)
        {
            var entry = item as RdpEntry;
            if (entry == null)
            {
                return false;
            }

            var query = SearchTextBox == null ? string.Empty : (SearchTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return MatchesNavigationFilter(entry) && MatchesConnectionsPlatformFilter(entry);
            }

            return MatchesNavigationFilter(entry) &&
                   MatchesConnectionsPlatformFilter(entry) &&
                   (ContainsIgnoreCase(entry.HostName, query) ||
                    ContainsIgnoreCase(entry.Host, query) ||
                    ContainsIgnoreCase(entry.User, query));
        }

        private void UpdateSummary()
        {
            UpdateNavigationVisuals();
            UpdateViewState();
            UpdateFilterButtonVisuals();

            if (_currentAppSection == AppSection.Connections)
            {
                UpdateFavoriteButtonState();
                UpdateEmptyState();
            }

            UpdateCloudminiEmptyState();
            UpdateSettingsSummary();
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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
                SearchTextBox.Visibility = _currentAppSection == AppSection.Connections ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ConnectionsActionsPanel != null)
            {
                ConnectionsActionsPanel.Visibility = _currentAppSection == AppSection.Connections ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyViewState()
        {
            if (_entriesView == null)
            {
                return;
            }

            _entriesView.SortDescriptions.Clear();
            if (_currentNavigationFilter == NavigationFilter.Recent)
            {
                _entriesView.SortDescriptions.Add(new SortDescription("LastConnectedUtc", ListSortDirection.Descending));
            }

            _entriesView.Refresh();
        }

        private bool MatchesNavigationFilter(RdpEntry entry)
        {
            switch (_currentNavigationFilter)
            {
                case NavigationFilter.Favorites:
                    return entry.IsFavorite;
                case NavigationFilter.Recent:
                    return entry.LastConnectedUtc.HasValue;
                default:
                    return true;
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
            var inactiveBackground = Brushes.Transparent;
            var activeForeground = FindResource("TextPrimaryBrush") as Brush;
            var inactiveForeground = FindResource("TextSecondaryBrush") as Brush;

            if (control != null)
            {
                control.Background = isActive ? activeBackground : inactiveBackground;
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
            if (EmptyStateTextBlock == null || _entriesView == null)
            {
                return;
            }

            if (_filteredEntriesCount > 0)
            {
                EmptyStateTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            switch (_currentNavigationFilter)
            {
                case NavigationFilter.Favorites:
                    EmptyStateTextBlock.Text = _currentConnectionsPlatformFilter == CloudminiPlatformFilter.All
                        ? "No favorite connections yet. Select an entry and click the star in Entry editor."
                        : string.Format("No {0} favorite connections match the current filter.", GetPlatformFilterLabel(_currentConnectionsPlatformFilter));
                    break;
                case NavigationFilter.Recent:
                    EmptyStateTextBlock.Text = _currentConnectionsPlatformFilter == CloudminiPlatformFilter.All
                        ? "No recent connections yet. Launch an RDP session once and it will appear here."
                        : string.Format("No {0} recent connections match the current filter.", GetPlatformFilterLabel(_currentConnectionsPlatformFilter));
                    break;
                default:
                    EmptyStateTextBlock.Text = _currentConnectionsPlatformFilter == CloudminiPlatformFilter.All
                        ? "No connections found. Add a new entry or open another CSV file."
                        : string.Format("No {0} connections match the current filter.", GetPlatformFilterLabel(_currentConnectionsPlatformFilter));
                    break;
            }

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
                switch (_currentCloudminiPlatformFilter)
                {
                    case CloudminiPlatformFilter.Windows:
                        CloudminiEmptyStateTextBlock.Text = "No Windows VPS match the current filter.";
                        break;
                    case CloudminiPlatformFilter.Linux:
                        CloudminiEmptyStateTextBlock.Text = "No Linux VPS match the current filter.";
                        break;
                    default:
                        CloudminiEmptyStateTextBlock.Text = "No Cloudmini VPS loaded yet.";
                        break;
                }

                CloudminiEmptyStateTextBlock.Visibility = Visibility.Visible;
                return;
            }

            CloudminiEmptyStateTextBlock.Visibility = Visibility.Collapsed;
        }

        private bool MatchesConnectionsPlatformFilter(RdpEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            var isLinux = string.Equals((entry.Port ?? string.Empty).Trim(), "22", StringComparison.OrdinalIgnoreCase);
            switch (_currentConnectionsPlatformFilter)
            {
                case CloudminiPlatformFilter.Windows:
                    return !isLinux;
                case CloudminiPlatformFilter.Linux:
                    return isLinux;
                default:
                    return true;
            }
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

            var filteredRemoteItems = _cloudminiRemoteItems.Where(MatchesCloudminiPlatformFilter).ToList();
            var preview = CloudminiSyncService.BuildPreview(_entries, filteredRemoteItems, BuildCurrentSyncOptions());
            _cloudminiPreviewItems.Clear();
            foreach (var item in preview)
            {
                _cloudminiPreviewItems.Add(item);
            }

            UpdateCloudminiEmptyState();
        }

        private bool MatchesCloudminiPlatformFilter(CloudminiVps remote)
        {
            if (remote == null)
            {
                return false;
            }

            var isLinux = string.Equals((remote.Port ?? string.Empty).Trim(), "22", StringComparison.OrdinalIgnoreCase);
            switch (_currentCloudminiPlatformFilter)
            {
                case CloudminiPlatformFilter.Windows:
                    return !isLinux;
                case CloudminiPlatformFilter.Linux:
                    return isLinux;
                default:
                    return true;
            }
        }

        private void UpdateFilterButtonVisuals()
        {
            SetFilterButtonVisual(ConnectionsAllPlatformFilterButton, _currentConnectionsPlatformFilter == CloudminiPlatformFilter.All);
            SetFilterButtonVisual(ConnectionsWindowsPlatformFilterButton, _currentConnectionsPlatformFilter == CloudminiPlatformFilter.Windows);
            SetFilterButtonVisual(ConnectionsLinuxPlatformFilterButton, _currentConnectionsPlatformFilter == CloudminiPlatformFilter.Linux);

            SetFilterButtonVisual(CloudminiAllPlatformFilterButton, _currentCloudminiPlatformFilter == CloudminiPlatformFilter.All);
            SetFilterButtonVisual(CloudminiWindowsPlatformFilterButton, _currentCloudminiPlatformFilter == CloudminiPlatformFilter.Windows);
            SetFilterButtonVisual(CloudminiLinuxPlatformFilterButton, _currentCloudminiPlatformFilter == CloudminiPlatformFilter.Linux);
        }

        private void SetFilterButtonVisual(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isActive
                ? (Brush)(FindResource("AccentSoftBrush") as Brush ?? Brushes.LightBlue)
                : (Brush)(FindResource("SurfaceBrush") as Brush ?? Brushes.White);
            button.BorderBrush = isActive
                ? (Brush)(FindResource("AccentSoftBorderBrush") as Brush ?? Brushes.DodgerBlue)
                : (Brush)(FindResource("CardBorderBrush") as Brush ?? Brushes.LightGray);
            button.Foreground = isActive
                ? (Brush)(FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue)
                : (Brush)(FindResource("TextPrimaryBrush") as Brush ?? Brushes.Black);
        }

        private static string GetPlatformFilterLabel(CloudminiPlatformFilter filter)
        {
            switch (filter)
            {
                case CloudminiPlatformFilter.Windows:
                    return "Windows";
                case CloudminiPlatformFilter.Linux:
                    return "Linux";
                default:
                    return "All";
            }
        }

        private void RebuildEntriesPage()
        {
            if (_entriesView == null)
            {
                _filteredEntriesCount = 0;
                _pagedEntries.Clear();
                UpdateEntriesPaginationControls();
                return;
            }

            var filteredEntries = _entriesView.Cast<RdpEntry>().ToList();
            _filteredEntriesCount = filteredEntries.Count;

            var totalPages = GetTotalEntriesPages();
            if (_currentEntriesPage > totalPages)
            {
                _currentEntriesPage = totalPages;
            }

            if (_currentEntriesPage < 1)
            {
                _currentEntriesPage = 1;
            }

            var currentSelection = EntriesGrid == null ? null : EntriesGrid.SelectedItem as RdpEntry;
            var pageEntries = filteredEntries
                .Skip((_currentEntriesPage - 1) * EntriesPageSize)
                .Take(EntriesPageSize)
                .ToList();

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
            if (entry == null || _entriesView == null)
            {
                return;
            }

            var filteredEntries = _entriesView.Cast<RdpEntry>().ToList();
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

            if (onlySelected && !_cloudminiPreviewItems.Any(item => item.IsSelected))
            {
                MessageBox.Show(this, "Select at least one VPS to sync.", "Cloudmini Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
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
                CloudminiStatusTextBlock.Text = _settings.LastCloudminiSyncSummary;

                MessageBox.Show(
                    this,
                    string.Format(
                        "Cloudmini sync completed.\nCreated: {0}\nUpdated: {1}\nSkipped: {2}\nConflicts: {3}",
                        result.CreatedCount,
                        result.UpdatedCount,
                        result.SkippedCount,
                        result.ConflictCount),
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

            CurrentCsvPathTextBlock.Text = "CSV: " + (_currentFilePath ?? "-");
            SettingsPathTextBlock.Text = "Settings: " + SettingsStorage.GetSettingsPath();
            SavedTokenStatusTextBlock.Text = _settings != null && _settings.RememberCloudminiToken && !string.IsNullOrWhiteSpace(_settings.EncryptedCloudminiToken)
                ? "Saved token: stored for current Windows user"
                : "Saved token: not stored";
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

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    public enum NavigationFilter
    {
        AllConnections,
        Favorites,
        Recent
    }

    public partial class MainWindow : Window
    {
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;

        private ObservableCollection<RdpEntry> _entries = new ObservableCollection<RdpEntry>();
        private string _currentFilePath;
        private bool _isDirty;
        private bool _isCreatingNew = true;
        private bool _editorDirty;
        private bool _isPopulatingForm;
        private ICollectionView _entriesView;
        private NavigationFilter _currentNavigationFilter = NavigationFilter.AllConnections;
        private RdpEntry _editingEntry;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public MainWindow()
        {
            InitializeComponent();

            EntriesGrid.ItemsSource = _entries;
            _currentFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clients.csv");

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

        private void EntriesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntriesGrid.SelectedItem is RdpEntry)
            {
                ConnectCurrentEntry();
            }
        }

        private void EntriesGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
            RefreshEntriesView();
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
                EntriesGrid.SelectedItem = entry;
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

        private void LoadEntries(string path)
        {
            _entries = CsvStorage.Load(path);
            MetadataStorage.Apply(path, _entries);
            EntriesGrid.ItemsSource = _entries;
            ConfigureEntriesView();
            _currentFilePath = path;
            _isDirty = false;
            _isCreatingNew = true;
            StartNewEntry();
            UpdateWindowTitle();
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
            Title = _isDirty
                ? string.Format("RDP Manager - {0} *", fileName)
                : string.Format("RDP Manager - {0}", fileName);
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
            if (!_editorDirty)
            {
                if (selected == null && HasMeaningfulFormInput())
                {
                    return ApplyEditorToCollection();
                }

                return selected ?? _editingEntry;
            }

            return ApplyEditorToCollection();
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
            UpdateSummary();
        }

        private void RefreshEntriesView()
        {
            ApplyViewState();
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
                return MatchesNavigationFilter(entry);
            }

            return MatchesNavigationFilter(entry) &&
                   (ContainsIgnoreCase(entry.HostName, query) ||
                    ContainsIgnoreCase(entry.Host, query) ||
                    ContainsIgnoreCase(entry.User, query));
        }

        private void UpdateSummary()
        {
            UpdateNavigationVisuals();
            UpdateFavoriteButtonState();
            UpdateEmptyState();
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

        private void SetNavigationFilter(NavigationFilter filter)
        {
            _currentNavigationFilter = filter;
            RefreshEntriesView();
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
            SetNavigationVisual(AllConnectionsNavButton, AllConnectionsNavIcon, AllConnectionsNavText, _currentNavigationFilter == NavigationFilter.AllConnections);
            SetNavigationVisual(FavoritesNavButton, FavoritesNavIcon, FavoritesNavText, _currentNavigationFilter == NavigationFilter.Favorites);
            SetNavigationVisual(RecentNavButton, RecentNavIcon, RecentNavText, _currentNavigationFilter == NavigationFilter.Recent);
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

            var hasVisibleItems = false;
            foreach (var item in _entriesView)
            {
                hasVisibleItems = true;
                break;
            }

            if (hasVisibleItems)
            {
                EmptyStateTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            switch (_currentNavigationFilter)
            {
                case NavigationFilter.Favorites:
                    EmptyStateTextBlock.Text = "No favorite connections yet. Select an entry and click the star in Entry editor.";
                    break;
                case NavigationFilter.Recent:
                    EmptyStateTextBlock.Text = "No recent connections yet. Launch an RDP session once and it will appear here.";
                    break;
                default:
                    EmptyStateTextBlock.Text = "No connections found. Add a new entry or open another CSV file.";
                    break;
            }

            EmptyStateTextBlock.Visibility = Visibility.Visible;
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

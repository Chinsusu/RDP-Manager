using System.ComponentModel;

namespace RdpManager.Models
{
    public class CloudminiSyncPreviewItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private string _syncAction;
        private string _note;

        public event PropertyChangedEventHandler PropertyChanged;

        public CloudminiVps Remote { get; set; }

        public RdpEntry MatchedEntry { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public string SyncAction
        {
            get { return _syncAction; }
            set
            {
                if (_syncAction == value)
                {
                    return;
                }

                _syncAction = value;
                OnPropertyChanged("SyncAction");
            }
        }

        public string Note
        {
            get { return _note; }
            set
            {
                if (_note == value)
                {
                    return;
                }

                _note = value;
                OnPropertyChanged("Note");
            }
        }

        public string SourceId
        {
            get { return Remote == null ? string.Empty : Remote.Id.ToString(); }
        }

        public string Ip
        {
            get { return Remote == null ? string.Empty : Remote.Ip; }
        }

        public string Port
        {
            get { return Remote == null ? string.Empty : Remote.Port; }
        }

        public string User
        {
            get { return Remote == null ? string.Empty : Remote.User; }
        }

        public string Status
        {
            get { return Remote == null ? string.Empty : Remote.Status; }
        }

        public string Platform
        {
            get
            {
                var port = Port ?? string.Empty;
                return string.Equals(port.Trim(), "22", System.StringComparison.OrdinalIgnoreCase)
                    ? "Linux"
                    : "Windows";
            }
        }

        public string Location
        {
            get { return Remote == null ? string.Empty : Remote.Location; }
        }

        public string ExpiredAtDisplay
        {
            get
            {
                if (Remote == null || !Remote.ExpiredAtUtc.HasValue)
                {
                    return string.Empty;
                }

                return Remote.ExpiredAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

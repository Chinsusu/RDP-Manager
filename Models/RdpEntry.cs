using System.ComponentModel;
using RdpManager.Services;

namespace RdpManager.Models
{
    public class RdpEntry : INotifyPropertyChanged
    {
        private string _hostName;
        private string _host;
        private string _port = "3389";
        private string _user;
        private string _password;
        private bool _isFavorite;
        private System.DateTime? _lastConnectedUtc;
        private string _sourceProvider;
        private string _sourceId;
        private string _sourceStatus;
        private string _sourceLocation;
        private System.DateTime? _sourceCreatedAtUtc;
        private System.DateTime? _sourceExpiredAtUtc;
        private System.DateTime? _lastSyncedUtc;
        private bool _isProviderManaged;

        public event PropertyChangedEventHandler PropertyChanged;

        public string HostName
        {
            get { return _hostName; }
            set
            {
                if (_hostName == value)
                {
                    return;
                }

                _hostName = value;
                OnPropertyChanged("HostName");
            }
        }

        public string Host
        {
            get { return _host; }
            set
            {
                if (_host == value)
                {
                    return;
                }

                _host = value;
                OnPropertyChanged("Host");
            }
        }

        public string Port
        {
            get { return _port; }
            set
            {
                if (_port == value)
                {
                    return;
                }

                _port = value;
                OnPropertyChanged("Port");
                OnPropertyChanged("PlatformLabel");
            }
        }

        public string User
        {
            get { return _user; }
            set
            {
                if (_user == value)
                {
                    return;
                }

                _user = value;
                OnPropertyChanged("User");
                OnPropertyChanged("PlatformLabel");
            }
        }

        public string Password
        {
            get { return _password; }
            set
            {
                if (_password == value)
                {
                    return;
                }

                _password = value;
                OnPropertyChanged("Password");
                OnPropertyChanged("MaskedPassword");
            }
        }

        public string MaskedPassword
        {
            get { return string.IsNullOrEmpty(Password) ? string.Empty : "********"; }
        }

        public string PlatformLabel
        {
            get { return ConnectionClassifier.GetPlatformLabel(Port, User); }
        }

        public bool IsFavorite
        {
            get { return _isFavorite; }
            set
            {
                if (_isFavorite == value)
                {
                    return;
                }

                _isFavorite = value;
                OnPropertyChanged("IsFavorite");
            }
        }

        public System.DateTime? LastConnectedUtc
        {
            get { return _lastConnectedUtc; }
            set
            {
                if (_lastConnectedUtc == value)
                {
                    return;
                }

                _lastConnectedUtc = value;
                OnPropertyChanged("LastConnectedUtc");
            }
        }

        public string SourceProvider
        {
            get { return _sourceProvider; }
            set
            {
                if (_sourceProvider == value)
                {
                    return;
                }

                _sourceProvider = value;
                OnPropertyChanged("SourceProvider");
            }
        }

        public string SourceId
        {
            get { return _sourceId; }
            set
            {
                if (_sourceId == value)
                {
                    return;
                }

                _sourceId = value;
                OnPropertyChanged("SourceId");
            }
        }

        public string SourceStatus
        {
            get { return _sourceStatus; }
            set
            {
                if (_sourceStatus == value)
                {
                    return;
                }

                _sourceStatus = value;
                OnPropertyChanged("SourceStatus");
                OnPropertyChanged("StatusLabel");
            }
        }

        public string SourceLocation
        {
            get { return _sourceLocation; }
            set
            {
                if (_sourceLocation == value)
                {
                    return;
                }

                _sourceLocation = value;
                OnPropertyChanged("SourceLocation");
            }
        }

        public System.DateTime? SourceCreatedAtUtc
        {
            get { return _sourceCreatedAtUtc; }
            set
            {
                if (_sourceCreatedAtUtc == value)
                {
                    return;
                }

                _sourceCreatedAtUtc = value;
                OnPropertyChanged("SourceCreatedAtUtc");
            }
        }

        public System.DateTime? SourceExpiredAtUtc
        {
            get { return _sourceExpiredAtUtc; }
            set
            {
                if (_sourceExpiredAtUtc == value)
                {
                    return;
                }

                _sourceExpiredAtUtc = value;
                OnPropertyChanged("SourceExpiredAtUtc");
            }
        }

        public System.DateTime? LastSyncedUtc
        {
            get { return _lastSyncedUtc; }
            set
            {
                if (_lastSyncedUtc == value)
                {
                    return;
                }

                _lastSyncedUtc = value;
                OnPropertyChanged("LastSyncedUtc");
            }
        }

        public bool IsProviderManaged
        {
            get { return _isProviderManaged; }
            set
            {
                if (_isProviderManaged == value)
                {
                    return;
                }

                _isProviderManaged = value;
                OnPropertyChanged("IsProviderManaged");
                OnPropertyChanged("StatusLabel");
            }
        }

        public string StatusLabel
        {
            get { return ConnectionClassifier.GetLocalStatusLabel(this); }
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

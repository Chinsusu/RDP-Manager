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
        private TransportMode _transportMode;
        private string _jumpHostProfileId;
        private string _tunnelTargetHostOverride;
        private string _tunnelTargetPortOverride;
        private string _groupName;
        private string _tags;
        private string _notes;
        private bool _isFavorite;
        private System.DateTime? _lastConnectedUtc;
        private string _healthStatus;
        private System.DateTime? _lastHealthCheckedUtc;
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

        public TransportMode TransportMode
        {
            get { return _transportMode; }
            set
            {
                if (_transportMode == value)
                {
                    return;
                }

                _transportMode = value;
                OnPropertyChanged("TransportMode");
                OnPropertyChanged("TransportLabel");
            }
        }

        public string JumpHostProfileId
        {
            get { return _jumpHostProfileId; }
            set
            {
                if (_jumpHostProfileId == value)
                {
                    return;
                }

                _jumpHostProfileId = value;
                OnPropertyChanged("JumpHostProfileId");
            }
        }

        public string TunnelTargetHostOverride
        {
            get { return _tunnelTargetHostOverride; }
            set
            {
                if (_tunnelTargetHostOverride == value)
                {
                    return;
                }

                _tunnelTargetHostOverride = value;
                OnPropertyChanged("TunnelTargetHostOverride");
            }
        }

        public string TunnelTargetPortOverride
        {
            get { return _tunnelTargetPortOverride; }
            set
            {
                if (_tunnelTargetPortOverride == value)
                {
                    return;
                }

                _tunnelTargetPortOverride = value;
                OnPropertyChanged("TunnelTargetPortOverride");
            }
        }

        public string GroupName
        {
            get { return _groupName; }
            set
            {
                if (_groupName == value)
                {
                    return;
                }

                _groupName = value;
                OnPropertyChanged("GroupName");
            }
        }

        public string Tags
        {
            get { return _tags; }
            set
            {
                if (_tags == value)
                {
                    return;
                }

                _tags = value;
                OnPropertyChanged("Tags");
            }
        }

        public string Notes
        {
            get { return _notes; }
            set
            {
                if (_notes == value)
                {
                    return;
                }

                _notes = value;
                OnPropertyChanged("Notes");
            }
        }

        public string PlatformLabel
        {
            get { return ConnectionClassifier.GetPlatformLabel(Port, User); }
        }

        public string TransportLabel
        {
            get { return TransportMode == TransportMode.SshTunnel ? "SSH Tunnel" : "Direct"; }
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

        public string HealthStatus
        {
            get { return _healthStatus; }
            set
            {
                if (_healthStatus == value)
                {
                    return;
                }

                _healthStatus = value;
                OnPropertyChanged("HealthStatus");
                OnPropertyChanged("HealthLabel");
                OnPropertyChanged("HealthDetails");
            }
        }

        public System.DateTime? LastHealthCheckedUtc
        {
            get { return _lastHealthCheckedUtc; }
            set
            {
                if (_lastHealthCheckedUtc == value)
                {
                    return;
                }

                _lastHealthCheckedUtc = value;
                OnPropertyChanged("LastHealthCheckedUtc");
                OnPropertyChanged("HealthDetails");
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

        public string HealthLabel
        {
            get { return ConnectionHealthService.GetDisplayLabel(HealthStatus); }
        }

        public string HealthDetails
        {
            get { return ConnectionHealthService.GetDetails(HealthStatus, LastHealthCheckedUtc); }
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

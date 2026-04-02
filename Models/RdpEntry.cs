using System.ComponentModel;

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

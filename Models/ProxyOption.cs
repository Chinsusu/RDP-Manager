namespace RdpManager.Models
{
    public class ProxyOption
    {
        public string DisplayName { get; set; }

        public TransportMode TransportMode { get; set; }

        public string JumpHostProfileId { get; set; }

        public string SelectionKey
        {
            get
            {
                return TransportMode == TransportMode.SshTunnel
                    ? "proxy:" + (JumpHostProfileId ?? string.Empty)
                    : "direct";
            }
        }

        public override string ToString()
        {
            return DisplayName ?? string.Empty;
        }
    }
}

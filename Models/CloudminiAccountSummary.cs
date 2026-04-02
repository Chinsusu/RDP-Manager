using System.Runtime.Serialization;

namespace RdpManager.Models
{
    [DataContract]
    public class CloudminiAccountSummary
    {
        [DataMember(Name = "balance")]
        public decimal Balance { get; set; }

        [DataMember(Name = "credit")]
        public decimal Credit { get; set; }
    }
}

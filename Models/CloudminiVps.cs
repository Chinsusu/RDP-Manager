using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace RdpManager.Models
{
    [DataContract]
    public class CloudminiVps
    {
        [DataMember(Name = "pk")]
        public int Id { get; set; }

        [DataMember(Name = "ip")]
        public string Ip { get; set; }

        [DataMember(Name = "user")]
        public string User { get; set; }

        [DataMember(Name = "password")]
        public string Password { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAtRaw { get; set; }

        [DataMember(Name = "expired_at")]
        public string ExpiredAtRaw { get; set; }

        [DataMember(Name = "port")]
        public string Port { get; set; }

        [DataMember(Name = "cpu")]
        public int Cpu { get; set; }

        [DataMember(Name = "ram")]
        public int Ram { get; set; }

        [DataMember(Name = "disk")]
        public int Disk { get; set; }

        [DataMember(Name = "price")]
        public decimal Price { get; set; }

        [DataMember(Name = "location")]
        public string Location { get; set; }

        [DataMember(Name = "status")]
        public string Status { get; set; }

        public DateTime? CreatedAtUtc
        {
            get { return ParseDate(CreatedAtRaw); }
        }

        public DateTime? ExpiredAtUtc
        {
            get { return ParseDate(ExpiredAtRaw); }
        }

        private static DateTime? ParseDate(string rawValue)
        {
            DateTime value;
            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
            {
                return value;
            }

            return null;
        }
    }
}

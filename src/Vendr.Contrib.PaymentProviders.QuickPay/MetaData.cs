using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class MetaData
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "origin")]
        public string Origin { get; set; }

        [DataMember(Name = "brand")]
        public string Brand { get; set; }

        [DataMember(Name = "bin")]
        public string Bin { get; set; }

        [DataMember(Name = "last4")]
        public string Last4 { get; set; }

        [DataMember(Name = "exp_month")]
        public int? ExpMonth { get; set; }

        [DataMember(Name = "exp_year")]
        public int? ExpYear { get; set; }

        [DataMember(Name = "country")]
        public string Country { get; set; }

        [DataMember(Name = "is_3d_secure")]
        public bool? Is3dSecure { get; set; }

        [DataMember(Name = "hash")]
        public string Hash { get; set; }

        [DataMember(Name = "number")]
        public object Number { get; set; }

        [DataMember(Name = "customer_ip")]
        public string CustomerIp { get; set; }

        [DataMember(Name = "customer_country")]
        public string CustomerCountry { get; set; }

        [DataMember(Name = "fraud_suspected")]
        public bool FraudSuspected { get; set; }

        [DataMember(Name = "fraud_remarks")]
        public List<object> FraudRemarks { get; set; }

        [DataMember(Name = "nin_number")]
        public object NinNumber { get; set; }

        [DataMember(Name = "nin_country_code")]
        public object NinCountryCode { get; set; }

        [DataMember(Name = "nin_gender")]
        public object NinGender { get; set; }
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class MetaData
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("bin")]
        public string Bin { get; set; }

        [JsonProperty("last4")]
        public string Last4 { get; set; }

        [JsonProperty("exp_month")]
        public int? ExpMonth { get; set; }

        [JsonProperty("exp_year")]
        public int? ExpYear { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("is_3d_secure")]
        public bool? Is3dSecure { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("number")]
        public object Number { get; set; }

        [JsonProperty("customer_ip")]
        public string CustomerIp { get; set; }

        [JsonProperty("customer_country")]
        public string CustomerCountry { get; set; }

        [JsonProperty("fraud_suspected")]
        public bool FraudSuspected { get; set; }

        [JsonProperty("fraud_remarks")]
        public List<object> FraudRemarks { get; set; }

        [JsonProperty("nin_number")]
        public object NinNumber { get; set; }

        [JsonProperty("nin_country_code")]
        public object NinCountryCode { get; set; }

        [JsonProperty("nin_gender")]
        public object NinGender { get; set; }
    }
}

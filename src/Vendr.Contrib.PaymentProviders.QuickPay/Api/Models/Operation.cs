using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class Operation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("pending")]
        public bool Pending { get; set; }

        [JsonProperty("qp_status_code")]
        public string QuickPayStatusCode { get; set; }

        [JsonProperty("qp_status_msg")]
        public string QuickPayStatusMessage { get; set; }

        [JsonProperty("aq_status_code")]
        public string AcquirerStatusCode { get; set; }

        [JsonProperty("aq_status_msg")]
        public string AcquirerStatusMessage { get; set; }
    }
}

using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class Operation
    {
        /// <summary>
        /// Operation ID
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Type of operation
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Amount
        /// </summary>
        [JsonProperty("amount")]
        public int Amount { get; set; }

        /// <summary>
        /// If the operation is pending
        /// </summary>
        [JsonProperty("pending")]
        public bool Pending { get; set; }

        /// <summary>
        /// QuickPay status code
        /// </summary>
        [JsonProperty("qp_status_code")]
        public string QuickPayStatusCode { get; set; }

        /// <summary>
        /// QuickPay status message
        /// </summary>
        [JsonProperty("qp_status_msg")]
        public string QuickPayStatusMessage { get; set; }

        /// <summary>
        /// Acquirer status code
        /// </summary>
        [JsonProperty("aq_status_code")]
        public string AcquirerStatusCode { get; set; }

        /// <summary>
        /// Acquirer status message
        /// </summary>
        [JsonProperty("aq_status_msg")]
        public string AcquirerStatusMessage { get; set; }
    }
}

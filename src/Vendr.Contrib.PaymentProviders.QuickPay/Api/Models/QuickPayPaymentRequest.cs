using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class QuickPayPaymentRequest
    {
        /// <summary>
        /// Unique order id (must be between 4-20 characters).
        /// </summary>
        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        /// <summary>
        /// Currency
        /// </summary>
        [JsonProperty("currency")]
        public string Currency { get; set; }

        /// <summary>
        /// Custom variables
        /// </summary>
        [JsonProperty("variables")]
        public Dictionary<string, string> Variables { get; set; }
    }
}

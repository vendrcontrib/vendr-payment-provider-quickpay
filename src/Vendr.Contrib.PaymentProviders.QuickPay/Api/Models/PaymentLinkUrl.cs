using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class PaymentLinkUrl
    {
        /// <summary>
        /// Url to payment window for this payment link
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}

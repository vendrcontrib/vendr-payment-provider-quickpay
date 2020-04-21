using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class PaymentLinkUrl
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}

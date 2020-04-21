using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class PaymentLink : PaymentLinkUrl
    {
        [JsonProperty("agreement_id")]
        public int AgreementId { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("continue_url")]
        public string ContinueUrl { get; set; }

        [JsonProperty("cancel_url")]
        public string CancelUrl { get; set; }

        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        [JsonProperty("payment_methods")]
        public string PaymentMethods { get; set; }

        [JsonProperty("auto_fee")]
        public bool AutoFee { get; set; }

        [JsonProperty("auto_capture")]
        public bool AutoCapture { get; set; }
    }
}

using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class PaymentLink : PaymentLinkUrl
    {
        [DataMember(Name = "agreement_id")]
        public int AgreementId { get; set; }

        [DataMember(Name = "language")]
        public string Language { get; set; }

        [DataMember(Name = "amount")]
        public int Amount { get; set; }

        [DataMember(Name = "continue_url")]
        public string ContinueUrl { get; set; }

        [DataMember(Name = "cancel_url")]
        public string CancelUrl { get; set; }

        [DataMember(Name = "callback_url")]
        public string CallbackUrl { get; set; }

        [DataMember(Name = "payment_methods")]
        public string PaymentMethods { get; set; }

        [DataMember(Name = "auto_fee")]
        public bool AutoFee { get; set; }

        [DataMember(Name = "auto_capture")]
        public bool AutoCapture { get; set; }
    }
}

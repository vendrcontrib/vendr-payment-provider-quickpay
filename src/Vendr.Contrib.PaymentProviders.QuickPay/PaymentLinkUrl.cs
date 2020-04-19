using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class PaymentLinkUrl
    {
        [DataMember(Name = "url")]
        public string Url { get; set; }
    }
}

using System;
using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class QuickPayPaymentLinkDto
    {
        [DataMember(Name = "url")]
        public string Url { get; set; }
    }
}

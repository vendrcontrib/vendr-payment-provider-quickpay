using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class QuickPayPaymentDto
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "merchant_id")]
        public int MerchantId { get; set; }

        [DataMember(Name = "order_id")]
        public string OrderId { get; set; }

        [DataMember(Name = "accepted")]
        public bool Accepted { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "currency")]
        public string Currency { get; set; }

        [DataMember(Name = "link")]
        public object Link { get; set; }
        
    }
}

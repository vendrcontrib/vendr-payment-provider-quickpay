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

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "metadata")]
        public dynamic MetaData { get; set; }

        [DataMember(Name = "link")]
        public object Link { get; set; }

        [DataMember(Name = "test_mode")]
        public bool TestMode { get; set; }

        [DataMember(Name = "acquirer")]
        public string Acquirer { get; set; }

        [DataMember(Name = "facilitator")]
        public string Facilitator { get; set; }

        [DataMember(Name = "balance")]
        public int Balance { get; set; }

        [DataMember(Name = "fee")]
        public int Fee { get; set; }
    }
}

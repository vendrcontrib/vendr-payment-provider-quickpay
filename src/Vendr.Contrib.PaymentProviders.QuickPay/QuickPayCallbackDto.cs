using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class QuickPayCallbackDto
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "merchant_id")]
        public string MerchantId { get; set; }

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

        [DataMember(Name = "operations")]
        public List<Operation> Operations { get; set; }

        [DataMember(Name = "metadata")]
        public MetaData MetaData { get; set; }

        [DataMember(Name = "test_mode")]
        public bool TestMode { get; set; }

        [DataMember(Name = "acquirer")]
        public string Acquirer { get; set; }

        [DataMember(Name = "balance")]
        public int Balance { get; set; }

        [DataMember(Name = "fee")]
        public int? Fee { get; set; }

        [DataMember(Name = "created_at")]
        public DateTime CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public DateTime UpdatedAt { get; set; }

        [DataMember(Name = "retented_at")]
        public DateTime? RetentedAt { get; set; }

        [DataMember(Name = "deadline_at")]
        public DateTime? DeadlineAt { get; set; }
    }
}

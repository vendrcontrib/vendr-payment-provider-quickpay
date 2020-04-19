using System.Runtime.Serialization;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [DataContract]
    public class Operation
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "amount")]
        public int Amount { get; set; }

        [DataMember(Name = "pending")]
        public bool Pending { get; set; }

        [DataMember(Name = "qp_status_code")]
        public string QuickPayStatusCode { get; set; }

        [DataMember(Name = "qp_status_msg")]
        public string QuickPayStatusMessage { get; set; }

        [DataMember(Name = "aq_status_code")]
        public string AcquirerStatusCode { get; set; }

        [DataMember(Name = "aq_status_msg")]
        public string AcquirerStatusMessage { get; set; }
    }
}

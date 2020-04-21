using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class QuickPayPayment
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("merchant_id")]
        public string MerchantId { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("operations")]
        public List<Operation> Operations { get; set; }

        [JsonProperty("metadata")]
        public MetaData MetaData { get; set; }

        [JsonProperty("link")]
        public PaymentLink Link { get; set; }

        [JsonProperty("test_mode")]
        public bool TestMode { get; set; }

        [JsonProperty("acquirer")]
        public string Acquirer { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }

        [JsonProperty("fee")]
        public int? Fee { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("retented_at")]
        public DateTime? RetentedAt { get; set; }

        [JsonProperty("deadline_at")]
        public DateTime? DeadlineAt { get; set; }
    }
}

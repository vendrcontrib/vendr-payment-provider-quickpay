﻿using Newtonsoft.Json;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api.Models
{
    public class QuickPayPaymentLinkRequest
    {
        /// <summary>
        /// Amount to authorize
        /// </summary>
        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Language
        /// </summary>
        [JsonProperty("language")]
        public string Language { get; set; }

        /// <summary>
        /// URL that cardholder is redirected to after authorize.
        /// </summary>
        [JsonProperty("continue_url")]
        public string ContinueUrl { get; set; }

        /// <summary>
        /// URL that cardholder is redirected to after cancelation.
        /// </summary>
        [JsonProperty("cancel_url")]
        public string CancelUrl { get; set; }

        /// <summary>
        /// Endpoint for async callback.
        /// </summary>
        [JsonProperty("callback_url")]
        public string CallbackUrl { get; set; }

        /// <summary>
        /// Limit payment methods.
        /// </summary>
        [JsonProperty("payment_methods")]
        public string PaymentMethods { get; set; }

        /// <summary>
        /// Add acquirer fee to amount. Default is merchant autofee.
        /// </summary>
        [JsonProperty("auto_fee")]
        public bool? AutoFee { get; set; }

        /// <summary>
        /// When true, payment is captured after authorization. Default is false.
        /// </summary>
        [JsonProperty("auto_capture")]
        public bool? AutoCapture { get; set; }

        /// <summary>
        /// Allow opening in iframe. Default is false.
        /// </summary>
        [JsonProperty("framed")]
        public bool? Framed { get; set; }
    }
}

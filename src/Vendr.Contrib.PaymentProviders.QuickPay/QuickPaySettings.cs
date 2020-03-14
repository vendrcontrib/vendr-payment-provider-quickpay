﻿using Vendr.Core.Web.PaymentProviders;

namespace Vendr.Contrib.PaymentProviders
{
    public class QuickPaySettings
    {
        [PaymentProviderSetting(Name = "Continue URL",
            Description = "The URL to continue to after this provider has done processing. eg: /continue/",
            SortOrder = 100)]
        public string ContinueUrl { get; set; }

        [PaymentProviderSetting(Name = "Cancel URL",
            Description = "The URL to return to if the payment attempt is canceled. eg: /cancel/",
            SortOrder = 200)]
        public string CancelUrl { get; set; }

        [PaymentProviderSetting(Name = "Error URL",
            Description = "The URL to return to if the payment attempt errors. eg: /error/",
            SortOrder = 300)]
        public string ErrorUrl { get; set; }

        [PaymentProviderSetting(Name = "API Key",
            Description = "API Key from the QuickPay administration portal.",
            SortOrder = 400)]
        public string ApiKey { get; set; }

        [PaymentProviderSetting(Name = "Merchant ID",
            Description = "Merchant ID supplied by QuickPay during registration.",
            SortOrder = 500)]
        public string MerchantId { get; set; }

        [PaymentProviderSetting(Name = "Agreement ID",
            Description = "Agreement ID supplied by QuickPay during registration.",
            SortOrder = 600)]
        public string AgreemendId { get; set; }

        [PaymentProviderSetting(Name = "Language",
            Description = "The language of the payment portal to display.",
            SortOrder = 900)]
        public string Lang { get; set; }

        [PaymentProviderSetting(Name = "Accepted Payment Methods",
            Description = "A comma separated list of Payment Methods to accept. To use negation just put a “!” in front the those you do not wish to accept.",
            SortOrder = 1000)]
        public string PaymentMethods { get; set; }

        [PaymentProviderSetting(Name = "Auto Fee",
            Description = "Flag indicating whether to automatically calculate and apply the fee from the acquirer.",
            SortOrder = 1100)]
        public bool AutoFee { get; set; }

        [PaymentProviderSetting(Name = "Auto Capture",
            Description = "Flag indicating whether to immediately capture the payment, or whether to just authorize the payment for later (manual) capture.",
            SortOrder = 1200)]
        public bool AutoCapture { get; set; }
    }
}

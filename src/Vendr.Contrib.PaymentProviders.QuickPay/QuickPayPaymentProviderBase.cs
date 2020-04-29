using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    public abstract class QuickPayPaymentProviderBase<TSettings> : PaymentProviderBase<TSettings>
            where TSettings : QuickPaySettingsBase, new()
    {
        public QuickPayPaymentProviderBase(VendrContext vendr)
            : base(vendr)
        { }

        public override string GetCancelUrl(OrderReadOnly order, TSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return settings.CancelUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, TSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, TSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return settings.ErrorUrl;
        }

        protected PaymentStatus GetPaymentStatus(Operation operation)
        {
            if (operation.Type == "authorize")
                return PaymentStatus.Authorized;

            if (operation.Type == "capture")
                return PaymentStatus.Captured;

            if (operation.Type == "refund")
                return PaymentStatus.Refunded;

            if (operation.Type == "cancel")
                return PaymentStatus.Cancelled;

            return PaymentStatus.Initialized;
        }

        protected string GetTransactionId(QuickPayPayment payment)
        {
            return payment?.Id.ToString();
        }

        protected string GetPaymentHash(string paymentId, string orderNumber, string currency, string amount)
        {
            return Base64Encode(paymentId + orderNumber + currency + amount);
        }

        protected QuickPayClientConfig GetQuickPayClientConfig(QuickPaySettingsBase settings)
        {
            var basicAuth = Base64Encode(":" + settings.ApiKey);

            return new QuickPayClientConfig
            {
                BaseUrl = "https://api.quickpay.net",
                Authorization = "Basic " + basicAuth
            };
        }
    }
}

using Vendr.Common.Logging;
using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;
using Vendr.Core.Api;
using Vendr.Core.Models;
using Vendr.Core.PaymentProviders;
using Vendr.Extensions;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    public abstract class QuickPayPaymentProviderBase<TSelf, TSettings> : PaymentProviderBase<TSettings>
        where TSelf : QuickPayPaymentProviderBase<TSelf, TSettings>
        where TSettings : QuickPaySettingsBase, new()
    {
        protected readonly ILogger<TSelf> _logger;

        public QuickPayPaymentProviderBase(VendrContext vendr,
            ILogger<TSelf> logger)
            : base(vendr)
        {
            _logger = logger;
        }

        public override string GetCancelUrl(PaymentProviderContext<TSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("settings");
            ctx.Settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return ctx.Settings.CancelUrl;
        }

        public override string GetContinueUrl(PaymentProviderContext<TSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("settings");
            ctx.Settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return ctx.Settings.ContinueUrl;
        }

        public override string GetErrorUrl(PaymentProviderContext<TSettings> ctx)
        {
            ctx.Settings.MustNotBeNull("settings");
            ctx.Settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return ctx.Settings.ErrorUrl;
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

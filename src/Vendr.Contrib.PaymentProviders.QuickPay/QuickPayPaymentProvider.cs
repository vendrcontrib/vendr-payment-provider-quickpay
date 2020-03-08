using System;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Flurl;
using Flurl.Http;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;

namespace Vendr.Contrib.PaymentProviders
{
    [PaymentProvider("quickpay-v10", "QuickPay V10", "QuickPay V10 payment provider", Icon = "icon-invoice")]
    public class QuickPayPaymentProvider : PaymentProviderBase<QuickPaySettings>
    {
        public QuickPayPaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool FinalizeAtContinueUrl => true;

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, QuickPaySettings settings)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currency.Code.ToUpperInvariant()))
            {
                throw new Exception("Currency must a valid ISO 4217 currency code: " + currency.Name);
            }

            try
            {
                // https://learn.quickpay.net/tech-talk/guides/payments/#introduction-to-payments

                var basicAuth = Convert.ToBase64String(Encoding.Default.GetBytes(":" + settings.ApiKey));

                var response = $"https://api.quickpay.net/payments"
                    .WithHeader("Accept-Version", "v10")
                    .WithHeader("Authorization", "Basic " + basicAuth)
                    .WithHeader("Content-Type", "application/json")
                    .PostUrlEncodedAsync(new
                    {
                        order_id = order.OrderNumber,
                        currency = currency.Code
                    })
                    .ReceiveString();

            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayPaymentProvider>(ex, "QuickPay - error creating payment.");
            }

            return new PaymentFormResult()
            {
                Form = new PaymentForm(continueUrl, FormMethod.Post)
            };
        }

        public override string GetCancelUrl(OrderReadOnly order, QuickPaySettings settings)
        {
            return string.Empty;
        }

        public override string GetErrorUrl(OrderReadOnly order, QuickPaySettings settings)
        {
            return string.Empty;
        }

        public override string GetContinueUrl(OrderReadOnly order, QuickPaySettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, QuickPaySettings settings)
        {
            return new CallbackResult
            {
                TransactionInfo = new TransactionInfo
                {
                    AmountAuthorized = order.TotalPrice.Value.WithTax,
                    TransactionFee = 0m,
                    TransactionId = Guid.NewGuid().ToString("N"),
                    PaymentStatus = PaymentStatus.Authorized
                }
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Flurl;
using Flurl.Http;
using Vendr.Contrib.PaymentProviders.QuickPay;
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

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[]{
            new TransactionMetaDataDefinition("quickPayPaymentId", "QuickPay Payment ID"),
            new TransactionMetaDataDefinition("quickPayPaymentHash", "QuickPay Payment Hash")
        };

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, QuickPaySettings settings)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currency.Code.ToUpperInvariant()))
            {
                throw new Exception("Currency must a valid ISO 4217 currency code: " + currency.Name);
            }

            string paymentFormLink = string.Empty;
            var orderAmount = (order.TotalPrice.Value.WithTax * 100M).ToString("0", CultureInfo.InvariantCulture);

            QuickPayPaymentDto payment = null;
            string quickPayPaymentHash = string.Empty;

            try
            {
                // https://learn.quickpay.net/tech-talk/guides/payments/#introduction-to-payments

                var basicAuth = Convert.ToBase64String(Encoding.Default.GetBytes(":" + settings.ApiKey));

                payment = $"https://api.quickpay.net/payments"
                    .WithHeader("Accept-Version", "v10")
                    .WithHeader("Authorization", "Basic " + basicAuth)
                    .WithHeader("Content-Type", "application/json")
                    .PostJsonAsync(new
                    {
                        order_id = order.OrderNumber,
                        currency = currency.Code
                    })
                    .ReceiveJson<QuickPayPaymentDto>().Result;

                // Set "quickPaymentId" order property (payment id)
                // Set "quickPayHash" order property (base64 hash of payment id + order number)

                var paymentLink = $"https://api.quickpay.net/payments/{payment.Id}/link"
                    .WithHeader("Accept-Version", "v10")
                    .WithHeader("Authorization", "Basic " + basicAuth)
                    .WithHeader("Content-Type", "application/json")
                    .PutJsonAsync(new
                    {
                        amount = orderAmount
                    })
                    .ReceiveJson<QuickPayPaymentLinkDto>().Result;

                quickPayPaymentHash = Base64Encode(payment.Id + order.OrderNumber);

                //var test = new ApiResult()
                //{
                //    TransactionInfo = new TransactionInfoUpdate()
                //    {
                //        TransactionId = GetTransactionId(payment),
                //        PaymentStatus = GetPaymentStatus(payment)
                //    },
                //    MetaData = new Dictionary<string, string>
                //    {
                //        { "quickPayPaymentId", payment.Id.ToString() },
                //        { "quickPayPaymentHash", hash }
                //    }
                //};

                //using (var uow = Vendr.Uow.Create())
                //{
                //    var hash = Base64Encode(payment.Id + order.OrderNumber);

                //    var properties = new Dictionary<string, string>()
                //    {
                //        { "quickPayPaymentId", payment.Id },
                //        { "quickPayPaymentHash", hash }
                //    };

                //    var basket = order.AsWritable(uow)
                //                      .SetProperties(properties);

                //    Vendr.Services.OrderService.SaveOrder(basket);

                //    uow.Complete();
                //}

                paymentFormLink = paymentLink.Url;
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayPaymentProvider>(ex, "QuickPay - error creating payment.");
            }

            return new PaymentFormResult()
            {
                MetaData = new Dictionary<string, string>
                {
                    { "quickPayPaymentId", payment?.Id.ToString() },
                    { "quickPayPaymentHash", quickPayPaymentHash }
                },
                Form = new PaymentForm(paymentFormLink, FormMethod.Get)
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

        protected PaymentStatus GetPaymentStatus(QuickPayPaymentDto payment)
        {
            // Possible Payment statuses:
            // - initial
            // - pending
            // - new
            // - rejected
            // - processed

            if (payment.State == "rejected")
                return PaymentStatus.Error;

            if (payment.State == "pending")
                return PaymentStatus.PendingExternalSystem;

            return PaymentStatus.Initialized;
        }
        protected string GetTransactionId(QuickPayPaymentDto payment)
        {
            return payment?.Id.ToString();
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using Vendr.Contrib.PaymentProviders.QuickPay;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.PaymentProviders.QuickPay;

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

            var paymentMethods = settings.PaymentMethods?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            // Parse language - default language is English.
            Enum.TryParse(settings.Lang, true, out QuickPayLang lang);

            QuickPayPaymentDto payment = null;
            string quickPayPaymentHash = string.Empty;

            var quickPayPaymentId = order.Properties["quickPayPaymentId"];

            //if (string.IsNullOrEmpty(quickPayPaymentId))
            //{
                try
                {
                    // https://learn.quickpay.net/tech-talk/guides/payments/#introduction-to-payments

                    var basicAuth = Base64Encode(":" + settings.ApiKey);

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
                            amount = orderAmount,
                            language = lang.ToString(),
                            continue_url = continueUrl,
                            cancel_url = cancelUrl,
                            callback_url = callbackUrl,
                            payment_methods = (paymentMethods != null && paymentMethods.Length > 0 ? string.Join(",", paymentMethods) : null),
                            auto_fee = settings.AutoFee,
                            auto_capture = settings.AutoCapture
                        })
                        .ReceiveJson<PaymentLinkUrl>().Result;

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

                    paymentFormLink = paymentLink.Url;
                }
                catch (Exception ex)
                {
                    Vendr.Log.Error<QuickPayPaymentProvider>(ex, "QuickPay - error creating payment.");
                }
            //}
            //else
            //{
            //    // Get payment link from order properties.
            //    paymentFormLink = string.Empty;
            //}

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
            settings.MustNotBeNull("settings");
            settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return settings.CancelUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, QuickPaySettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return settings.ErrorUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, QuickPaySettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, QuickPaySettings settings)
        {
            try
            {
                if (ValidateChecksum(request, settings.PrivateKey))
                {
                    var payment = ReadCallbackBody(request);

                    // Get operations to check if payment has been approved
                    var operation = payment.Operations.LastOrDefault();

                    // Check if payment has been approved
                    if (operation != null)
                    {
                        var totalAmount = operation.Amount;

                        //var operations = callbackObject.Operations.LastOrDefault();
                        // Check if payment has been approved
                        //return operations != null && (operations.qp_status_code == "000" || operations.qp_status_code == "20000") && operations.qp_status_msg.ToLower() == "approved";

                        return new CallbackResult
                        {
                            TransactionInfo = new TransactionInfo
                            {
                                AmountAuthorized = totalAmount / 100M,
                                TransactionId = GetTransactionId(payment),
                                PaymentStatus = GetPaymentStatus(payment)
                            }
                        };
                    }
                }
                else
                {
                    Vendr.Log.Warn<QuickPayPaymentProvider>($"QuickPay [{order.OrderNumber}] - Checksum validation failed");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayPaymentProvider>(ex, "QuickPay - ProcessCallback");
            }

            return CallbackResult.Empty;
        }

        protected PaymentStatus GetPaymentStatus(QuickPayPaymentDto payment)
        {
            // Possible Payment statuses:
            // - initial
            // - pending
            // - new
            // - rejected
            // - processed

            if (payment.State == "new")
                return PaymentStatus.Authorized;

            if (payment.State == "processed")
                return PaymentStatus.Captured;

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

        public QuickPayPaymentDto ReadCallbackBody(HttpRequestBase request)
        {
            request.InputStream.Position = 0;

            // Get quickpay callback body text - See parameters: http://tech.quickpay.net/api/callback/
            var bodyStream = new StreamReader(request.InputStream);
            bodyStream.BaseStream.Seek(0, SeekOrigin.Begin);

            // Get body text
            var bodyText = bodyStream.ReadToEnd();
            request.InputStream.Position = 0;

            // Deserialize json body text 
            return JsonConvert.DeserializeObject<QuickPayPaymentDto>(bodyText);
        }

        private bool ValidateChecksum(HttpRequestBase request, string privateAccountKey)
        {
            var requestCheckSum = request.Headers["QuickPay-Checksum-Sha256"];

            if (string.IsNullOrEmpty(requestCheckSum)) return false;

            var inputStream = request.InputStream;
            var bytes = new byte[inputStream.Length];
            request.InputStream.Position = 0;
            request.InputStream.Read(bytes, 0, bytes.Length);
            request.InputStream.Position = 0;
            var content = Encoding.UTF8.GetString(bytes);
            var calculatedChecksum = Checksum(content, privateAccountKey);

            return requestCheckSum.Equals(calculatedChecksum);
        }

        private string Checksum(string content, string privateKey)
        {
            var s = new StringBuilder();
            var e = Encoding.UTF8;
            var bytes = e.GetBytes(privateKey);

            using (var hmac = new HMACSHA256(bytes))
            {
                var b = hmac.ComputeHash(e.GetBytes(content));

                foreach (var t in b)
                {
                    s.Append(t.ToString("x2"));
                }
            }

            return s.ToString();
        }
    }
}

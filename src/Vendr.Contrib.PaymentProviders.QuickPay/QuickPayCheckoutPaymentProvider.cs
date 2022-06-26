using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using Vendr.Contrib.PaymentProviders.QuickPay.Api;
using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.Extensions;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [PaymentProvider("quickpay-v10-checkout", "QuickPay V10", "QuickPay V10 payment provider for one time payments")]
    public class QuickPayCheckoutPaymentProvider : QuickPayPaymentProviderBase<QuickPayCheckoutSettings>
    {
        public QuickPayCheckoutPaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool CanRefundPayments => true;
        public override bool CanFetchPaymentStatus => true;

        public override bool FinalizeAtContinueUrl => true;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[] {
            new TransactionMetaDataDefinition("quickPayOrderId", "QuickPay Order ID"),
            new TransactionMetaDataDefinition("quickPayPaymentId", "QuickPay Payment ID"),
            new TransactionMetaDataDefinition("quickPayPaymentHash", "QuickPay Payment Hash")
        };

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, QuickPayCheckoutSettings settings)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
            {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            string paymentFormLink = string.Empty;
            var orderAmount = AmountToMinorUnits(order.TransactionAmount.Value).ToString("0", CultureInfo.InvariantCulture);

            var paymentMethods = settings.PaymentMethods?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            // Parse language - default language is English.
            Enum.TryParse(settings.Lang, true, out QuickPayLang lang);

            var quickPayOrderId = order.Properties["quickPayOrderId"]?.Value;
            var quickPayPaymentId = order.Properties["quickPayPaymentId"]?.Value;
            var quickPayPaymentHash = order.Properties["quickPayPaymentHash"]?.Value ?? string.Empty;
            var quickPayPaymentLinkHash = order.Properties["quickPayPaymentLinkHash"]?.Value ?? string.Empty;

            if (quickPayPaymentHash != GetPaymentHash(quickPayPaymentId, order.OrderNumber, currencyCode, orderAmount))
            {
                try
                {
                    // https://learn.quickpay.net/tech-talk/guides/payments/#introduction-to-payments

                    var clientConfig = GetQuickPayClientConfig(settings);
                    var client = new QuickPayClient(clientConfig);

                    var reference = order.OrderNumber;

                    // QuickPay has a limit of order id between 4-20 characters.
                    if (reference.Length > 20)
                    {                       
                        var store = Vendr.Services.StoreService.GetStore(order.StoreId);
                        var orderNumberTemplate = store.OrderNumberTemplate;

                        // If the ordernumber template is manipulatet, we then remove everything other than the VENDR automated ordernumber.
                        if (orderNumberTemplate.Equals("{0}") == false)
                        {
                            var index = orderNumberTemplate.IndexOf("{0}");
                            var prefix = orderNumberTemplate.Substring(0, index);
                            var suffix = orderNumberTemplate.Substring(index + 3, orderNumberTemplate.Length - (index + 3));

                            if (orderNumberTemplate.StartsWith("{0}"))
                            {
                                // Trim suffix
                                reference = reference.Substring(index, reference.Length - suffix.Length);
                            }
                            else if (orderNumberTemplate.EndsWith("{0}"))
                            {
                                // Trim prefix
                                reference = reference.Substring(prefix.Length - 1);
                            }
                            else if (orderNumberTemplate.Contains("{0}"))
                            {
                                // Trim prefix & suffix
                                reference = reference.Substring(prefix.Length - 1, reference.Length - suffix.Length);
                            }
                        }
                    }

                    var metaData = new Dictionary<string, string>
                    {
                        { "orderReference", order.GenerateOrderReference() },
                        { "orderId", order.Id.ToString("D") },
                        { "orderNumber", order.OrderNumber }
                    };

                    quickPayOrderId = reference;

                    var payment = client.CreatePayment(new
                    {
                        order_id = quickPayOrderId,
                        currency = currencyCode,
                        Variables = metaData
                    });

                    quickPayPaymentId = GetTransactionId(payment);

                    var paymentLink = client.CreatePaymentLink(payment.Id.ToString(), new
                    {
                        amount = orderAmount,
                        language = lang.ToString(),
                        continue_url = continueUrl,
                        cancel_url = cancelUrl,
                        callback_url = callbackUrl,
                        payment_methods = (paymentMethods != null && paymentMethods.Length > 0 ? string.Join(",", paymentMethods) : null),
                        auto_fee = settings.AutoFee,
                        auto_capture = settings.AutoCapture
                    });

                    paymentFormLink = paymentLink.Url;

                    quickPayPaymentHash = GetPaymentHash(payment.Id.ToString(), order.OrderNumber, currencyCode, orderAmount);
                    quickPayPaymentLinkHash = Base64Encode(paymentFormLink);
                }
                catch (Exception ex)
                {
                    Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - error creating payment.");
                }
            }
            else
            {
                // Get payment link from order properties.
                paymentFormLink = Base64Decode(quickPayPaymentLinkHash);
            }

            return new PaymentFormResult()
            {
                MetaData = new Dictionary<string, string>
                {
                    { "quickPayOrderId", quickPayOrderId },
                    { "quickPayPaymentId", quickPayPaymentId },
                    { "quickPayPaymentHash", quickPayPaymentHash },
                    { "quickPayPaymentLinkHash", quickPayPaymentLinkHash }
                },
                Form = new PaymentForm(paymentFormLink, FormMethod.Get)
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, QuickPayCheckoutSettings settings)
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

                        if (operation.QuickPayStatusCode == "20000" || operation.AcquirerStatusCode == "000")
                        {
                            var paymentStatus = GetPaymentStatus(operation);

                            return new CallbackResult
                            {
                                TransactionInfo = new TransactionInfo
                                {
                                    AmountAuthorized = AmountFromMinorUnits(totalAmount),
                                    TransactionId = GetTransactionId(payment),
                                    PaymentStatus = paymentStatus
                                }
                            };
                        }
                        else
                        {
                            Vendr.Log.Warn<QuickPayCheckoutPaymentProvider>($"QuickPay [{order.OrderNumber}] - Payment not approved. QuickPay status code: {operation.QuickPayStatusCode} ({operation.QuickPayStatusMessage}). Acquirer status code: {operation.AcquirerStatusCode} ({operation.AcquirerStatusMessage}).");
                        }   
                    }
                }
                else
                {
                    Vendr.Log.Warn<QuickPayCheckoutPaymentProvider>($"QuickPay [{order.OrderNumber}] - Checksum validation failed");
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - ProcessCallback");
            }

            return CallbackResult.Empty;
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, QuickPayCheckoutSettings settings)
        {
            // GET: /payments/{id}

            try
            {
                var id = order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(settings);
                var client = new QuickPayClient(clientConfig);

                var payment = client.GetPayment(id);

                Operation lastCompletedOperation = payment.Operations.LastOrDefault(o => !o.Pending && o.QuickPayStatusCode == "20000");

                if (lastCompletedOperation != null)
                {
                    var paymentStatus = GetPaymentStatus(lastCompletedOperation);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = GetTransactionId(payment),
                            PaymentStatus = paymentStatus
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CancelPayment(OrderReadOnly order, QuickPayCheckoutSettings settings)
        {
            // POST: /payments/{id}/cancel

            try
            {
                var id = order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(settings);
                var client = new QuickPayClient(clientConfig);

                var payment = client.CancelPayment(id);

                Operation lastCompletedOperation = payment.Operations.LastOrDefault(o => !o.Pending && o.QuickPayStatusCode == "20000");

                if (lastCompletedOperation != null)
                {
                    var paymentStatus = GetPaymentStatus(lastCompletedOperation);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = GetTransactionId(payment),
                            PaymentStatus = paymentStatus
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - CancelPayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CapturePayment(OrderReadOnly order, QuickPayCheckoutSettings settings)
        {
            // POST: /payments/{id}/capture

            try
            {
                var id = order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(settings);
                var client = new QuickPayClient(clientConfig);

                var payment = client.CapturePayment(id, new
                {
                    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                });

                Operation lastCompletedOperation = payment.Operations.LastOrDefault(o => !o.Pending && o.QuickPayStatusCode == "20000");

                if (lastCompletedOperation != null)
                {
                    var paymentStatus = GetPaymentStatus(lastCompletedOperation);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = GetTransactionId(payment),
                            PaymentStatus = paymentStatus
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult RefundPayment(OrderReadOnly order, QuickPayCheckoutSettings settings)
        {
            // POST: /payments/{id}/refund

            try
            {
                var id = order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(settings);
                var client = new QuickPayClient(clientConfig);

                var payment = client.RefundPayment(id, new
                {
                    amount = AmountToMinorUnits(order.TransactionInfo.AmountAuthorized.Value)
                });

                Operation lastCompletedOperation = payment.Operations.LastOrDefault(o => !o.Pending && o.QuickPayStatusCode == "20000");

                if (lastCompletedOperation != null)
                {
                    var paymentStatus = GetPaymentStatus(lastCompletedOperation);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = GetTransactionId(payment),
                            PaymentStatus = paymentStatus
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<QuickPayCheckoutPaymentProvider>(ex, "QuickPay - RefundPayment");
            }

            return ApiResult.Empty;
        }

        public QuickPayPayment ReadCallbackBody(HttpRequestBase request)
        {
            request.InputStream.Position = 0;

            // Get quickpay callback body text - See parameters: http://tech.quickpay.net/api/callback/
            var bodyStream = new StreamReader(request.InputStream);
            bodyStream.BaseStream.Seek(0, SeekOrigin.Begin);

            // Get body text
            var bodyText = bodyStream.ReadToEnd();
            request.InputStream.Position = 0;

            // Deserialize json body text 
            return JsonConvert.DeserializeObject<QuickPayPayment>(bodyText);
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

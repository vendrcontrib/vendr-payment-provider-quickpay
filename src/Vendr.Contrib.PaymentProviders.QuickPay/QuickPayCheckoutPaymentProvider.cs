using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Vendr.Common.Logging;
using Vendr.Contrib.PaymentProviders.QuickPay.Api;
using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;
using Vendr.Core.Api;
using Vendr.Core.Models;
using Vendr.Core.PaymentProviders;
using Vendr.Extensions;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [PaymentProvider("quickpay-v10-checkout", "QuickPay V10", "QuickPay V10 payment provider for one time payments")]
    public class QuickPayCheckoutPaymentProvider : QuickPayPaymentProviderBase<QuickPayCheckoutPaymentProvider, QuickPayCheckoutSettings>
    {
        public QuickPayCheckoutPaymentProvider(VendrContext vendr, ILogger<QuickPayCheckoutPaymentProvider> logger)
            : base(vendr, logger)
        { }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool CanRefundPayments => true;
        public override bool CanFetchPaymentStatus => true;

        public override bool FinalizeAtContinueUrl => false;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[]
        {
            new TransactionMetaDataDefinition("quickPayOrderId", "QuickPay Order ID"),
            new TransactionMetaDataDefinition("quickPayPaymentId", "QuickPay Payment ID"),
            new TransactionMetaDataDefinition("quickPayPaymentHash", "QuickPay Payment Hash")
        };

        public override async Task<PaymentFormResult> GenerateFormAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            var currency = Vendr.Services.CurrencyService.GetCurrency(ctx.Order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
            {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            string paymentFormLink = string.Empty;
            var orderAmount = AmountToMinorUnits(ctx.Order.TransactionAmount.Value);

            var paymentMethods = ctx.Settings.PaymentMethods?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(s => s.Trim())
                   .ToArray();

            // Parse language - default language is English.
            Enum.TryParse(ctx.Settings.Lang, true, out QuickPayLang lang);

            var quickPayOrderId = ctx.Order.Properties["quickPayOrderId"]?.Value;
            var quickPayPaymentId = ctx.Order.Properties["quickPayPaymentId"]?.Value;
            var quickPayPaymentHash = ctx.Order.Properties["quickPayPaymentHash"]?.Value ?? string.Empty;
            var quickPayPaymentLinkHash = ctx.Order.Properties["quickPayPaymentLinkHash"]?.Value ?? string.Empty;

            if (quickPayPaymentHash != GetPaymentHash(quickPayPaymentId, ctx.Order.OrderNumber, currencyCode, orderAmount))
            {
                try
                {
                    // https://learn.quickpay.net/tech-talk/guides/payments/#introduction-to-payments

                    var clientConfig = GetQuickPayClientConfig(ctx.Settings);
                    var client = new QuickPayClient(clientConfig);
                    
                    var reference = ctx.Order.OrderNumber;

                    // QuickPay has a limit of order id between 4-20 characters.
                    if (reference.Length > 20)
                    {
                        var store = Vendr.Services.StoreService.GetStore(ctx.Order.StoreId);
                        var orderNumberTemplate = store.OrderNumberTemplate;

                        // If the order number template is not equals Vendr generated order number, we need to decide whether to trim prefix, suffix or both.
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
                                reference = reference.Substring(prefix.Length);
                            }
                            else if (orderNumberTemplate.Contains("{0}"))
                            {
                                // Trim prefix & suffix
                                reference = reference.Substring(prefix.Length, reference.Length - prefix.Length - suffix.Length);
                            }
                        }
                    }

                    var metaData = new Dictionary<string, string>
                    {
                        { "orderReference", ctx.Order.GenerateOrderReference() },
                        { "orderId", ctx.Order.Id.ToString("D") },
                        { "orderNumber", ctx.Order.OrderNumber }
                    };

                    quickPayOrderId = reference;

                    var payment = await client.CreatePaymentAsync(new QuickPayPaymentRequest
                    {
                        OrderId = quickPayOrderId,
                        Currency = currencyCode,
                        Variables = metaData
                    });
                    
                    quickPayPaymentId = GetTransactionId(payment);

                    var paymentLink = await client.CreatePaymentLinkAsync(payment.Id.ToString(), new QuickPayPaymentLinkRequest
                    {
                        Amount = orderAmount,
                        Language = lang.ToString(),
                        ContinueUrl = ctx.Urls.ContinueUrl,
                        CancelUrl = ctx.Urls.CancelUrl,
                        CallbackUrl = ctx.Urls.CallbackUrl.Replace("https://localhost:44360", "https://0a01-188-228-1-122.ngrok-free.app"),
                        PaymentMethods = paymentMethods?.Length > 0 ? string.Join(",", paymentMethods) : null,
                        AutoFee = ctx.Settings.AutoFee,
                        AutoCapture = ctx.Settings.AutoCapture,
                        Framed = ctx.Settings.Framed
                    });

                    paymentFormLink = paymentLink.Url;

                    quickPayPaymentHash = GetPaymentHash(payment.Id.ToString(), ctx.Order.OrderNumber, currencyCode, orderAmount);
                    quickPayPaymentLinkHash = Base64Encode(paymentFormLink);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "QuickPay - error creating payment.");
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
                Form = new PaymentForm(paymentFormLink, PaymentFormMethod.Get)
            };
        }

        public override async Task<CallbackResult> ProcessCallbackAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            try
            {
                if (await ValidateChecksum(ctx.Request, ctx.Settings.PrivateKey))
                {
                    var payment = await ParseCallbackAsync(ctx.Request);

                    if (VerifyOrder(ctx.Order, payment))
                    {
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
                                _logger.Warn($"QuickPay [{ctx.Order.OrderNumber}] - Payment not approved. QuickPay status code: {operation.QuickPayStatusCode} ({operation.QuickPayStatusMessage}). Acquirer status code: {operation.AcquirerStatusCode} ({operation.AcquirerStatusMessage}).");
                            }
                        }
                    }
                    else
                    {
                        _logger.Warn($"QuickPay [{ctx.Order.OrderNumber}] - Couldn't verify the order");
                    }
                }
                else
                {
                    _logger.Warn($"QuickPay [{ctx.Order.OrderNumber}] - Checksum validation failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "QuickPay - ProcessCallback");
            }

            return CallbackResult.Empty;
        }

        private bool VerifyOrder(OrderReadOnly order, QuickPayPayment payment)
        {
            if (payment.Variables.Count > 0 && 
                payment.Variables.TryGetValue("orderReference", out string orderReference))
            {
                if (order.GenerateOrderReference() == orderReference)
                {
                    return true;
                }
            }
            else
            {
                if (order.Properties["quickPayOrderId"]?.Value == payment.OrderId)
                {
                    return true;
                }
            }

            return false;
        }

        public override async Task<ApiResult> FetchPaymentStatusAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            // GET: /payments/{id}

            try
            {
                var id = ctx.Order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(ctx.Settings);
                var client = new QuickPayClient(clientConfig);

                var payment = await client.GetPaymentAsync(id);

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
                _logger.Error(ex, "QuickPay - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CancelPaymentAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            // POST: /payments/{id}/cancel

            try
            {
                var id = ctx.Order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(ctx.Settings);
                var client = new QuickPayClient(clientConfig);

                var payment = await client.CancelPaymentAsync(id);

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
                _logger.Error(ex, "QuickPay - CancelPayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CapturePaymentAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            // POST: /payments/{id}/capture

            try
            {
                var id = ctx.Order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(ctx.Settings);
                var client = new QuickPayClient(clientConfig);

                var payment = await client.CapturePaymentAsync(id, new
                {
                    amount = AmountToMinorUnits(ctx.Order.TransactionInfo.AmountAuthorized.Value)
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
                _logger.Error(ex, "QuickPay - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> RefundPaymentAsync(PaymentProviderContext<QuickPayCheckoutSettings> ctx)
        {
            // POST: /payments/{id}/refund

            try
            {
                var id = ctx.Order.TransactionInfo.TransactionId;

                var clientConfig = GetQuickPayClientConfig(ctx.Settings);
                var client = new QuickPayClient(clientConfig);

                var payment = await client.RefundPaymentAsync(id, new
                {
                    amount = AmountToMinorUnits(ctx.Order.TransactionInfo.AmountAuthorized.Value)
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
                _logger.Error(ex, "QuickPay - RefundPayment");
            }

            return ApiResult.Empty;
        }

        public async Task<QuickPayPayment> ParseCallbackAsync(HttpRequestMessage request)
        {
            using (var stream = await request.Content.ReadAsStreamAsync())
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                // Get quickpay callback body text - See parameters: https://learn.quickpay.net/tech-talk/api/callback/

                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();

                    // Deserialize json body text 
                    return JsonConvert.DeserializeObject<QuickPayPayment>(json);
                }
            }
        }

        private async Task<bool> ValidateChecksum(HttpRequestMessage request, string privateAccountKey)
        {
            var json = await request.Content.ReadAsStringAsync();
            var checkSum = request.Headers.GetValues("QuickPay-Checksum-Sha256").FirstOrDefault();

            if (string.IsNullOrEmpty(checkSum)) return false;

            var calculatedChecksum = Checksum(json, privateAccountKey);

            return checkSum.Equals(calculatedChecksum);
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

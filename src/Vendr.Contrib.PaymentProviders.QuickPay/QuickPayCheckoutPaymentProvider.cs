using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vendr.Common.Logging;
using Vendr.Contrib.PaymentProviders.QuickPay.Api;
using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;
using Vendr.Core.Api;
using Vendr.Core.Models;
using Vendr.Core.PaymentProviders;

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

        public override bool FinalizeAtContinueUrl => true;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new[]{
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
                    
                    var orderReference = ctx.Order.OrderNumber;

                    // QuickPay has a limit of order id between 4-20 characters.
                    if (orderReference.Length > 20)
                    {
                        var store = Vendr.Services.StoreService.GetStore(ctx.Order.StoreId);
                        var orderNumberTemplate = store.OrderNumberTemplate;

                        if (orderNumberTemplate.Equals("{0}") == false)
                        {
                            int placeholderLength = 3;

                            if (orderNumberTemplate.StartsWith("{0}"))
                            {
                                var start = orderNumberTemplate.IndexOf("{0}") + placeholderLength;
                                var valueToTrim = orderNumberTemplate.Substring(start, orderNumberTemplate.Length - placeholderLength);
                                orderReference = orderReference.TrimEnd(valueToTrim.ToCharArray());
                            }
                            else if (orderNumberTemplate.EndsWith("{0}"))
                            {
                                var valueToTrim = orderNumberTemplate.Substring(0, orderNumberTemplate.Length - placeholderLength);
                                orderReference = orderReference.TrimStart(valueToTrim.ToCharArray());
                            }
                            else if (orderNumberTemplate.Contains("{0}"))
                            {
                                var valueToTrim = orderNumberTemplate.Split("{0}".ToCharArray()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
                                orderReference = orderReference.TrimStart(valueToTrim[0].ToCharArray()).TrimEnd(valueToTrim[1].ToCharArray());
                            }
                        }
                    }

                    var payment = await client.CreatePaymentAsync(new QuickPayPaymentRequest
                    {
                        OrderId = orderReference,
                        Currency = currencyCode
                    });

                    quickPayPaymentId = GetTransactionId(payment);

                    var paymentLink = await client.CreatePaymentLinkAsync(payment.Id.ToString(), new QuickPayPaymentLinkRequest
                    {
                        Amount = orderAmount,
                        Language = lang.ToString(),
                        ContinueUrl = ctx.Urls.ContinueUrl,
                        CancelUrl = ctx.Urls.CancelUrl,
                        CallbackUrl = ctx.Urls.CallbackUrl,
                        PaymentMethods = paymentMethods?.Length > 0 ? string.Join(",", paymentMethods) : null,
                        AutoFee = ctx.Settings.AutoFee,
                        AutoCapture = ctx.Settings.AutoCapture
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
                    _logger.Warn($"QuickPay [{ctx.Order.OrderNumber}] - Checksum validation failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "QuickPay - ProcessCallback");
            }

            return CallbackResult.Empty;
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

                // Get quickpay callback body text - See parameters: http://tech.quickpay.net/api/callback/

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
            var requestCheckSum = request.Headers.GetValues("QuickPay-Checksum-Sha256").FirstOrDefault();

            if (string.IsNullOrEmpty(requestCheckSum)) return false;

            using (var stream = await request.Content.ReadAsStreamAsync())
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                // Get quickpay callback body text - See parameters: http://tech.quickpay.net/api/callback/

                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();

                    var calculatedChecksum = Checksum(json, privateAccountKey);

                    return requestCheckSum.Equals(calculatedChecksum);
                }
            }
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

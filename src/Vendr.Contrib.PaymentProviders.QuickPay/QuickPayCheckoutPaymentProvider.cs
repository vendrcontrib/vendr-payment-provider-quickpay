﻿using System;
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
            var orderAmount = AmountToMinorUnits(ctx.Order.TransactionAmount.Value).ToString("0", CultureInfo.InvariantCulture);

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

                    var payment = client.CreatePayment(new
                    {
                        order_id = ctx.Order.OrderNumber,
                        currency = currencyCode
                    });

                    quickPayPaymentId = GetTransactionId(payment);

                    var paymentLink = client.CreatePaymentLink(payment.Id.ToString(), new
                    {
                        amount = orderAmount,
                        language = lang.ToString(),
                        continue_url = ctx.Urls.ContinueUrl,
                        cancel_url = ctx.Urls.CancelUrl,
                        callback_url = ctx.Urls.CallbackUrl,
                        payment_methods = (paymentMethods != null && paymentMethods.Length > 0 ? string.Join(",", paymentMethods) : null),
                        auto_fee = ctx.Settings.AutoFee,
                        auto_capture = ctx.Settings.AutoCapture
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
                if (ValidateChecksum(ctx.Request, ctx.Settings.PrivateKey))
                {
                    var payment = ReadCallbackBody(ctx.Request);

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

                var payment = client.CapturePayment(id, new
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

                var payment = client.RefundPayment(id, new
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

        private bool ValidateChecksum(HttpRequestMessage request, string privateAccountKey)
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

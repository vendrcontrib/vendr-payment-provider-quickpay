using Flurl.Http;
using Flurl.Http.Configuration;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Vendr.Contrib.PaymentProviders.QuickPay.Api.Models;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Api
{
    public class QuickPayClient
    {
        private QuickPayClientConfig _config;

        public QuickPayClient(QuickPayClientConfig config)
        {
            _config = config;
        }

        public async Task<QuickPayPayment> CreatePaymentAsync(object data)
        {
            return await Request("/payments", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        public async Task<PaymentLinkUrl> CreatePaymentLinkAsync(string paymentId, object data)
        {
            return await Request($"/payments/{paymentId}/link", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PutJsonAsync(data)
                .ReceiveJson<PaymentLinkUrl>());
        }

        public async Task<QuickPayPayment> GetPaymentAsync(string paymentId)
        {
            return await Request($"/payments/{paymentId}", (req) => req
                .GetJsonAsync<QuickPayPayment>());
        }

        public async Task<QuickPayPayment> CancelPaymentAsync(string paymentId)
        {
            return await Request($"/payments/{paymentId}/cancel", (req) => req
                .WithHeader("Content-Type", "application/json")
                .SetQueryParam("synchronized", string.Empty)
                .PostJsonAsync(null)
                .ReceiveJson<QuickPayPayment>());
        }

        public async Task<QuickPayPayment> CapturePaymentAsync(string paymentId, object data)
        {
            return await Request($"/payments/{paymentId}/capture", (req) => req
                .WithHeader("Content-Type", "application/json")
                .SetQueryParam("synchronized", string.Empty)
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        public async Task<QuickPayPayment> RefundPaymentAsync(string paymentId, object data)
        {
            return await Request($"/payments/{paymentId}/refund", (req) => req
                .WithHeader("Content-Type", "application/json")
                .SetQueryParam("synchronized", string.Empty)
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        private async Task<TResult> Request<TResult>(string url, Func<IFlurlRequest, Task<TResult>> func)
        {
            var result = default(TResult);

            try
            {
                var req = new FlurlRequest(_config.BaseUrl + url)
                        .ConfigureRequest(x =>
                        {
                            var jsonSettings = new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                DefaultValueHandling = DefaultValueHandling.Include,
                                MissingMemberHandling = MissingMemberHandling.Ignore
                            };
                            x.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);
                        })
                        .WithHeader("Accept-Version", "v10")
                        .WithHeader("Authorization", _config.Authorization);

                result = await func.Invoke(req);
            }
            catch (FlurlHttpException ex)
            {
                throw;
            }

            return result;
        }
    }
}

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

        public QuickPayPayment CreatePayment(object data)
        {
            return Request("/payments", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        public PaymentLinkUrl CreatePaymentLink(string paymentId, object data)
        {
            return Request($"/payments/{paymentId}/link", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PutJsonAsync(data)
                .ReceiveJson<PaymentLinkUrl>());
        }

        public QuickPayPayment GetPayment(string paymentId)
        {
            return Request($"/payments/{paymentId}", (req) => req
                .GetJsonAsync<QuickPayPayment>());
        }

        public QuickPayPayment CancelPayment(string paymentId)
        {
            return Request($"/payments/{paymentId}/cancel", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(null)
                .ReceiveJson<QuickPayPayment>());
        }

        public QuickPayPayment CapturePayment(string paymentId, object data)
        {
            return Request($"/payments/{paymentId}/capture", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        public QuickPayPayment RefundPayment(string paymentId, object data)
        {
            return Request($"/payments/{paymentId}/refund", (req) => req
                .WithHeader("Content-Type", "application/json")
                .PostJsonAsync(data)
                .ReceiveJson<QuickPayPayment>());
        }

        private TResult Request<TResult>(string url, Func<IFlurlRequest, Task<TResult>> func)
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

                result = func.Invoke(req).Result;
            }
            catch (FlurlHttpException ex)
            {
                throw;
            }

            return result;
        }
    }
}

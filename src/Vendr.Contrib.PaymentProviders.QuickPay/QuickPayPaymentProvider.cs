using System;
using System.Threading.Tasks;
using Vendr.Core.Api;
using Vendr.Core.Models;
using Vendr.Core.PaymentProviders;
using Vendr.Extensions;

namespace Vendr.Contrib.PaymentProviders.QuickPay
{
    [PaymentProvider("quickpay", "QuickPay", "QuickPay payment provider")]
    public class QuickPayPaymentProvider : PaymentProviderBase<QuickPaySettings>
    {
        public QuickPayPaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool FinalizeAtContinueUrl => true;

        public override Task<PaymentFormResult> GenerateFormAsync(PaymentProviderContext<QuickPaySettings> ctx)
        {
            return Task.FromResult(new PaymentFormResult()
            {
                Form = new PaymentForm(ctx.Urls.ContinueUrl, PaymentFormMethod.Post)
            });
        }

        public override string GetCancelUrl(PaymentProviderContext<QuickPaySettings> ctx)
        {
            return string.Empty;
        }

        public override string GetErrorUrl(PaymentProviderContext<QuickPaySettings> ctx)
        {
            return string.Empty;
        }

        public override string GetContinueUrl(PaymentProviderContext<QuickPaySettings> ctx)
        {
            ctx.Settings.MustNotBeNull("ctx.Settings");
            ctx.Settings.ContinueUrl.MustNotBeNull("ctx.Settings.ContinueUrl");

            return ctx.Settings.ContinueUrl;
        }

        public override Task<CallbackResult> ProcessCallbackAsync(PaymentProviderContext<QuickPaySettings> ctx)
        {
            return Task.FromResult(new CallbackResult
            {
                TransactionInfo = new TransactionInfo
                {
                    AmountAuthorized = ctx.Order.TransactionAmount.Value,
                    TransactionFee = 0m,
                    TransactionId = Guid.NewGuid().ToString("N"),
                    PaymentStatus = PaymentStatus.Authorized
                }
            });
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreateStripeCheckoutSessionAsync(int bookingId, string successUrl, string cancelUrl);
        Task<bool> ProcessStripeWebhookAsync(string json, string signature);
        Task<bool> RefundPaymentAsync(int bookingId);
    }
}

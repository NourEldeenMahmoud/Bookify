using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreateStripeCheckoutSession(decimal amount, string currency, int bookingId);
        Task<bool> ProcessStripeWebhookAsync(string json, string signature);
        Task<bool> RefundPaymentAsync(int bookingId);
    }
}

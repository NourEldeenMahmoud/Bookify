using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreateStripeCheckoutSessionAsync(int bookingId, string successUrl, string cancelUrl);
        public Task<bool> ProcessStripeWebhookAsync(string json, string signature);
        public Task<bool> RefundPaymentAsync(int bookingId);
        
        // Payment Intent methods for inline checkout
        public Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests);
        public Task<bool> ConfirmPaymentAndCreateBookingAsync(string paymentIntentId, string userId, int roomId, DateTime checkIn, DateTime checkOut, int numberOfGuests, string? specialRequests = null);
    }
}

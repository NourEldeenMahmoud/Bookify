using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bookify.Services.DTOs;

namespace Bookify.Services.Interfaces
{
    public interface IPaymentService
    {
        public Task<string> CreatePaymentIntentForCartAsync(decimal amount, string currency, string userId, List<CartItemDto> cartItems);
        public Task<bool> ConfirmPaymentAndCreateBookingsAsync(string paymentIntentId, string userId, List<CartItemDto> cartItems, string? specialRequests = null);
    }
}

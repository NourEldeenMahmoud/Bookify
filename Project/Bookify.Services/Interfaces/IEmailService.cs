using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IEmailService
    {
        public Task SendBookingConfirmationAsync(string toEmail,int bookingId, string userName, DateTime checkIn, DateTime checkOut, decimal totalAmount);
        public Task SendPaymentConfirmationAsync(string toEmail, string userName, int bookingId, decimal totalAmount);
        public Task SendBookingCancellationAsync(string toEmail, string userName, int bookingId);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Services.Interfaces
{
    public interface IEmailService
    {
        public Task SendBookingConfirmation(string toEamil, string userName, DateTime checkIn, DateTime checkOut, decimal totalAmount);
        public Task SendBookingPayment(string toEmail, string userName, int bookingId, decimal totalAmount);
        public Task SendBookingCancellation(string toEmail, string userName, int bookingId);
    }
}

using Bookify.Data.Models;
using Bookify.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;


namespace Bookify.Services.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly Serilog.ILogger _logger;
        public EmailService(IConfiguration configuration, ILogger<EmailService> microsoftLogger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = Log.ForContext<EmailService>();
        }
        public async Task SendBookingCancellationAsync(string toEmail, string userName, int bookingId)
        {
            try
            {
                _logger.Information("Sending booking cancellation email - To: {ToEmail}, BookingId: {BookingId}", toEmail, bookingId);
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    _logger.Warning("Invalid email address provided");
                    throw new ArgumentException("Email address cannot be null or empty", nameof(toEmail));
                }
                if (string.IsNullOrWhiteSpace(userName))
                {
                    _logger.Warning("Invalid userName provided");
                    throw new ArgumentException("UserName cannot be null or empty", nameof(userName));
                }
                if (bookingId <= 0)
                {
                    _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                    throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
                }

                var apiKey = _configuration["SendGrid:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Warning("SendGrid API key is not configured. Email not sent.");
                    return ;
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(_configuration["SendGrid:FromEmail"] ?? "noreply@bookify.com", "Bookify");
                var to = new EmailAddress(toEmail, userName);
                var subject = "Booking Cancellation - Bookify";
                var plainTextContent = $@"
                                        RoyalHaven
                                        Where luxury meets comfort
                                        --------------------------------------------------
                                        
                                        Booking Cancelled
                                        
                                        Hi {userName},
                                        We regret to inform you that your booking has been cancelled.
                                        
                                        Booking ID: #{bookingId}
                                        Status: Cancelled
                                        
                                        RoyalHaven • Mansoura City
                                        Contact: info@royalhaven.com
                                        ";

                var htmlContent = $@"
                                    <table width='100%' cellpadding='0' cellspacing='0' role='presentation' 
                                    style='font-family: Inter, Arial, Helvetica, sans-serif; background-color:#f3ede6; padding:24px;'>
                                      <tr>
                                        <td align='center'>
                                          <table width='600' cellpadding='0' cellspacing='0' role='presentation' 
                                          style='background:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
                                    
                                            <!-- Header with small logo -->
                                            <tr>
                                              <td style='background:#4a2d1f; padding:18px 22px; text-align:center;'>
                                                <div style='font-size:18px; font-weight:700; color:#fff; margin-top:4px;'>RoyalHaven</div>
                                                <div style='font-size:12px; color:rgba(255,255,255,0.9); margin-top:4px;'>Where luxury meets comfort</div>
                                              </td>
                                            </tr>
                                    
                                            <!-- Body -->
                                            <tr>
                                              <td style='padding:28px 36px;'>
                                                <h2 style='margin:0 0 10px 0; font-size:20px; color:#4a2d1f; font-weight:700;'>Booking Cancelled</h2>
                                    
                                                <p style='margin:0 0 16px 0; font-size:15px; color:#6d5a4f; line-height:1.6;'>
                                                  Hi {userName},<br/>
                                                  We regret to inform you that your booking has been cancelled. Below are the details:
                                                </p>
                                    
                                                <table width='100%' cellpadding='10' cellspacing='0' role='presentation' 
                                                style='margin-top:14px; background:#fff7f0; border-radius:10px; border:1px solid #f0ded4;'>
                                    
                                                  <tr>
                                                    <td style='width:40%; font-weight:700; color:#4a2d1f;'>Booking ID</td>
                                                    <td style='color:#6d5a4f;'>#{bookingId}</td>
                                                  </tr>
                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Status</td>
                                                    <td style='color:#6d5a4f;'>Cancelled</td>
                                                  </tr>
                                    
                                                </table>
                                    
                                                <p style='font-size:14px; color:#6d5a4f; margin-top:16px;'>
                                                  If this cancellation was made in error or you need assistance, please contact our support team and we'll be happy to help.
                                                </p>
                                    
                                                <hr style='border:none; border-top:1px solid #e6dfd7; margin:22px 0;' />
                                    
                                                <p style='font-size:12px; color:#9c8f87; text-align:center; margin:0;'>
                                                  RoyalHaven • Mansoura City<br/>
                                                  <a href='mailto:info@royalhaven.com' style='color:#9c8f87; text-decoration:underline;'>info@royalhaven.com</a>
                                                </p>
                                              </td>
                                            </tr>
                                    
                                          </table>
                                        </td>
                                      </tr>
                                    </table>
                                    ";
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("Successfully sent booking cancellation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                }
                else
                {
                    _logger.Error("Failed to send cancellation email. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, await response.Body.ReadAsStringAsync());
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending booking cancellation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                throw;
            }
        }

        public async Task SendBookingConfirmationAsync(string toEmail, int bookingId, string userName, DateTime checkIn, DateTime checkOut, decimal totalAmount)
        {
            try
            {
                _logger.Information("Sending booking confirmation email - To: {ToEmail}, BookingId: {BookingId}", toEmail, bookingId);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    _logger.Warning("Invalid email address provided");
                    throw new ArgumentException("Email address cannot be null or empty", nameof(toEmail));
                }

                if (string.IsNullOrWhiteSpace(userName))
                {
                    _logger.Warning("Invalid userName provided");
                    throw new ArgumentException("UserName cannot be null or empty", nameof(userName));
                }

                if (bookingId <= 0)
                {
                    _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                    throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
                }

                if (checkIn >= checkOut)
                {
                    _logger.Warning("Invalid date range - CheckIn {CheckIn} must be before CheckOut {CheckOut}", checkIn, checkOut);
                    throw new ArgumentException("Check-in date must be before check-out date", nameof(checkIn));
                }

                if (totalAmount < 0)
                {
                    _logger.Warning("Invalid totalAmount provided: {TotalAmount}", totalAmount);
                    throw new ArgumentException("Total amount cannot be negative", nameof(totalAmount));
                }

                var apiKey = _configuration["SendGrid:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Warning("SendGrid API key is not configured. Email not sent.");
                    return;
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(_configuration["SendGrid:FromEmail"] ?? "noreply@bookify.com", "Bookify");
                var to = new EmailAddress(toEmail, userName);
                var subject = "Booking Confirmation - Bookify";
                var plainTextContent = $"RoyalHaven\r\nWhere luxury meets comfort\r\n--------------------------------------------------\r\n\r\nBooking Confirmed\r\n\r\nHi {userName},\r\nYour reservation at RoyalHaven has been successfully confirmed.\r\n\r\nHere are your stay details:\r\n\r\nBooking ID: #{bookingId}\r\nCheck-in: {checkIn:yyyy-MM-dd}\r\nCheck-out: {checkOut:yyyy-MM-dd}\r\nTotal Amount: ${totalAmount:F2}\r\n\r\n--------------------------------------------------\r\nRoyalHaven • Mansoura City\r\nContact: info@royalhaven.com";
                var htmlContent = $@"
                                    <table width='100%' cellpadding='0' cellspacing='0' role='presentation' 
                                    style='font-family: ""Inter"", Arial, Helvetica, sans-serif; background-color:#f3ede6; padding:24px;'>
                                      <tr>
                                        <td align='center'>
                                          <table width='600' cellpadding='0' cellspacing='0' role='presentation' 
                                          style='background:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
                                    
                                            <!-- Header -->
                                            <tr>
                                              <td style='background:#4a2d1f; padding:22px 28px; text-align:center;'>
                                                <div style='font-size:22px; font-weight:700; color:#fff;'>RoyalHaven</div>
                                                <div style='font-size:13px; color:rgba(255,255,255,0.85); margin-top:4px;'>
                                                  Where luxury meets comfort
                                                </div>
                                              </td>
                                            </tr>
                                    
                                            <!-- Body -->
                                            <tr>
                                              <td style='padding:32px 40px;'>
                                    
                                                <h2 style='margin:0 0 10px 0; font-size:22px; color:#4a2d1f; font-weight:700;'>
                                                  Booking Confirmed
                                                </h2>
                                    
                                                <p style='margin:0 0 16px 0; font-size:15px; color:#6d5a4f; line-height:1.6;'>
                                                  Hi {userName},<br>
                                                  Your reservation at <strong>RoyalHaven</strong> has been successfully confirmed.  
                                                  Here are your stay details:
                                                </p>
                                    
                                                <table width='100%' cellpadding='10' cellspacing='0' role='presentation' 
                                                style='margin-top:14px; background:#f9f4ef; border-radius:10px; border:1px solid #e7dbd1;'>
                                    
                                                  <tr>
                                                    <td style='width:40%; font-weight:700; color:#4a2d1f;'>Booking ID</td>
                                                    <td style='color:#6d5a4f;'>#{bookingId}</td>
                                                  </tr>
                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Check-in</td>
                                                    <td style='color:#6d5a4f;'>{checkIn:yyyy-MM-dd}</td>
                                                  </tr>
                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Check-out</td>
                                                    <td style='color:#6d5a4f;'>{checkOut:yyyy-MM-dd}</td>
                                                  </tr>

                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Total Amount</td>
                                                    <td style='color:#6d5a4f;'>${totalAmount:F2}</td>
                                                  </tr>
                                    
                                                </table>
                                    
                                                <hr style='border:none; border-top:1px solid #e6dfd7; margin:24px 0;' />
                                    
                                                <p style='font-size:12px; color:#9c8f87; text-align:center; margin:0;'>
                                                  RoyalHaven • Mansoura City<br>
                                                  <a href='mailto:info@royalhaven.com' 
                                                  style='color:#9c8f87; text-decoration:underline;'>info@royalhaven.com</a>
                                                </p>
                                    
                                              </td>
                                            </tr>
                                    
                                          </table>
                                        </td>
                                      </tr>
                                    </table>";
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("Successfully sent booking confirmation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                }
                else
                {
                    _logger.Error("Failed to send booking confirmation email. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, await response.Body.ReadAsStringAsync());
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending booking confirmation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                throw;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            try
            {
                _logger.Information("Sending password reset email - To: {ToEmail}", toEmail);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    _logger.Warning("Invalid email address provided");
                    throw new ArgumentException("Email address cannot be null or empty", nameof(toEmail));
                }

                if (string.IsNullOrWhiteSpace(resetLink))
                {
                    _logger.Warning("Invalid reset link provided");
                    throw new ArgumentException("Reset link cannot be null or empty", nameof(resetLink));
                }

                var apiKey = _configuration["SendGrid:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Warning("SendGrid API key is not configured. Email not sent.");
                    return false;
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(_configuration["SendGrid:FromEmail"] ?? "noreply@bookify.com", "Bookify");
                var to = new EmailAddress(toEmail, string.IsNullOrWhiteSpace(userName) ? "Bookify User" : userName);
                var subject = "Password Reset Request - Bookify";
                var plainTextContent = $"Hello {userName},\n\nYou recently requested to reset your password for your Bookify account.\n" +
                                       $"Click the link below to reset it:\n{resetLink}\n\nIf you did not request a password reset, please ignore this email.\n\n" +
                                       "This link will expire in 24 hours.\n\nThank you.";
                var htmlContent = $@"
                                    <table width='100%' cellpadding='0' cellspacing='0' role='presentation'
                                    style='font-family: ""Inter"", Arial, Helvetica, sans-serif; background-color:#f3ede6; padding:24px;'>
                                      <tr>
                                        <td align='center'>
                                          <table width='600' cellpadding='0' cellspacing='0' role='presentation'
                                          style='background:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
                                    
                                            <!-- Header -->
                                            <tr>
                                              <td style='background:#4a2d1f; padding:22px 28px; text-align:center;'>
                                                <div style='font-size:22px; font-weight:700; color:#fff;'>RoyalHaven</div>
                                                <div style='font-size:13px; color:rgba(255,255,255,0.85); margin-top:4px;'>
                                                  Your Journey to Luxury Continues
                                                </div>
                                              </td>
                                            </tr>
                                    
                                            <!-- Body -->
                                            <tr>
                                              <td style='padding:32px 40px;'>
                                    
                                                <h2 style='margin:0 0 10px 0; font-size:22px; color:#4a2d1f; font-weight:700;'>
                                                  Password Reset Request
                                                </h2>
                                    
                                                <p style='margin:0 0 16px 0; font-size:15px; color:#6d5a4f; line-height:1.6;'>
                                                  Hello {userName},<br/>
                                                  You recently requested to reset your password for your <strong>RoyalHaven</strong> account.
                                                  Please click the button below to set a new password.
                                                </p>
                                    
                                                <!-- Button -->
                                                <div style='margin:24px 0; text-align:center;'>
                                                  <a href='{resetLink}'
                                                  style='display:inline-block; padding:12px 28px; background-color:#c9591d; 
                                                  color:#ffffff; text-decoration:none; border-radius:8px; font-size:15px; font-weight:600;'>
                                                    Reset Password
                                                  </a>
                                                </div>
                                    
                                                <p style='margin:0 0 16px 0; font-size:14px; color:#6d5a4f; line-height:1.6;'>
                                                  If the button above doesn't work, copy and paste this link into your browser:
                                                </p>
                                    
                                                <p style='margin:0 0 16px 0; font-size:14px; word-break:break-all;'>
                                                  <a href='{resetLink}' style='color:#c9591d; text-decoration:underline;'>{resetLink}</a>
                                                </p>
                                    
                                                <p style='margin:0 0 24px 0; font-size:14px; color:#6d5a4f;'>
                                                  If you didn't request this password reset, you can safely ignore this email.<br/>
                                                  This link will expire in 24 hours.
                                                </p>
                                    
                                                <hr style='border:none; border-top:1px solid #e6dfd7; margin:24px 0;' />
                                    
                                                <!-- Footer -->
                                                <p style='font-size:12px; color:#9c8f87; text-align:center; margin:0;'>
                                                  RoyalHaven • Mansoura City<br/>
                                                  <a href='mailto:info@royalhaven.com'
                                                  style='color:#9c8f87; text-decoration:underline;'>info@royalhaven.com</a>
                                                </p>
                                    
                                              </td>
                                            </tr>
                                    
                                          </table>
                                        </td>
                                      </tr>
                                    </table>";


                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("Password reset email sent successfully to {ToEmail}", toEmail);
                    return true;
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.Error("Failed to send password reset email. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, responseBody);
                    
                    // Log specific error message for debugging
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _logger.Error("SendGrid Forbidden error - The 'From' email address is not verified. Please verify '{FromEmail}' in SendGrid Sender Identity settings.", 
                            _configuration["SendGrid:FromEmail"] ?? "noreply@bookify.com");
                    }
                    
                    return false;
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending password reset email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task SendPaymentConfirmationAsync(string toEmail, string userName, int bookingId, decimal totalAmount)
        {
            try
            {
                _logger.Information("Sending payment confirmation email - To: {ToEmail}, BookingId: {BookingId}, Amount: {Amount}",
                    toEmail, bookingId, totalAmount);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    _logger.Warning("Invalid email address provided");
                    throw new ArgumentException("Email address cannot be null or empty", nameof(toEmail));
                }

                if (string.IsNullOrWhiteSpace(userName))
                {
                    _logger.Warning("Invalid userName provided");
                    throw new ArgumentException("UserName cannot be null or empty", nameof(userName));
                }

                if (bookingId <= 0)
                {
                    _logger.Warning("Invalid bookingId provided: {BookingId}", bookingId);
                    throw new ArgumentException("BookingId must be greater than zero", nameof(bookingId));
                }

                if (totalAmount < 0)
                {
                    _logger.Warning("Invalid amount provided: {Amount}", totalAmount);
                    throw new ArgumentException("Amount cannot be negative", nameof(totalAmount));
                }

                var apiKey = _configuration["SendGrid:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Warning("SendGrid API key is not configured. Email not sent.");
                    return;
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(_configuration["SendGrid:FromEmail"] ?? "noreply@bookify.com", "Bookify");
                var to = new EmailAddress(toEmail, userName);
                var subject = "Payment Confirmation - Bookify";
                var plainTextContent = $@"
                                        RoyalHaven
                                        Where luxury meets comfort
                                        --------------------------------------------------
                                        
                                        Payment Received
                                        
                                        Hi {userName},
                                        
                                        We have received your payment for the following booking:
                                        
                                        Booking ID: #{bookingId}
                                        Amount Paid: ${totalAmount:F2}
                                        Status: Paid
                                        
                                        RoyalHaven • Mansoura City
                                        Contact: info@royalhaven.com
                                        ";
                var htmlContent = $@"
                                    <table width='100%' cellpadding='0' cellspacing='0' role='presentation' 
                                    style='font-family: Inter, Arial, Helvetica, sans-serif; background-color:#f3ede6; padding:24px;'>
                                      <tr>
                                        <td align='center'>
                                          <table width='600' cellpadding='0' cellspacing='0' role='presentation' 
                                          style='background:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
                                    
                                            <!-- Header with small logo -->
                                            <tr>
                                              <td style='background:#4a2d1f; padding:18px 22px; text-align:center;'>
                                                <div style='font-size:18px; font-weight:700; color:#fff; margin-top:4px;'>RoyalHaven</div>
                                                <div style='font-size:12px; color:rgba(255,255,255,0.9); margin-top:4px;'>Where luxury meets comfort</div>
                                              </td>
                                            </tr>
                                    
                                            <!-- Body -->
                                            <tr>
                                              <td style='padding:28px 36px;'>
                                                <h2 style='margin:0 0 10px 0; font-size:20px; color:#4a2d1f; font-weight:700;'>Payment Received</h2>
                                    
                                                <p style='margin:0 0 16px 0; font-size:15px; color:#6d5a4f; line-height:1.6;'>
                                                  Hi {userName},<br/>
                                                  We have successfully received your payment for the booking below. Thank you for choosing RoyalHaven!
                                                </p>
                                    
                                                <table width='100%' cellpadding='10' cellspacing='0' role='presentation' 
                                                style='margin-top:14px; background:#fff7f0; border-radius:10px; border:1px solid #f0ded4;'>
                                    
                                                  <tr>
                                                    <td style='width:40%; font-weight:700; color:#4a2d1f;'>Booking ID</td>
                                                    <td style='color:#6d5a4f;'>#{bookingId}</td>
                                                  </tr>
                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Amount Paid</td>
                                                    <td style='color:#6d5a4f;'>${totalAmount:F2}</td>
                                                  </tr>
                                    
                                                  <tr>
                                                    <td style='font-weight:700; color:#4a2d1f;'>Status</td>
                                                    <td style='color:#6d5a4f;'>Paid</td>
                                                  </tr>
                                    
                                                </table>
                                    
                                                <p style='font-size:14px; color:#6d5a4f; margin-top:16px;'>
                                                  You can view the full booking details or download your receipt using the links below.
                                                </p>
                                    
                                                <hr style='border:none; border-top:1px solid #e6dfd7; margin:22px 0;' />
                                    
                                                <p style='font-size:12px; color:#9c8f87; text-align:center; margin:0;'>
                                                  RoyalHaven • Mansoura City<br/>
                                                  <a href='mailto:info@royalhaven.com' style='color:#9c8f87; text-decoration:underline;'>info@royalhaven.com</a>
                                                </p>
                                              </td>
                                            </tr>
                                    
                                          </table>
                                        </td>
                                      </tr>
                                    </table>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("Successfully sent payment confirmation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                }
                else
                {
                    _logger.Error("Failed to send payment confirmation email. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, await response.Body.ReadAsStringAsync());
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending payment confirmation email to {ToEmail} for BookingId: {BookingId}", toEmail, bookingId);
                throw;
            }
        }
    }
}

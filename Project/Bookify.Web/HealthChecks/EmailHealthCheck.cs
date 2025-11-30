using Bookify.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bookify.Web.HealthChecks
{
    public class EmailHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        public EmailHealthCheck(IConfiguration configuration, IEmailService emailService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = _configuration["SendGrid:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "ApiKey")
                {
                    return HealthCheckResult.Unhealthy("SendGrid API key is not configured.");
                }
                var client = new SendGridClient(apiKey);

                var fromEmail = _configuration["SendGrid:FromEmail"];
                if (string.IsNullOrWhiteSpace(fromEmail))
                {
                    return HealthCheckResult.Degraded("SendGrid API key is configured but FromEmail is missing.");
                }

                if (!apiKey.StartsWith("SG.", StringComparison.OrdinalIgnoreCase))
                {
                    return HealthCheckResult.Unhealthy("SendGrid API key format is invalid. API keys should start with 'SG.'");
                }
                
                return HealthCheckResult.Healthy($"SendGrid is configured correctly. FromEmail: {fromEmail}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SendGrid health check failed.", ex);
            }
        }
    }
}

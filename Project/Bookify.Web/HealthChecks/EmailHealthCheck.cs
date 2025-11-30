using Bookify.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace Bookify.Web.HealthChecks
{
    public class EmailHealthCheck : IHealthCheck
    {
        private readonly IEmailService _emailService;

        public EmailHealthCheck(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
               
                if (_emailService == null)
                {
                    return HealthCheckResult.Unhealthy("Email service is not available.");
                }


                return HealthCheckResult.Healthy("Email service is available.");
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy("Email service check failed.", ex);
            }
        }
    }
}

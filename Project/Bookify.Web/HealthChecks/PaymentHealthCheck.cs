using Bookify.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace Bookify.Web.HealthChecks
{
    public class PaymentHealthCheck : IHealthCheck
    {
        private readonly IPaymentService _paymentService;

        public PaymentHealthCheck(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
              
                if (_paymentService == null)
                {
                    return HealthCheckResult.Unhealthy("Payment service is not available.");
                }

                
                return HealthCheckResult.Healthy("Payment service is available.");
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy("Payment service check failed.", ex);
            }
        }
    }
}

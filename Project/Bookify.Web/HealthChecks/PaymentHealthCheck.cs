using Bookify.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stripe;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bookify.Web.HealthChecks
{
    public class PaymentHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public PaymentHealthCheck(IConfiguration configuration, IPaymentService paymentService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = _configuration["Stripe:SecretKey"];
                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "SecretKey")
                {
                    return HealthCheckResult.Unhealthy("Stripe API key is not configured.");
                }

                // Validate API key format
                // Stripe secret keys start with "sk_" (live) or "sk_test_" (test)
                if (!apiKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
                {
                    return HealthCheckResult.Unhealthy("Stripe API key format is invalid. Secret keys should start with 'sk_' or 'sk_test_'.");
                }

                // Make a real API call to Stripe to verify the key is valid
                // We'll use the Balance API which is lightweight and doesn't require special permissions
                StripeConfiguration.ApiKey = apiKey;

                var balanceService = new BalanceService();
                var balance = await balanceService.GetAsync(cancellationToken: cancellationToken);

                if (balance != null)
                {
                    var isTestMode = apiKey.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase);
                    return HealthCheckResult.Healthy($"Stripe API is accessible. Mode: {(isTestMode ? "Test" : "Live")}, Currency: {balance.Available?.FirstOrDefault()?.Currency ?? "N/A"}");
                }

                return HealthCheckResult.Degraded("Stripe API key is configured but unable to retrieve account information.");
            }
            catch (StripeException ex)
            {
                // Handle specific Stripe errors
                if (ex.StripeError?.Code == "api_key_expired" || ex.StripeError?.Code == "invalid_api_key")
                {
                    return HealthCheckResult.Unhealthy($"Stripe API key is invalid or expired: {ex.Message}");
                }

                return HealthCheckResult.Unhealthy($"Stripe API error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Stripe health check failed.", ex);
            }
        }
    }
}

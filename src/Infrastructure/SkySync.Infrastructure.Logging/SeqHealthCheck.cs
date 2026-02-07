using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SkySync.Infrastructure.Logging;

/// <summary>
/// Health check that verifies Seq log server connectivity.
/// GET {SeqUrl}/health - Seq returns 200 (healthy), 503 (degraded), or unreachable (unhealthy).
/// </summary>
public sealed class SeqHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private const string DefaultSeqUrl = "http://localhost:5341";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var seqUrl = configuration["Seq:ServerUrl"] ?? DefaultSeqUrl;
        var baseUrl = seqUrl.TrimEnd('/');
        var healthUrl = $"{baseUrl}/health";

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync(healthUrl, cancellationToken);

            // Seq /health returns 200 when healthy, 503 when degraded
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Seq is reachable", new Dictionary<string, object>
                {
                    ["seq_url"] = baseUrl,
                    ["status_code"] = (int)response.StatusCode
                });
            }

            // 503 = Seq node degraded but reachable
            return HealthCheckResult.Degraded($"Seq responded with {response.StatusCode}", data: new Dictionary<string, object>
            {
                ["seq_url"] = baseUrl,
                ["status_code"] = (int)response.StatusCode
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Seq is unreachable", ex, new Dictionary<string, object>
            {
                ["seq_url"] = baseUrl
            });
        }
    }
}

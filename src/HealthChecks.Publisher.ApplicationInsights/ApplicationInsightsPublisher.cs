using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthChecks.Publisher.ApplicationInsights;

internal class ApplicationInsightsPublisher : IHealthCheckPublisher
{
    private const string EVENT_NAME = "AspNetCoreHealthCheck";
    private const string METRIC_STATUS_NAME = "AspNetCoreHealthCheckStatus";
    private const string METRIC_DURATION_NAME = "AspNetCoreHealthCheckDuration";
    private const string HEALTHCHECK_NAME = "AspNetCoreHealthCheckName";

    private static TelemetryClient? _client;
    private static readonly object _syncRoot = new object();
    private readonly TelemetryConfiguration? _telemetryConfiguration;
    private readonly string? _connectionString;
    private readonly string? _instrumentationKey;
    private readonly bool _saveDetailedReport;
    private readonly bool _excludeHealthyReports;

    private readonly bool _publishAvailabilityEvent;

    private readonly string _publishAvailabilityLocation;

    public ApplicationInsightsPublisher(
        IOptions<TelemetryConfiguration>? telemetryConfiguration,
        string? connectionString = default,
        string? instrumentationKey = default,
        bool saveDetailedReport = false,
        bool excludeHealthyReports = false,
        bool publishAvailabilityEvent = false,
        string publishAvailabilityLocation = "")
    {
        _telemetryConfiguration = telemetryConfiguration?.Value;
        _connectionString = connectionString;
        _instrumentationKey = instrumentationKey;
        _saveDetailedReport = saveDetailedReport;
        _excludeHealthyReports = excludeHealthyReports;
        _publishAvailabilityEvent = publishAvailabilityEvent;
        _publishAvailabilityLocation = publishAvailabilityLocation;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.Status == HealthStatus.Healthy && _excludeHealthyReports)
        {
            return Task.CompletedTask;
        }

        var client = GetOrCreateTelemetryClient();
        if (_saveDetailedReport)
        {
            SaveDetailedReport(report, client, _publishAvailabilityEvent, _publishAvailabilityLocation);
        }
        else
        {
            SaveGeneralizedReport(report, client, _publishAvailabilityEvent, _publishAvailabilityLocation);
        }

        return Task.CompletedTask;
    }

    private void SaveDetailedReport(HealthReport report, TelemetryClient client, bool publishAvailabilityEvent, string publishAvailabilityLocation)
    {
        foreach (var reportEntry in report.Entries.Where(entry => !_excludeHealthyReports || entry.Value.Status != HealthStatus.Healthy))
        {
            if (publishAvailabilityEvent)
            {
                client.TrackAvailability($"{EVENT_NAME}:{reportEntry.Key}",
                    DateTimeOffset.Now,
                    reportEntry.Value.Duration,
                    publishAvailabilityLocation,
                    reportEntry.Value.Status == HealthStatus.Healthy,
                    null,
                    properties: new Dictionary<string, string?>()
                    {
                        { nameof(Environment.MachineName), Environment.MachineName },
                        { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name }
                    });
            }
            else
            {
                client.TrackEvent($"{EVENT_NAME}:{reportEntry.Key}",
                    properties: new Dictionary<string, string?>()
                    {
                        { nameof(Environment.MachineName), Environment.MachineName },
                        { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name },
                        { HEALTHCHECK_NAME, reportEntry.Key }
                    },
                    metrics: new Dictionary<string, double>()
                    {
                        { METRIC_STATUS_NAME, reportEntry.Value.Status == HealthStatus.Healthy ? 1 : 0 },
                        { METRIC_DURATION_NAME, reportEntry.Value.Duration.TotalMilliseconds }
                    });
            }

        }

        foreach (var reportEntry in report.Entries.Where(entry => entry.Value.Exception != null))
        {
            if (publishAvailabilityEvent)
            {
                client.TrackAvailability($"{EVENT_NAME}:{reportEntry.Key}",
                    DateTimeOffset.Now,
                    reportEntry.Value.Duration,
                    publishAvailabilityLocation,
                    reportEntry.Value.Status == HealthStatus.Healthy,
                    reportEntry.Value.Exception != null ? reportEntry.Value.Exception.ToString() : null,
                    properties: new Dictionary<string, string?>()
                    {
                        { nameof(Environment.MachineName), Environment.MachineName },
                        { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name }
                    });
            }
            else
            {
                client.TrackException(reportEntry.Value.Exception,
                    properties: new Dictionary<string, string?>()
                    {
                        { nameof(Environment.MachineName), Environment.MachineName },
                        { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name },
                        { HEALTHCHECK_NAME, reportEntry.Key }
                    },
                    metrics: new Dictionary<string, double>()
                    {
                        { METRIC_STATUS_NAME, reportEntry.Value.Status == HealthStatus.Healthy ? 1 : 0 },
                        { METRIC_DURATION_NAME, reportEntry.Value.Duration.TotalMilliseconds }
                    });
            }

        }
    }
    private static void SaveGeneralizedReport(HealthReport report, TelemetryClient client, bool publishAvailabilityEvent, string publishAvailabilityLocation)
    {
        if (publishAvailabilityEvent)
        {
            client.TrackAvailability(EVENT_NAME,
                DateTimeOffset.Now,
                report.TotalDuration,
                publishAvailabilityLocation,
                report.Status == HealthStatus.Healthy,
                null,
                properties: new Dictionary<string, string?>()
                {
                    { nameof(Environment.MachineName), Environment.MachineName },
                    { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name }
                });
        }
        else
        {
            client.TrackEvent(EVENT_NAME,
                properties: new Dictionary<string, string?>
                {
                    { nameof(Environment.MachineName), Environment.MachineName },
                    { nameof(Assembly), Assembly.GetEntryAssembly()?.GetName().Name }
                },
                metrics: new Dictionary<string, double>
                {
                    { METRIC_STATUS_NAME, report.Status == HealthStatus.Healthy ? 1 : 0 },
                    { METRIC_DURATION_NAME, report.TotalDuration.TotalMilliseconds }
                });
        }
    }

    private TelemetryClient GetOrCreateTelemetryClient()
    {
        if (_client == null)
        {
            lock (_syncRoot)
            {
                if (_client == null)
                {
                    // Create TelemetryConfiguration
                    // Hierachy: _connectionString > _instrumentationKey > _telemetryConfiguration
                    var configuration = string.IsNullOrWhiteSpace(_connectionString)
                        ? string.IsNullOrWhiteSpace(_instrumentationKey)
                            ? _telemetryConfiguration
                            : new TelemetryConfiguration(_instrumentationKey)
                        : new TelemetryConfiguration { ConnectionString = _connectionString };


                    _client = new TelemetryClient(configuration);
                }
            }
        }
        return _client;
    }
}

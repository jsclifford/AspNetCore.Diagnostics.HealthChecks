using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthChecks.Publisher.ApplicationInsights
{
    public class ApplicationInsightsAvailabilityPublisher : IHealthCheckPublisher
    {
        private static TelemetryClient? _client;
        private static readonly object _syncRoot = new object();
        private readonly TelemetryConfiguration? _telemetryConfiguration;
        private readonly string? _connectionString;
        private readonly string? _instrumentationKey;
        private readonly bool _saveDetailedReport;
        private readonly IOptions<ApplicationInsightsAvailibilityPublisherOptions> _options;

        public ApplicationInsightsAvailabilityPublisher(
            IOptions<TelemetryConfiguration>? telemetryConfiguration,
            IOptions<ApplicationInsightsAvailibilityPublisherOptions> options,
            string? connectionString = default,
            string? instrumentationKey = default,
            bool saveDetailedReport = false)
        {
            _telemetryConfiguration = telemetryConfiguration?.Value;
            _connectionString = connectionString;
            _instrumentationKey = instrumentationKey;
            _saveDetailedReport = saveDetailedReport;
            _options = options;
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            var client = GetOrCreateTelemetryClient();
            if (_saveDetailedReport)
            {
                foreach (var entry in report.Entries)
                {
                    var availabilityTelemetry = GetAvailabilityTelemetry(entry.Key, entry.Value);
                    client.TrackAvailability(availabilityTelemetry);
                }
            }
            else
            {
                var availabilityTelemetry = GetAvailabilityTelemetry(report);
                client.TrackAvailability(availabilityTelemetry);
            }


            return Task.CompletedTask;
        }

        private AvailabilityTelemetry GetAvailabilityTelemetry(HealthReport report)
        {
            var availabilityTelemetry = new AvailabilityTelemetry
            {
                Timestamp = DateTimeOffset.UtcNow,

                Duration = report.TotalDuration,
                Success = GetSuccessFromStatus(report.Status),

                Name = _options.Value.TestName,
                RunLocation = _options.Value.TestRunLocation
            };

            AddHealthChecksStatuses(availabilityTelemetry, report);
            AddHealthChecksDurations(availabilityTelemetry, report);
            AddHealthChecksDescriptions(availabilityTelemetry, report);

            return availabilityTelemetry;
        }

        private AvailabilityTelemetry GetAvailabilityTelemetry(string name, HealthReportEntry entry)
        {
            var availabilityTelemetry = new AvailabilityTelemetry
            {
                Timestamp = DateTimeOffset.UtcNow,

                Duration = entry.Duration,
                Success = GetSuccessFromStatus(entry.Status),

                Name = $"{_options.Value.TestName}-{name}",
                RunLocation = _options.Value.TestRunLocation,
                Message = entry.Description
            };

            AddHealthReportEntryData(availabilityTelemetry, entry);

            return availabilityTelemetry;
        }

        private bool GetSuccessFromStatus(HealthStatus status)
        {
            return status == HealthStatus.Healthy
                   || _options.Value.TreatDegradedAsSuccess && status == HealthStatus.Degraded;
        }

        private static void AddHealthChecksDurations(AvailabilityTelemetry telemetry, HealthReport report)
        {
            var durations = report.Entries.ToDictionary(x => $"HealthCheck-Duration-{x.Key}", x => x.Value.Duration.TotalMilliseconds);
            foreach (var duration in durations)
            {
                telemetry.Metrics.Add(duration);
            }
        }

        private static void AddHealthChecksStatuses(AvailabilityTelemetry telemetry, HealthReport report)
        {
            var statuses = report.Entries.ToDictionary(x => $"HealthCheck-Status-{x.Key}", x => x.Value.Status.ToString());
            foreach (var status in statuses)
            {
                telemetry.Properties.Add(status);
            }
        }

        private static void AddHealthChecksDescriptions(AvailabilityTelemetry telemetry, HealthReport report)
        {
            var descriptions = report.Entries.ToDictionary(x => $"HealthCheck-Description-{x.Key}", x => x.Value.Description ?? string.Empty);
            foreach (var description in descriptions)
            {
                telemetry.Properties.Add(description);
            }
        }

        private static void AddHealthReportEntryData(AvailabilityTelemetry telemetry, HealthReportEntry entry)
        {
            foreach (var data in entry.Data)
            {
                telemetry.Properties.Add($"HealthCheck-Data-{data.Key}", data.Value?.ToString() ?? "<null>");
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
}

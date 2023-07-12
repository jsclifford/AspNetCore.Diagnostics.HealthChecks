namespace HealthChecks.Publisher.ApplicationInsightsAvailability
{
    public class ApplicationInsightsAvailibilityPublisherOptions
    {
        // <summary>
        /// AppInsights Connection String for telemetry client
        /// </summary>
        public string? ConnectionString { get; set; }

        // <summary>
        /// AppInsights Instrumentation Key for telemetry client
        /// </summary>
        public string? InstrumentationKey { get; set; }

        // <summary>
        /// Save Detailed Report for every health check instead of aggregate of all health checks
        /// </summary>
        public bool SaveDetailedReport { get; set; } = false;

        /// <summary>
        /// Availibility telemetry name.
        /// </summary>
        public string TestName { get; set; } = "AspNetCoreHealthCheck";

        /// <summary>
        /// Availibility telemetry run location.
        /// </summary>
        public string TestRunLocation { get; set; } = "Application";

        /// <summary>
        /// If a degredes status indicates that the Availibility telemetry is not successful.
        /// </summary>
        public bool TreatDegradedAsSuccess { get; set; } = false;
    }
}

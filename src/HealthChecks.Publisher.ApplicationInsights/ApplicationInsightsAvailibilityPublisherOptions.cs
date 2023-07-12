namespace HealthChecks.Publisher.ApplicationInsights
{
    public class ApplicationInsightsAvailibilityPublisherOptions
    {
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

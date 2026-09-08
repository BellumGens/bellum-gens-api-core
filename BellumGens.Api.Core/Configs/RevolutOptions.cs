namespace BellumGens.Api.Core
{
    /// <summary>
    /// Bound from the "revolut" configuration section. SecretKey and WebhookSigningSecret must come from
    /// App Service settings or user-secrets, never from appsettings.json.
    /// </summary>
    public class RevolutOptions
    {
        public const string Section = "revolut";

        /// <summary>https://sandbox-merchant.revolut.com for testing, https://merchant.revolut.com in production.</summary>
        public string BaseUrl { get; set; } = "https://sandbox-merchant.revolut.com";

        public string SecretKey { get; set; }

        public string ApiVersion { get; set; } = "2024-09-01";

        /// <summary>The wsk_ secret returned when the webhook was registered.</summary>
        public string WebhookSigningSecret { get; set; }

        /// <summary>Maximum age of a webhook timestamp before it is rejected.</summary>
        public int WebhookToleranceMinutes { get; set; } = 5;
    }
}

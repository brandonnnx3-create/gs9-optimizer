namespace ConnectionOptimizer.Services.Licensing;

/// <summary>
/// Where the app checks who is allowed. The URL is baked into the build on purpose: if it could be changed
/// on the user's PC, a revoked user could point it at an old copy of the list and dodge the revocation.
/// </summary>
public static class AccessConfig
{
    /// <summary>
    /// Raw URL of the signed allowlist (e.g. a GitHub gist "raw" link). Set this before shipping a build.
    /// While it is the placeholder, the app cannot verify access and will stay locked.
    /// </summary>
    public const string AllowlistUrl = "https://REPLACE-WITH-YOUR-GIST-RAW-URL";

    /// <summary>How long the app keeps working offline after the last successful check, before it must reconnect.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);

    /// <summary>How long to wait for the allowlist download before treating it as offline.</summary>
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    public static bool IsConfigured =>
        AllowlistUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        && !AllowlistUrl.Contains("REPLACE-WITH", StringComparison.OrdinalIgnoreCase);
}

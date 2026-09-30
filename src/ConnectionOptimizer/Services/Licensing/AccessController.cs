using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConnectionOptimizer.Services.Licensing;

public enum AccessState
{
    /// <summary>Allowed by a fresh online check.</summary>
    Allowed,

    /// <summary>Allowed using the cached list because the server could not be reached (within the grace period).</summary>
    AllowedOffline,

    /// <summary>The hardware ID is not on the list (never added, or removed/revoked).</summary>
    NotAuthorized,

    /// <summary>The entry exists but its date passed.</summary>
    Expired,

    /// <summary>Could not verify: server unreachable and no valid cache within the grace period.</summary>
    Unverified,

    /// <summary>The build has no allowlist URL set.</summary>
    NotConfigured,

    /// <summary>The hardware ID could not be read.</summary>
    NoHardwareId,
}

public sealed record AccessResult
{
    public required AccessState State { get; init; }
    public string? Hwid { get; init; }
    public string? Name { get; init; }
    public required string Message { get; init; }

    public bool IsAllowed => State is AccessState.Allowed or AccessState.AllowedOffline;
}

/// <summary>
/// Decides whether this PC may run the app by downloading a signed allowlist the owner controls.
/// Removing a hardware ID from that list revokes it: the change takes effect on the next successful check,
/// or within the grace period if the PC is kept offline. The list is signed, so only the owner can change it.
/// </summary>
public sealed class AccessController(string dataDirectory)
{
    private readonly string _cachePath = Path.Combine(dataDirectory, "access.cache");
    private static readonly HttpClient Http = new() { Timeout = AccessConfig.FetchTimeout };

    private sealed record CacheFile(string SignedText, DateTime FetchedUtc);

    public async Task<AccessResult> CheckAsync()
    {
        if (!AccessConfig.IsConfigured)
        {
            return new AccessResult
            {
                State = AccessState.NotConfigured,
                Message = "This build has no access server configured.",
            };
        }

        string hwid;
        try
        {
            hwid = HardwareId.Get();
        }
        catch (Exception ex)
        {
            return new AccessResult
            {
                State = AccessState.NoHardwareId,
                Message = $"The hardware ID could not be read: {ex.Message}",
            };
        }

        using ECDsa publicKey = LicenseFormat.ImportPublicKey(LicensePublicKey.Value);

        (string? signedText, bool fromNetwork) = await FetchAsync().ConfigureAwait(false);

        // A fresh, valid download is authoritative and refreshes the cache.
        if (fromNetwork && signedText is not null && AllowlistFormat.Verify(signedText, publicKey) is { } fresh)
        {
            SaveCache(new CacheFile(signedText, DateTime.UtcNow));
            return Decide(fresh, hwid, online: true);
        }

        // Could not fetch/verify a fresh list: fall back to the last good one within the grace period.
        if (LoadCache() is { } cache
            && DateTime.UtcNow - cache.FetchedUtc <= AccessConfig.GracePeriod
            && AllowlistFormat.Verify(cache.SignedText, publicKey) is { } cached)
        {
            return Decide(cached, hwid, online: false);
        }

        return new AccessResult
        {
            State = AccessState.Unverified,
            Hwid = hwid,
            Message = "Could not verify access. Connect to the internet and try again.",
        };
    }

    private AccessResult Decide(Allowlist allowlist, string hwid, bool online)
    {
        (AllowlistDecision decision, AllowlistEntry? entry) = AllowlistFormat.Evaluate(allowlist, hwid, DateTime.Now);
        return decision switch
        {
            AllowlistDecision.Allowed => new AccessResult
            {
                State = online ? AccessState.Allowed : AccessState.AllowedOffline,
                Hwid = hwid,
                Name = entry!.Name,
                Message = online ? $"Authorized as {entry.Name}." : $"Authorized as {entry.Name} (offline).",
            },
            AllowlistDecision.Expired => new AccessResult
            {
                State = AccessState.Expired,
                Hwid = hwid,
                Name = entry!.Name,
                Message = $"Access for {entry.Name} expired on {entry.Expires}.",
            },
            _ => new AccessResult
            {
                State = AccessState.NotAuthorized,
                Hwid = hwid,
                Message = "This PC is not authorized.",
            },
        };
    }

    private static async Task<(string? Text, bool FromNetwork)> FetchAsync()
    {
        try
        {
            // Bypass any cached copy so a revocation is seen promptly.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{AccessConfig.AllowlistUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            request.Headers.CacheControl = new() { NoCache = true };
            using HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, false);
            }

            return (await response.Content.ReadAsStringAsync().ConfigureAwait(false), true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            return (null, false);
        }
    }

    private void SaveCache(CacheFile cache)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache), Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the cache only means the next offline launch cannot use the grace period.
        }
    }

    private CacheFile? LoadCache()
    {
        try
        {
            return File.Exists(_cachePath)
                ? JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cachePath))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}

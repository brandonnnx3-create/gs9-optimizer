namespace ConnectionOptimizer.Services.Licensing;

/// <summary>
/// Public half of the G.S.9 license key (ECDSA P-256, SubjectPublicKeyInfo, base64).
/// It can only verify licenses, not create them. To change keys: run
/// <c>LicenseTool keygen</c>, paste the printed value here and rebuild. Existing licenses stop working.
/// </summary>
internal static class LicensePublicKey
{
    public const string Value =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAERdUp7afKnVJu+/mEMlAdEYkgXrEDawiSb8ZR2t+XYA7ItKHruuChuOjrMFg7gFuIX4QLfkEwjdNYsVRQwUKgqA==";
}

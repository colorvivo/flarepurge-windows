using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FlarePurge.Core.Api;

public sealed class CertificatePinningHandler : HttpClientHandler
{
    // SPKI SHA-256 hashes mirrored byte-for-byte from
    // Apple/CloudflareKit/Sources/CloudflareKit/API/CertificatePinning.swift.
    // Divergence from the iOS/macOS/Android apps kill-switches this platform
    // independently. Regenerate with scripts/extract_spki_hashes.sh from the
    // Apple repo after CA rotation, then re-sync both sides in the same commit.
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CloudflareApiPins =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["api.cloudflare.com"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "kIdp6NNEd8wsugYyyIYFsi1ylMCED3hZbSR8ZFsa/A4=", // GTS WE1 (ECDSA intermediate, primary)
                "yDu9og255NN5GEf+Bwa9rTrqFQ0EydZ0r1FCh9TdAW4=", // GTS WR1 (RSA intermediate, alternate)
                "mEflZT5enoR1FuXLgYYGqnVEoZvmf9c2bVBpiOjYQ0c=", // GTS Root R4 (ECDSA root, safety net)
            },
        };

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _pins;
    private readonly bool _enabled;

    public CertificatePinningHandler()
        : this(enabled: true, pinnedHosts: null) { }

    public CertificatePinningHandler(bool enabled, IReadOnlyDictionary<string, IReadOnlySet<string>>? pinnedHosts)
    {
        _enabled = enabled;
        _pins = pinnedHosts ?? CloudflareApiPins;
        ServerCertificateCustomValidationCallback = ValidateCertificate;

        // Never follow redirects: a 3xx from the API would carry the request (and
        // its POST body) to a host where the pin set does not apply, silently
        // escaping the pinned perimeter. The Cloudflare v4 API uses no redirects.
        AllowAutoRedirect = false;
    }

    public static string ComputeSpkiHash(X509Certificate2 certificate)
    {
        var spki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
        return Convert.ToBase64String(SHA256.HashData(spki));
    }

    public bool CheckPin(string host, IEnumerable<X509Certificate2> chain, SslPolicyErrors errors)
    {
        if (!_enabled) return errors == SslPolicyErrors.None;
        if (errors != SslPolicyErrors.None) return false;
        if (!_pins.TryGetValue(host, out var expected)) return true;

        foreach (var cert in chain)
        {
            if (expected.Contains(ComputeSpkiHash(cert))) return true;
        }
        return false;
    }

    private bool ValidateCertificate(HttpRequestMessage message, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        var host = message.RequestUri?.Host ?? string.Empty;
        var chainCerts = chain is null
            ? (certificate is null
                ? Array.Empty<X509Certificate2>()
                : new[] { certificate })
            : chain.ChainElements.Select(e => e.Certificate).ToArray();
        return CheckPin(host, chainCerts, errors);
    }
}

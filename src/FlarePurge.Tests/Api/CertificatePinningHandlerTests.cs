using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FlarePurge.Core.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class CertificatePinningHandlerTests
{
    private static X509Certificate2 CreateSelfSignedRsaCert(string subject = "test")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            $"CN={subject}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public void ComputeSpkiHash_IsStable_ForSameKey()
    {
        using var cert = CreateSelfSignedRsaCert();

        var h1 = CertificatePinningHandler.ComputeSpkiHash(cert);
        var h2 = CertificatePinningHandler.ComputeSpkiHash(cert);

        h1.Should().Be(h2);
        h1.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeSpkiHash_DiffersAcrossDifferentKeys()
    {
        using var a = CreateSelfSignedRsaCert("a");
        using var b = CreateSelfSignedRsaCert("b");

        var ha = CertificatePinningHandler.ComputeSpkiHash(a);
        var hb = CertificatePinningHandler.ComputeSpkiHash(b);

        ha.Should().NotBe(hb);
    }

    [Fact]
    public void CheckPin_HostNotPinned_FallsThroughToTrueWhenErrorsNone()
    {
        using var cert = CreateSelfSignedRsaCert();
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>());

        handler.CheckPin("api.example.com", new[] { cert }, SslPolicyErrors.None)
            .Should().BeTrue();
    }

    [Fact]
    public void CheckPin_HostPinned_HashMatches_ReturnsTrue()
    {
        using var cert = CreateSelfSignedRsaCert();
        var hash = CertificatePinningHandler.ComputeSpkiHash(cert);
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>
            {
                ["api.cloudflare.com"] = new HashSet<string> { hash }
            });

        handler.CheckPin("api.cloudflare.com", new[] { cert }, SslPolicyErrors.None)
            .Should().BeTrue();
    }

    [Fact]
    public void CheckPin_HostPinned_NoHashMatch_ReturnsFalse()
    {
        using var cert = CreateSelfSignedRsaCert();
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>
            {
                ["api.cloudflare.com"] = new HashSet<string> { "not-a-matching-hash" }
            });

        handler.CheckPin("api.cloudflare.com", new[] { cert }, SslPolicyErrors.None)
            .Should().BeFalse();
    }

    [Fact]
    public void CheckPin_HostPinned_ScansEntireChain()
    {
        using var leaf = CreateSelfSignedRsaCert("leaf");
        using var intermediate = CreateSelfSignedRsaCert("intermediate");
        using var root = CreateSelfSignedRsaCert("root");
        var rootHash = CertificatePinningHandler.ComputeSpkiHash(root);

        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>
            {
                ["api.cloudflare.com"] = new HashSet<string> { rootHash }
            });

        handler.CheckPin("api.cloudflare.com", new[] { leaf, intermediate, root }, SslPolicyErrors.None)
            .Should().BeTrue();
    }

    [Fact]
    public void CheckPin_HostnameLookupIsCaseInsensitive()
    {
        using var cert = CreateSelfSignedRsaCert();
        var hash = CertificatePinningHandler.ComputeSpkiHash(cert);
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["api.cloudflare.com"] = new HashSet<string> { hash }
            });

        handler.CheckPin("API.Cloudflare.com", new[] { cert }, SslPolicyErrors.None)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
    public void CheckPin_AnySslPolicyError_ReturnsFalseEvenWithMatchingPin(SslPolicyErrors errors)
    {
        using var cert = CreateSelfSignedRsaCert();
        var hash = CertificatePinningHandler.ComputeSpkiHash(cert);
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>
            {
                ["api.cloudflare.com"] = new HashSet<string> { hash }
            });

        handler.CheckPin("api.cloudflare.com", new[] { cert }, errors).Should().BeFalse();
    }

    [Fact]
    public void CheckPin_Disabled_RespectsBaseSslValidation()
    {
        using var cert = CreateSelfSignedRsaCert();
        var handler = new CertificatePinningHandler(enabled: false, pinnedHosts: null);

        handler.CheckPin("anything", new[] { cert }, SslPolicyErrors.None).Should().BeTrue();
        handler.CheckPin("anything", new[] { cert }, SslPolicyErrors.RemoteCertificateChainErrors).Should().BeFalse();
    }

    [Fact]
    public void CheckPin_EmptyChain_HostPinned_ReturnsFalse()
    {
        var handler = new CertificatePinningHandler(
            enabled: true,
            pinnedHosts: new Dictionary<string, IReadOnlySet<string>>
            {
                ["api.cloudflare.com"] = new HashSet<string> { "x" }
            });

        handler.CheckPin("api.cloudflare.com", Array.Empty<X509Certificate2>(), SslPolicyErrors.None)
            .Should().BeFalse();
    }
}

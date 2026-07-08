using System.Collections.Generic;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;
using FlarePurge.Tests.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class AuthServiceTests
{
    [Fact]
    public async Task VerifyAsync_Success_ReturnsVerification()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<TokenVerification>(
                Success: true,
                Errors: [],
                Messages: [],
                Result: new TokenVerification("tok", "active"),
                ResultInfo: null));
        var svc = new AuthService(client);

        var verification = await svc.VerifyAsync();

        verification.Id.Should().Be("tok");
        verification.IsActive.Should().BeTrue();
        client.Calls.Should().ContainSingle()
            .Which.Path.Should().Be(Endpoints.VerifyToken);
    }

    [Fact]
    public async Task VerifyAsync_NullResult_ThrowsDecoding()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<TokenVerification>(true, [], [], null, null));
        var svc = new AuthService(client);

        var act = async () => await svc.VerifyAsync();

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Decoding>();
    }

    [Fact]
    public async Task ListAccountsAsync_NullResult_ReturnsEmptyList()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<Account[]>(true, [], [], null, null));
        var svc = new AuthService(client);

        var accounts = await svc.ListAccountsAsync();

        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAccountsAsync_SendsPerPageFifty()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<Account[]>(true, [], [], [new Account("a", "A")], null));
        var svc = new AuthService(client);

        await svc.ListAccountsAsync();

        var call = client.Calls.Should().ContainSingle().Subject;
        call.Path.Should().Be(Endpoints.Accounts);
        call.Query.Should().BeEquivalentTo(new (string, string)[] { ("per_page", "50") });
    }

    [Fact]
    public async Task ValidateAndFetchAccountsAsync_CombinesBothCalls()
    {
        var client = new FakeApiClient()
            .EnqueueGet(new ApiResponse<TokenVerification>(true, [], [], new TokenVerification("t", "active"), null))
            .EnqueueGet(new ApiResponse<Account[]>(true, [], [], [new Account("a", "Acme")], null));
        var svc = new AuthService(client);

        var result = await svc.ValidateAndFetchAccountsAsync();

        result.Verification.Id.Should().Be("t");
        result.Accounts.Should().ContainSingle().Which.Name.Should().Be("Acme");
        client.Calls.Should().HaveCount(2);
    }

    [Fact]
    public void DeriveAccountsFromZones_SkipsZonesWithoutAccount()
    {
        var zones = new List<Zone>
        {
            new("z1", "a.com", "active", null, "acc-1", "Alpha", "Free", null),
            new("z2", "b.com", "active", null, null, null, null, null),
            new("z3", "c.com", "active", null, "acc-2", "Beta", null, null),
            new("z4", "d.com", "active", null, "acc-1", "Alpha", null, null),
        };

        var accounts = AuthService.DeriveAccountsFromZones(zones);

        accounts.Should().HaveCount(2);
        accounts.Should().BeInAscendingOrder(a => a.Name);
        accounts[0].Should().Be(new Account("acc-1", "Alpha"));
        accounts[1].Should().Be(new Account("acc-2", "Beta"));
    }

    [Fact]
    public void DeriveAccountsFromZones_CaseInsensitiveSort()
    {
        var zones = new List<Zone>
        {
            new("z1", "a.com", "active", null, "id-b", "beta", null, null),
            new("z2", "a.com", "active", null, "id-a", "Alpha", null, null),
        };

        var accounts = AuthService.DeriveAccountsFromZones(zones);

        accounts[0].Name.Should().Be("Alpha");
        accounts[1].Name.Should().Be("beta");
    }
}

using System.Text.Json;
using FlarePurge.Core.Api;
using FlarePurge.Core.Json;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class ApiResponseTests
{
    [Fact]
    public void Decode_SuccessfulTokenVerify_MapsResult()
    {
        const string json = """
            {
              "success": true,
              "errors": [],
              "messages": [],
              "result": { "id": "token-id", "status": "active" }
            }
            """;

        var response = JsonSerializer.Deserialize(
            json,
            CoreJsonContext.Default.ApiResponseTokenVerification);

        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Errors.Should().BeEmpty();
        response.Messages.Should().BeEmpty();
        response.Result!.Id.Should().Be("token-id");
        response.Result.IsActive.Should().BeTrue();
        response.ResultInfo.Should().BeNull();
    }

    [Fact]
    public void Decode_ErrorEnvelope_CapturesCodeAndMessage()
    {
        const string json = """
            {
              "success": false,
              "errors": [
                { "code": 10000, "message": "Authentication error" }
              ],
              "messages": [],
              "result": null
            }
            """;

        var response = JsonSerializer.Deserialize(
            json,
            CoreJsonContext.Default.ApiResponseTokenVerification);

        response!.Success.Should().BeFalse();
        response.Result.Should().BeNull();
        response.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ApiError(10000, "Authentication error", null));
    }

    [Fact]
    public void Decode_NestedErrorChain_IsPreserved()
    {
        const string json = """
            {
              "success": false,
              "errors": [
                { "code": 1000, "message": "outer",
                  "error_chain": [ { "code": 1001, "message": "inner" } ] }
              ],
              "messages": [],
              "result": null
            }
            """;

        var response = JsonSerializer.Deserialize(
            json,
            CoreJsonContext.Default.ApiResponseTokenVerification);

        var outer = response!.Errors.Should().ContainSingle().Subject;
        outer.ErrorChain.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ApiError(1001, "inner", null));
    }

    [Fact]
    public void Decode_ResultInfoWithPages_PopulatesHasMorePages()
    {
        const string json = """
            {
              "success": true,
              "errors": [],
              "messages": [],
              "result": null,
              "result_info": {
                "page": 1, "per_page": 50, "total_pages": 4, "count": 50, "total_count": 175
              }
            }
            """;

        var response = JsonSerializer.Deserialize(
            json,
            CoreJsonContext.Default.ApiResponseTokenVerification);

        var info = response!.ResultInfo!;
        info.Page.Should().Be(1);
        info.PerPage.Should().Be(50);
        info.TotalPages.Should().Be(4);
        info.TotalCount.Should().Be(175);
        info.HasMorePages.Should().BeTrue();
    }

    [Fact]
    public void ResultInfo_LastPage_HasMorePagesFalse()
    {
        var info = new ResultInfo(Page: 4, PerPage: 50, TotalPages: 4, Count: 25, TotalCount: 175);

        info.HasMorePages.Should().BeFalse();
    }

    [Fact]
    public void ResultInfo_MissingPageMetadata_HasMorePagesFalse()
    {
        new ResultInfo(null, null, null, null, null).HasMorePages.Should().BeFalse();
    }

    [Fact]
    public void Decode_ZonesArrayEnvelope_ParsesListOfZones()
    {
        const string json = """
            {
              "success": true,
              "errors": [],
              "messages": [],
              "result": [
                { "id": "z1", "name": "a.com", "status": "active",
                  "account": { "id": "acc", "name": "A" }, "plan": { "name": "Free" },
                  "created_on": "2023-01-15T10:30:00Z" },
                { "id": "z2", "name": "b.com", "status": "pending" }
              ],
              "result_info": { "page": 1, "per_page": 50, "total_pages": 1, "count": 2, "total_count": 2 }
            }
            """;

        var response = JsonSerializer.Deserialize(
            json,
            CoreJsonContext.Default.ApiResponseZoneArray);

        response!.Success.Should().BeTrue();
        response.Result.Should().HaveCount(2);
        response.Result![0].Name.Should().Be("a.com");
        response.Result[0].AccountId.Should().Be("acc");
        response.Result[1].IsActive.Should().BeFalse();
        response.ResultInfo!.HasMorePages.Should().BeFalse();
    }
}

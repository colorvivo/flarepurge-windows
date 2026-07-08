using FlarePurge.Core;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests;

public class PlaceholderTests
{
    [Fact]
    public void ApiVersion_IsSemver()
    {
        Placeholder.ApiVersion
            .Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }
}

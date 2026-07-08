using FlarePurge.Core.Localization;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Localization;

public class LocalizationKeysTests
{
    [Theory]
    [InlineData("error.unauthorized.invalid", "error_unauthorized_invalid")]
    [InlineData("error.server", "error_server")]
    [InlineData("already_safe", "already_safe")]
    [InlineData("", "")]
    public void Normalize_ReplacesDotsWithUnderscores(string input, string expected)
    {
        LocalizationKeys.Normalize(input).Should().Be(expected);
    }
}

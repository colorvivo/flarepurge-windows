using System;
using FlarePurge.Core.Localization;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Localization;

public class LocalizerTests : IDisposable
{
    public LocalizerTests() => Localizer.ResetResolver();
    public void Dispose() => Localizer.ResetResolver();

    [Fact]
    public void Default_ReturnsKeyUnchanged()
        => Localizer.Get("some.key").Should().Be("some.key");

    [Fact]
    public void SetResolver_OverridesLookup()
    {
        Localizer.SetResolver(key => $"[{key}]");

        Localizer.Get("hello").Should().Be("[hello]");
    }

    [Fact]
    public void ResetResolver_RestoresIdentity()
    {
        Localizer.SetResolver(_ => "x");
        Localizer.ResetResolver();

        Localizer.Get("k").Should().Be("k");
    }

    [Fact]
    public void SetResolver_Null_Throws()
    {
        var act = () => Localizer.SetResolver(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

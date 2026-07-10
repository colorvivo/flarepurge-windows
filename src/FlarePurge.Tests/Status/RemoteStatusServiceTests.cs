using FlarePurge.Core.Status;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Status;

public class RemoteStatusServiceTests
{
    [Fact]
    public void Clamp_ShortMessage_Unchanged()
    {
        var status = new RemoteStatus(Disabled: true, Message: "Down for maintenance.");

        RemoteStatusService.Clamp(status).Should().BeSameAs(status);
    }

    [Fact]
    public void Clamp_NullMessage_Unchanged()
    {
        var status = new RemoteStatus(Disabled: true, Message: null);

        RemoteStatusService.Clamp(status).Should().BeSameAs(status);
    }

    [Fact]
    public void Clamp_OverlongMessage_TruncatedToLimit()
    {
        var longMessage = new string('x', RemoteStatusService.MaxMessageChars + 500);
        var status = new RemoteStatus(Disabled: true, Message: longMessage);

        var clamped = RemoteStatusService.Clamp(status);

        clamped.Message.Should().HaveLength(RemoteStatusService.MaxMessageChars);
        clamped.Disabled.Should().BeTrue();
    }
}

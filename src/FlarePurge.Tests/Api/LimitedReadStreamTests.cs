using System;
using System.IO;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class LimitedReadStreamTests
{
    private static MemoryStream Bytes(int n) => new(new byte[n]);

    [Fact]
    public void Read_WithinLimit_ReturnsAllBytes()
    {
        using var s = new LimitedReadStream(Bytes(100), maxBytes: 100);
        var buffer = new byte[256];

        var total = 0;
        int r;
        while ((r = s.Read(buffer, 0, buffer.Length)) > 0) total += r;

        total.Should().Be(100);
    }

    [Fact]
    public void Read_PastLimit_ThrowsIOException()
    {
        using var s = new LimitedReadStream(Bytes(101), maxBytes: 100);
        var buffer = new byte[256];

        var act = () => s.Read(buffer, 0, buffer.Length);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public async Task ReadAsync_PastLimit_ThrowsIOException()
    {
        await using var s = new LimitedReadStream(Bytes(4096), maxBytes: 1024);
        var buffer = new byte[8192];

        var act = async () => await s.ReadAsync(buffer);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public void CtorNullInner_Throws()
    {
        var act = () => new LimitedReadStream(null!, 10);

        act.Should().Throw<ArgumentNullException>();
    }
}

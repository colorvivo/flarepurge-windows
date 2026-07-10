using System;
using System.IO;
using System.Linq;
using System.Text;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class AtomicFileTests : IDisposable
{
    private readonly string _dir;

    public AtomicFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fp-atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static void WriteText(Stream s, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
    }

    [Fact]
    public void Write_CreatesFileWithContent()
    {
        var path = Path.Combine(_dir, "sub", "data.json");

        AtomicFile.Write(path, s => WriteText(s, "hello"));

        File.ReadAllText(path).Should().Be("hello");
    }

    [Fact]
    public void Write_OverwritesExisting()
    {
        var path = Path.Combine(_dir, "data.json");
        AtomicFile.Write(path, s => WriteText(s, "first"));

        AtomicFile.Write(path, s => WriteText(s, "second"));

        File.ReadAllText(path).Should().Be("second");
    }

    [Fact]
    public void Write_LeavesNoTempFileBehind()
    {
        var path = Path.Combine(_dir, "data.json");

        AtomicFile.Write(path, s => WriteText(s, "x"));

        Directory.GetFiles(_dir).Where(f => f.EndsWith(".tmp", StringComparison.Ordinal))
            .Should().BeEmpty();
    }

    [Fact]
    public void Write_ContentActionThrows_NoTempLeftAndOriginalIntact()
    {
        var path = Path.Combine(_dir, "data.json");
        AtomicFile.Write(path, s => WriteText(s, "original"));

        var act = () => AtomicFile.Write(path, _ => throw new InvalidOperationException("boom"));

        act.Should().Throw<InvalidOperationException>();
        File.ReadAllText(path).Should().Be("original"); // untouched
        Directory.GetFiles(_dir).Where(f => f.EndsWith(".tmp", StringComparison.Ordinal))
            .Should().BeEmpty();
    }
}

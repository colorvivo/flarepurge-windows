using System;
using System.IO;

namespace FlarePurge.Core.Auth;

internal static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="path"/> by streaming into a per-process-unique temp
    /// file and atomically moving it into place. The unique suffix (process id +
    /// guid) stops two FlarePurge instances from colliding on a shared
    /// <c>.tmp</c> path (audit C7); the temp file is removed if the write or move
    /// throws, so a failure never leaves a stray partial file behind.
    /// </summary>
    public static void Write(string path, Action<Stream> writeContent)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var tempPath = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
                writeContent(stream);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }
    }
}

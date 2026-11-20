using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace TryashtarUtils.Utility;

public static class IOUtils
{
    // GetFiles but with multiple allowed file extensions
    public static IEnumerable<FilePath> GetValidFiles(FilePath directory, IEnumerable<string> extensions,
        bool recursive)
    {
        return ScanFiles(directory, new FilePath(), extensions, recursive, new FilePath[0]);
    }

    public static IEnumerable<FilePath> GetValidFiles(FilePath directory, IEnumerable<string> extensions,
        bool recursive, IEnumerable<FilePath> exclude)
    {
        return ScanFiles(directory, new FilePath(), extensions, recursive, exclude);
    }

    private static IEnumerable<FilePath> ScanFiles(FilePath original_path, FilePath relative_deeper,
        IEnumerable<string> extensions, bool recursive, IEnumerable<FilePath> exclude)
    {
        string dir = String.Join(Path.DirectorySeparatorChar.ToString(), original_path.CombineWith(relative_deeper));
        var valid = Directory.GetFiles(dir)
            .Where(x => extensions.Contains(Path.GetExtension(x).ToLower()))
            .OrderBy(x => x, LogicalStringComparer.Instance) // sort entries by logical comparer
            .Select(x => new FilePath(x));
        if (recursive)
        {
            foreach (var subfolder in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(subfolder);
                var final = original_path.CombineWith(relative_deeper).CombineWith(name);
                if (!exclude.Any(x => x.StartsWith(final)))
                {
                    var more = ScanFiles(original_path, relative_deeper.CombineWith(name), extensions, true, exclude);
                    valid = valid.Concat(more);
                }
            }
        }

        return valid;
    }

    public static byte[] ReadBytes(FileStream stream, int count)
    {
        byte[] bytes = new byte[count];
        stream.Read(bytes, 0, count);
        return bytes;
    }

    public static string GetUniqueFilename(string full_path)
    {
        if (!Path.IsPathRooted(full_path))
            full_path = Path.GetFullPath(full_path);
        if (File.Exists(full_path))
        {
            string filename = Path.GetFileName(full_path);
            string path = full_path.Substring(0, full_path.Length - filename.Length);
            string no_extension = Path.GetFileNameWithoutExtension(full_path);
            string ext = Path.GetExtension(full_path);
            int n = 1;
            do
            {
                full_path = Path.Combine(path, String.Format("{0} ({1}){2}", no_extension, (n++), ext));
            } while (File.Exists(full_path));
        }

        return full_path;
    }

    public static void OpenUrlInBrowser(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    public static void WipeDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }

    public static List<ZipArchiveEntry> CreateEntryFromAny(this ZipArchive archive, string sourceName, string entryName,
        CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        var list = new List<ZipArchiveEntry>();
        if (Directory.Exists(sourceName))
            list.AddRange(archive.CreateEntryFromDirectory(sourceName, entryName));
        else
            list.Add(archive.CreateEntryFromFile(sourceName, entryName, compressionLevel));
        return list;
    }

    public static List<ZipArchiveEntry> CreateEntryFromDirectory(this ZipArchive archive, string sourceDirName,
        string entryName, CompressionLevel compressionLevel = CompressionLevel.Fastest,
        Func<string, bool>? predicate = null)
    {
        var list = new List<ZipArchiveEntry>();
        foreach (var file in Directory.GetFiles(sourceDirName))
        {
            if (predicate == null || predicate(file))
                list.Add(archive.CreateEntryFromFile(file, Path.Combine(entryName, Path.GetFileName(file)),
                    compressionLevel));
        }

        foreach (var file in Directory.GetDirectories(sourceDirName))
        {
            if (predicate == null || predicate(file))
                list.AddRange(archive.CreateEntryFromDirectory(file, Path.Combine(entryName, Path.GetFileName(file)),
                    compressionLevel, predicate));
        }

        return list;
    }

    public static void ExtractDirectoryEntry(this ZipArchive archive, string entryName, string destName,
        bool overwriteFiles)
    {
        entryName += '/';
        foreach (var item in archive.Entries)
        {
            if (item.FullName.Replace('\\', '/').StartsWith(entryName))
            {
                string relative = item.FullName.Substring(entryName.Length);
                string file = Path.Combine(destName, relative);
                string? parent = Path.GetDirectoryName(file);
                if (parent != null)
                    Directory.CreateDirectory(parent);
                item.ExtractToFile(file, overwriteFiles);
            }
        }
    }

    private static readonly string[] ReservedFilenames = new[]
    {
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
        "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
        "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string MakeFilesafe(string name)
    {
        var regex = new Regex($"[{new String(Path.GetInvalidFileNameChars())}]+");
        var result = regex.Replace(name, "_");
        foreach (var reserved in ReservedFilenames)
        {
            var reservedWordPattern = string.Format("^{0}\\.", reserved);
            result = Regex.Replace(result, $"^{reserved}\\.", "_" + reserved, RegexOptions.IgnoreCase);
        }

        return result;
    }
}
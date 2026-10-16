using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using KeyDeck.Infrastructure;

namespace KeyDeck.Core;

/// <summary>
/// Extracts and caches application icons from executables as frozen WPF <see cref="BitmapImage"/> objects.
/// Supports in-memory caching and persistent on-disk PNG caching invalidated by file modification timestamp.
/// </summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, BitmapImage> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, BitmapImage> GetCachedIconsSnapshot()
    {
        lock (MemoryCache)
        {
            return new ReadOnlyDictionary<string, BitmapImage>(new Dictionary<string, BitmapImage>(MemoryCache));
        }
    }

    public static void ClearCache()
    {
        lock (MemoryCache)
        {
            MemoryCache.Clear();
        }
    }

    /// <summary>
    /// Extracts the associated icon for the specified executable into <paramref name="iconsDirectory"/>
    /// and returns the cached PNG file path on disk, or <see langword="null"/> if extraction failed.
    /// </summary>
    public static string? ExtractIconToCacheFile(string? exePath, string iconsDirectory)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        var fullPath = ResolveExecutablePath(exePath);
        if (fullPath == null)
        {
            return null;
        }

        try
        {
            var lastWriteTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            var hash = HashHelper.ComputeSha256($"{fullPath}|{lastWriteTicks}");
            var cachedPngPath = Path.Combine(iconsDirectory, $"{hash}.png");

            if (!File.Exists(cachedPngPath))
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(fullPath);
                if (sysIcon != null)
                {
                    using var bitmap = sysIcon.ToBitmap();
                    Directory.CreateDirectory(iconsDirectory);
                    bitmap.Save(cachedPngPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            return File.Exists(cachedPngPath) ? cachedPngPath : null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[IconExtractor] Failed to extract icon to cache file for '{exePath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts the associated icon for the specified executable or fetches it from cache.
    /// </summary>
    public static BitmapImage? ExtractOrGetCachedIcon(string? exePath, string iconsDirectory)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        var fullPath = ResolveExecutablePath(exePath);
        if (fullPath == null)
        {
            return null;
        }

        try
        {
            var lastWriteTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            var cacheKey = $"{fullPath}|{lastWriteTicks}";

            lock (MemoryCache)
            {
                if (MemoryCache.TryGetValue(cacheKey, out var cachedImg))
                {
                    return cachedImg;
                }
            }

            var cachedPngPath = ExtractIconToCacheFile(fullPath, iconsDirectory);
            if (!string.IsNullOrEmpty(cachedPngPath) && File.Exists(cachedPngPath))
            {
                var bitmapImage = LoadFrozenBitmap(cachedPngPath);
                if (bitmapImage != null)
                {
                    lock (MemoryCache)
                    {
                        MemoryCache[cacheKey] = bitmapImage;
                    }
                    return bitmapImage;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[IconExtractor] Extraction error for '{exePath}': {ex.Message}");
        }

        return null;
    }

    public static BitmapImage? LoadFrozenBitmap(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var image = new BitmapImage();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[IconExtractor] Failed to load frozen bitmap from '{filePath}': {ex.Message}");
            return null;
        }
    }

    private static string? ResolveExecutablePath(string rawPath)
    {
        var fullPath = Environment.ExpandEnvironmentVariables(rawPath.Trim());
        if (Path.IsPathRooted(fullPath) && File.Exists(fullPath))
        {
            return fullPath;
        }

        return ResolveFromPath(fullPath);
    }

    private static string? ResolveFromPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var extensions = new[] { ".exe", ".bat", ".cmd", "" };
        var paths = pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            foreach (var ext in extensions)
            {
                try
                {
                    var candidate = fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                        ? fileName
                        : fileName + ext;
                    var test = Path.Combine(p.Trim(), candidate);
                    if (File.Exists(test))
                    {
                        return test;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries
                }
            }
        }

        return null;
    }
}

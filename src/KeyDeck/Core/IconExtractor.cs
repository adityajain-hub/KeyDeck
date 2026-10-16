using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using KeyDeck.Infrastructure;

namespace KeyDeck.Core;

/// <summary>
/// Extracts and caches application icons from executables as frozen WPF <see cref="BitmapImage"/> objects.
/// Supports in-memory caching and persistent on-disk PNG caching.
/// </summary>
public static class IconExtractor
{
    private static readonly Dictionary<string, BitmapImage> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a snapshot of the current in-memory icon cache.
    /// </summary>
    /// <returns>A read-only dictionary of executable paths to cached bitmap images.</returns>
    public static IReadOnlyDictionary<string, BitmapImage> GetCachedIconsSnapshot()
    {
        lock (MemoryCache)
        {
            return new ReadOnlyDictionary<string, BitmapImage>(new Dictionary<string, BitmapImage>(MemoryCache));
        }
    }

    /// <summary>
    /// Clears the in-memory icon cache.
    /// </summary>
    public static void ClearCache()
    {
        lock (MemoryCache)
        {
            MemoryCache.Clear();
        }
    }

    /// <summary>
    /// Extracts the associated icon for the specified executable or fetches it from cache.
    /// </summary>
    /// <param name="exePath">Path to the target executable or script.</param>
    /// <param name="iconsDirectory">The directory path where extracted icon PNGs are cached on disk.</param>
    /// <returns>A frozen <see cref="BitmapImage"/> if extraction succeeded; otherwise, <see langword="null"/>.</returns>
    public static BitmapImage? ExtractOrGetCachedIcon(string? exePath, string iconsDirectory)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        var fullPath = Environment.ExpandEnvironmentVariables(exePath);

        // If relative or just command name, try finding in PATH
        if (!Path.IsPathRooted(fullPath) || !File.Exists(fullPath))
        {
            var resolved = ResolveFromPath(fullPath);
            if (resolved != null)
            {
                fullPath = resolved;
            }
            else
            {
                return null;
            }
        }

        lock (MemoryCache)
        {
            if (MemoryCache.TryGetValue(fullPath, out var cachedImg))
            {
                return cachedImg;
            }
        }

        try
        {
            var hash = HashHelper.ComputeSha256(fullPath);
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

            if (File.Exists(cachedPngPath))
            {
                var bitmapImage = LoadFrozenBitmap(cachedPngPath);
                if (bitmapImage != null)
                {
                    lock (MemoryCache)
                    {
                        MemoryCache[fullPath] = bitmapImage;
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

    /// <summary>
    /// Loads an image file from disk and returns a frozen, UI-thread-independent <see cref="BitmapImage"/>.
    /// </summary>
    /// <param name="filePath">The file path of the image to load.</param>
    /// <returns>A frozen <see cref="BitmapImage"/>, or <see langword="null"/> if file does not exist or fails to load.</returns>
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

    private static string? ResolveFromPath(string fileName)
    {
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
                var test = Path.Combine(p.Trim(), fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ext);
                if (File.Exists(test))
                {
                    return test;
                }
            }
        }

        return null;
    }
}

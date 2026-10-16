using System.Security.Cryptography;
using System.Text;

namespace KeyDeck.Infrastructure;

/// <summary>
/// Provides cryptographic hashing utility methods used for caching keys, identifiers, and file content.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Computes a lowercase hexadecimal SHA-256 hash string for the specified input string.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>A 64-character lowercase hexadecimal hash string, or empty string if input is empty.</returns>
    public static string ComputeSha256(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var inputBytes = Encoding.UTF8.GetBytes(input.ToLowerInvariant());
        var hashBytes = SHA256.HashData(inputBytes);

        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}

using System.Collections;

namespace KeyDeck.Tests.Framework;

/// <summary>
/// Lightweight, zero-dependency assertion framework for testing KeyDeck.
/// </summary>
public static class Assert
{
    public static int AssertionCount { get; private set; }

    public static void ResetCount()
    {
        AssertionCount = 0;
    }

    public static void True(bool condition, string? message = null)
    {
        AssertionCount++;
        if (!condition)
        {
            throw new AssertionException(message ?? "Expected condition to be true, but was false.");
        }
    }

    public static void False(bool condition, string? message = null)
    {
        AssertionCount++;
        if (condition)
        {
            throw new AssertionException(message ?? "Expected condition to be false, but was true.");
        }
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        AssertionCount++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException(message ?? $"Expected '{expected}' but found '{actual}'.");
        }
    }

    public static void NotEqual<T>(T notExpected, T actual, string? message = null)
    {
        AssertionCount++;
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new AssertionException(message ?? $"Expected value not to be '{notExpected}', but it was.");
        }
    }

    public static void Null(object? obj, string? message = null)
    {
        AssertionCount++;
        if (obj != null)
        {
            throw new AssertionException(message ?? $"Expected null, but found '{obj}'.");
        }
    }

    public static void NotNull(object? obj, string? message = null)
    {
        AssertionCount++;
        if (obj == null)
        {
            throw new AssertionException(message ?? "Expected not null, but found null.");
        }
    }

    public static void Contains(string expectedSubstring, string? actual, string? message = null)
    {
        AssertionCount++;
        if (actual == null || !actual.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException(message ?? $"Expected string to contain '{expectedSubstring}', but was '{actual ?? "(null)"}'.");
        }
    }

    public static void NotEmpty(IEnumerable? collection, string? message = null)
    {
        AssertionCount++;
        if (collection == null)
        {
            throw new AssertionException(message ?? "Expected collection to be non-empty, but was null.");
        }

        var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new AssertionException(message ?? "Expected collection to be non-empty, but contained 0 elements.");
        }
    }

    public static TException Throws<TException>(Action action, string? message = null) where TException : Exception
    {
        AssertionCount++;
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException(message ?? $"Expected exception of type {typeof(TException).Name}, but caught {ex.GetType().Name}: {ex.Message}");
        }

        throw new AssertionException(message ?? $"Expected exception of type {typeof(TException).Name}, but no exception was thrown.");
    }
}

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Abstraction for interacting with the operating system environment.
/// Decouples file-system paths, process information, and environment variables
/// from domain and infrastructure logic to enable deterministic testing.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>
    /// Gets the unique identifier for the current running process.
    /// </summary>
    int CurrentProcessId { get; }

    /// <summary>
    /// Expands environment variable expressions (e.g. <c>%USERPROFILE%</c>) within the input string.
    /// </summary>
    /// <param name="name">The string containing zero or more environment variable names formatted as <c>%variable%</c>.</param>
    /// <returns>A string with each environment variable replaced by its value.</returns>
    string ExpandEnvironmentVariables(string name);

    /// <summary>
    /// Retrieves the value of an environment variable from the current process.
    /// </summary>
    /// <param name="variable">The name of the environment variable.</param>
    /// <returns>The value of the environment variable, or <see langword="null"/> if not found.</returns>
    string? GetEnvironmentVariable(string variable);

    /// <summary>
    /// Gets the path to the system special folder identified by the specified enumeration.
    /// </summary>
    /// <param name="folder">An enumerated constant that identifies a system special folder.</param>
    /// <returns>The path to the specified system special folder, or empty string if it does not physically exist.</returns>
    string GetFolderPath(Environment.SpecialFolder folder);

    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    bool FileExists(string? path);

    /// <summary>
    /// Determines whether the given path refers to an existing directory on disk.
    /// </summary>
    bool DirectoryExists(string? path);
}

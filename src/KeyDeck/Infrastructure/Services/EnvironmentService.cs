using System.IO;
using KeyDeck.Core.Abstractions;

namespace KeyDeck.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="IEnvironmentService"/> delegating to system <see cref="System.Environment"/>
/// and <see cref="System.IO.File"/> APIs.
/// </summary>
public sealed class EnvironmentService : IEnvironmentService
{
    public static EnvironmentService Instance { get; } = new();

    public int CurrentProcessId => Environment.ProcessId;

    public string ExpandEnvironmentVariables(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? name : Environment.ExpandEnvironmentVariables(name);
    }

    public string? GetEnvironmentVariable(string variable)
    {
        return Environment.GetEnvironmentVariable(variable);
    }

    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return Environment.GetFolderPath(folder);
    }

    public bool FileExists(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public bool DirectoryExists(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }
}

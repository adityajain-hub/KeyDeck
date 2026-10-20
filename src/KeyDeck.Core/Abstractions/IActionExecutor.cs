using KeyDeck.Core.Models;

namespace KeyDeck.Core.Abstractions;

/// <summary>
/// Defines the contract for asynchronously executing macro actions.
/// Isolates execution logic from callers, enabling mocking and future
/// sandboxing or auditing middleware.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Executes <paramref name="action"/> asynchronously without blocking the calling thread.
    /// A <see langword="null"/> action or one of type <see cref="ActionType.None"/> is silently ignored.
    /// </summary>
    /// <param name="action">The action to execute. May be <see langword="null"/>.</param>
    Task ExecuteAsync(KeyAction? action);
}

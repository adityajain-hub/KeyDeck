using System.Text.Json.Serialization;

namespace KeyDeck.Core.Models;

/// <summary>
/// A named profile containing a full 16-key numpad mapping and an optional linked executable for auto-switching.
/// </summary>
public class Profile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional executable name(s) (e.g., <c>Code.exe</c> or <c>obs64.exe</c>) that automatically
    /// activate this profile when brought to the foreground while Auto-Profile Switching is enabled.
    /// </summary>
    [JsonPropertyName("linkedProcessName")]
    public string? LinkedProcessName { get; set; }

    [JsonPropertyName("bindings")]
    public Dictionary<NumpadKeyId, KeyAction> Bindings { get; set; } = new();

    public Profile() { }

    public Profile(string name)
    {
        Name = name;
    }

    public KeyAction GetAction(NumpadKeyId keyId)
    {
        if (Bindings.TryGetValue(keyId, out var action))
        {
            return action;
        }

        return new KeyAction { Type = ActionType.None };
    }

    public void SetAction(NumpadKeyId keyId, KeyAction action)
    {
        Bindings[keyId] = action;
    }

    public Profile Clone(string newName)
    {
        var cloned = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            LinkedProcessName = LinkedProcessName
        };

        foreach (var kvp in Bindings)
        {
            cloned.Bindings[kvp.Key] = kvp.Value.Clone();
        }

        return cloned;
    }
}

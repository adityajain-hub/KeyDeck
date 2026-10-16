using System.Text.Json.Serialization;

namespace KeyDeck.Core.Models;

/// <summary>
/// A named profile containing a full 16-key numpad mapping.
/// </summary>
public class Profile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bindings")]
    public Dictionary<NumpadKeyId, KeyAction> Bindings { get; set; } = new();

    public Profile() { }

    public Profile(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the action assigned to a given key, or a default empty action if none exists.
    /// This method is a pure read and does not mutate the bindings collection.
    /// </summary>
    public KeyAction GetAction(NumpadKeyId keyId)
    {
        if (Bindings.TryGetValue(keyId, out var action))
        {
            return action;
        }

        return new KeyAction { Type = ActionType.None };
    }

    /// <summary>
    /// Assigns or updates the action for a given key.
    /// </summary>
    public void SetAction(NumpadKeyId keyId, KeyAction action)
    {
        Bindings[keyId] = action;
    }

    /// <summary>
    /// Deep clones the profile with a fresh unique ID.
    /// </summary>
    public Profile Clone(string newName)
    {
        var cloned = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName
        };

        foreach (var kvp in Bindings)
        {
            cloned.Bindings[kvp.Key] = kvp.Value.Clone();
        }

        return cloned;
    }
}

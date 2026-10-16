using System.Text.Json.Serialization;

namespace KeyDeck.Core.Models;

/// <summary>
/// Pairs a physical numpad key with an assigned macro action.
/// </summary>
public class KeyBinding
{
    [JsonPropertyName("keyId")]
    public NumpadKeyId KeyId { get; set; }

    [JsonPropertyName("action")]
    public KeyAction Action { get; set; } = new();

    public KeyBinding() { }

    public KeyBinding(NumpadKeyId keyId, KeyAction action)
    {
        KeyId = keyId;
        Action = action;
    }
}

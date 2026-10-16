using System.Text.Json.Serialization;

namespace KeyDeck.Core.Models;

/// <summary>
/// Flat serialization model defining a single macro action.
/// </summary>
public class KeyAction
{
    [JsonPropertyName("type")]
    public ActionType Type { get; set; } = ActionType.None;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("iconPath")]
    public string? IconPath { get; set; }

    [JsonPropertyName("targetPath")]
    public string? TargetPath { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("hotkeyVirtualKeys")]
    public List<int> HotkeyVirtualKeys { get; set; } = new();

    [JsonPropertyName("hotkeyDisplay")]
    public string? HotkeyDisplay { get; set; }

    [JsonPropertyName("mediaCommand")]
    public MediaCommand MediaCommand { get; set; } = MediaCommand.PlayPause;

    [JsonPropertyName("systemCommand")]
    public SystemCommand SystemCommand { get; set; } = SystemCommand.LockWorkstation;

    [JsonPropertyName("textToPaste")]
    public string? TextToPaste { get; set; }

    [JsonPropertyName("delayMs")]
    public int DelayMs { get; set; } = 100;

    [JsonPropertyName("subActions")]
    public List<KeyAction> SubActions { get; set; } = new();

    /// <summary>
    /// Creates a deep copy of this action.
    /// </summary>
    public KeyAction Clone()
    {
        return new KeyAction
        {
            Type = Type,
            Label = Label,
            IconPath = IconPath,
            TargetPath = TargetPath,
            Arguments = Arguments,
            HotkeyVirtualKeys = new List<int>(HotkeyVirtualKeys),
            HotkeyDisplay = HotkeyDisplay,
            MediaCommand = MediaCommand,
            SystemCommand = SystemCommand,
            TextToPaste = TextToPaste,
            DelayMs = DelayMs,
            SubActions = SubActions.Select(s => s.Clone()).ToList()
        };
    }
}

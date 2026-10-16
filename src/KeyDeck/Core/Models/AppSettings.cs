using System.Text.Json.Serialization;

namespace KeyDeck.Core.Models;

/// <summary>
/// Global application configuration settings.
/// </summary>
public class AppSettings
{
    [JsonPropertyName("activeProfileId")]
    public string ActiveProfileId { get; set; } = string.Empty;

    [JsonPropertyName("startOnBoot")]
    public bool StartOnBoot { get; set; } = false;

    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; } = false;

    [JsonPropertyName("numLockMode")]
    public NumLockMacroMode NumLockMode { get; set; } = NumLockMacroMode.OffMeansMacro;

    [JsonPropertyName("hudMode")]
    public HudVisibilityMode HudMode { get; set; } = HudVisibilityMode.AlwaysOnWhenNumLockOff;

    [JsonPropertyName("hudOpacity")]
    public double HudOpacity { get; set; } = 0.85;

    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    [JsonPropertyName("isMacroEnabled")]
    public bool IsMacroEnabled { get; set; } = true;
}

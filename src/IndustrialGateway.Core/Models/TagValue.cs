namespace IndustrialGateway.Core.Models;

/// <summary>
/// Represents a tag value with metadata
/// </summary>
public class TagValue
{
    /// <summary>
    /// Tag identifier
    /// </summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// Tag name
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// The raw value
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Timestamp when the value was read
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quality indicator
    /// </summary>
    public TagQuality Quality { get; set; } = TagQuality.Good;

    /// <summary>
    /// Optional error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        return $"{TagName}: {Value} ({Quality}) @ {Timestamp:HH:mm:ss.fff}";
    }
}

/// <summary>
/// Quality indicator for tag values
/// </summary>
public enum TagQuality
{
    Good,
    Bad,
    Uncertain
}

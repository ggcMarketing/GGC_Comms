namespace IndustrialGateway.Core.Models;

/// <summary>
/// Represents a tag or data point definition for reading/writing
/// </summary>
public class TagDefinition
{
    /// <summary>
    /// Unique identifier for this tag
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable name for this tag
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Protocol-specific address (e.g., "40001" for Modbus, "ns=2;s=MyVariable" for OPC UA)
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Data type of the tag
    /// </summary>
    public TagDataType DataType { get; set; } = TagDataType.Int16;

    /// <summary>
    /// Whether this tag is read-only
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// Scan rate in milliseconds (for subscriptions)
    /// </summary>
    public int ScanRateMs { get; set; } = 1000;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Common data types for industrial tags
/// </summary>
public enum TagDataType
{
    Bool,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String
}

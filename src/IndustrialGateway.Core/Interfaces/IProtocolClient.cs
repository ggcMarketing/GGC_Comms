using IndustrialGateway.Core.Models;

namespace IndustrialGateway.Core.Interfaces;

/// <summary>
/// Interface for all protocol client implementations
/// </summary>
public interface IProtocolClient : IDisposable
{
    /// <summary>
    /// Current connection state
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Event raised when connection state changes
    /// </summary>
    event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// Event raised when a subscribed tag value changes
    /// </summary>
    event EventHandler<TagValue>? TagValueChanged;

    /// <summary>
    /// Connect to the device/server asynchronously
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the device/server asynchronously
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a single tag value
    /// </summary>
    /// <param name="tag">Tag definition to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tag value</returns>
    Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read multiple tag values
    /// </summary>
    /// <param name="tags">Tag definitions to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of tag values</returns>
    Task<IEnumerable<TagValue>> ReadTagsAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a value to a tag
    /// </summary>
    /// <param name="tag">Tag definition to write to</param>
    /// <param name="value">Value to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteTagAsync(TagDefinition tag, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write values to multiple tags
    /// </summary>
    /// <param name="tagValues">Dictionary of tag definitions and values to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteTagsAsync(IDictionary<TagDefinition, object> tagValues, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to tag value changes
    /// </summary>
    /// <param name="tags">Tags to subscribe to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from tag value changes
    /// </summary>
    /// <param name="tags">Tags to unsubscribe from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from all tag value changes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}

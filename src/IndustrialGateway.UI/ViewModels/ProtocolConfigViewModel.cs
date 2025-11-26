using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// Base ViewModel for protocol configuration screens
/// </summary>
public abstract class ProtocolConfigViewModel : ViewModelBase, IDisposable
{
    protected readonly IProtocolClient _client;
    protected readonly ILogger _logger;

    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string _statusMessage = "Disconnected";
    private TagDefinition? _selectedTag;

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        set
        {
            if (SetProperty(ref _connectionState, value))
            {
                UpdateStatusMessage();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public TagDefinition? SelectedTag
    {
        get => _selectedTag;
        set => SetProperty(ref _selectedTag, value);
    }

    public ObservableCollection<TagDefinition> Tags { get; } = new();
    public ObservableCollection<TagValue> TagValues { get; } = new();

    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand TestReadCommand { get; }
    public AsyncRelayCommand TestWriteCommand { get; }
    public RelayCommand AddTagCommand { get; }
    public RelayCommand RemoveTagCommand { get; }

    protected ProtocolConfigViewModel(IProtocolClient client, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client.StateChanged += OnClientStateChanged;
        _client.TagValueChanged += OnTagValueChanged;

        ConnectCommand = new AsyncRelayCommand(
            async _ => await ConnectAsync(),
            _ => ConnectionState == ConnectionState.Disconnected);

        DisconnectCommand = new AsyncRelayCommand(
            async _ => await DisconnectAsync(),
            _ => ConnectionState == ConnectionState.Connected);

        TestReadCommand = new AsyncRelayCommand(
            async _ => await TestReadAsync(),
            _ => ConnectionState == ConnectionState.Connected && SelectedTag != null);

        TestWriteCommand = new AsyncRelayCommand(
            async _ => await TestWriteAsync(),
            _ => ConnectionState == ConnectionState.Connected && SelectedTag != null && !SelectedTag.IsReadOnly);

        AddTagCommand = new RelayCommand(_ => AddTag());
        RemoveTagCommand = new RelayCommand(_ => RemoveTag(), _ => SelectedTag != null);
    }

    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "Connecting...";
            await _client.ConnectAsync();

            if (Tags.Any())
            {
                await _client.SubscribeAsync(Tags);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            StatusMessage = "Disconnecting...";
            await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect");
            StatusMessage = $"Disconnect failed: {ex.Message}";
        }
    }

    private async Task TestReadAsync()
    {
        if (SelectedTag == null)
            return;

        try
        {
            var result = await _client.ReadTagAsync(SelectedTag);

            var existing = TagValues.FirstOrDefault(tv => tv.TagId == result.TagId);
            if (existing != null)
            {
                TagValues.Remove(existing);
            }

            TagValues.Insert(0, result);

            StatusMessage = $"Read: {result.TagName} = {result.Value} ({result.Quality})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read tag");
            StatusMessage = $"Read failed: {ex.Message}";
        }
    }

    protected virtual async Task TestWriteAsync()
    {
        // Override in derived classes to show value input dialog
        await Task.CompletedTask;
    }

    protected virtual void AddTag()
    {
        var tag = new TagDefinition
        {
            Name = $"Tag_{Tags.Count + 1}",
            Address = "0",
            DataType = TagDataType.Int16,
            ScanRateMs = 1000
        };

        Tags.Add(tag);
    }

    private void RemoveTag()
    {
        if (SelectedTag != null)
        {
            Tags.Remove(SelectedTag);
        }
    }

    private void OnClientStateChanged(object? sender, ConnectionState state)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectionState = state;
        });
    }

    private void OnTagValueChanged(object? sender, TagValue tagValue)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = TagValues.FirstOrDefault(tv => tv.TagId == tagValue.TagId);
            if (existing != null)
            {
                TagValues.Remove(existing);
            }

            TagValues.Insert(0, tagValue);
        });
    }

    private void UpdateStatusMessage()
    {
        StatusMessage = ConnectionState switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Disconnecting => "Disconnecting...",
            ConnectionState.Error => "Error",
            ConnectionState.Disconnected => "Disconnected",
            _ => "Unknown"
        };
    }

    public virtual void Dispose()
    {
        _client.StateChanged -= OnClientStateChanged;
        _client.TagValueChanged -= OnTagValueChanged;
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

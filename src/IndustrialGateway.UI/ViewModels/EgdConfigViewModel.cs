using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace IndustrialGateway.UI.ViewModels;

public class EgdConfigViewModel : ViewModelBase, IDisposable
{
    private readonly EgdConfig _config;
    private readonly IProtocolClient _client;
    private readonly ILogger<EgdConfigViewModel> _logger;

    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string _statusMessage = "Disconnected";
    private bool _isConnecting;

    public EgdConfig Config => _config;

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

    public bool IsConnecting
    {
        get => _isConnecting;
        set => SetProperty(ref _isConnecting, value);
    }

    private TagViewModel? _selectedTag;

    public ObservableCollection<TagViewModel> Tags { get; }
    public ObservableCollection<TagValue> TagValues { get; }

    public TagViewModel? SelectedTag
    {
        get => _selectedTag;
        set => SetProperty(ref _selectedTag, value);
    }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand SubscribeTagsCommand { get; }
    public ICommand UnsubscribeTagsCommand { get; }
    public ICommand AddExchangeCommand { get; }
    public ICommand RemoveExchangeCommand { get; }
    public ICommand ImportConfigCommand { get; }

    public EgdConfigViewModel(
        EgdConfig config,
        IProtocolClient client,
        ILogger<EgdConfigViewModel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Tags = new ObservableCollection<TagViewModel>();
        TagValues = new ObservableCollection<TagValue>();

        ConnectCommand = new AsyncRelayCommand(async _ => await ConnectAsync(), _ => !IsConnecting && ConnectionState != ConnectionState.Connected);
        DisconnectCommand = new AsyncRelayCommand(async _ => await DisconnectAsync(), _ => ConnectionState == ConnectionState.Connected);
        TestConnectionCommand = new AsyncRelayCommand(async _ => await TestConnectionAsync(), _ => !IsConnecting);
        AddTagCommand = new RelayCommand(_ => AddTag());
        RemoveTagCommand = new RelayCommand(_ => RemoveTag(), _ => SelectedTag != null);
        SubscribeTagsCommand = new AsyncRelayCommand(async _ => await SubscribeTagsAsync(), _ => ConnectionState == ConnectionState.Connected && Tags.Count > 0);
        UnsubscribeTagsCommand = new AsyncRelayCommand(async _ => await UnsubscribeTagsAsync(), _ => ConnectionState == ConnectionState.Connected);
        AddExchangeCommand = new RelayCommand(_ => AddExchange());
        RemoveExchangeCommand = new RelayCommand(_ => RemoveExchange());
        ImportConfigCommand = new RelayCommand(_ => ImportConfig());

        _client.StateChanged += OnClientStateChanged;
        _client.TagValueChanged += OnTagValueChanged;
    }

    private async Task ConnectAsync()
    {
        try
        {
            IsConnecting = true;
            StatusMessage = "Connecting...";
            _logger.LogInformation("Connecting to EGD device at {Host}:{Port}", _config.Host, _config.Port);

            await _client.ConnectAsync();

            StatusMessage = "Connected";
            _logger.LogInformation("Successfully connected to EGD device");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _logger.LogError(ex, "Failed to connect to EGD device");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            StatusMessage = "Disconnecting...";
            _logger.LogInformation("Disconnecting from EGD device");

            await _client.DisconnectAsync();

            StatusMessage = "Disconnected";
            _logger.LogInformation("Disconnected from EGD device");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnect failed: {ex.Message}";
            _logger.LogError(ex, "Failed to disconnect from EGD device");
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            IsConnecting = true;
            StatusMessage = "Testing connection...";
            _logger.LogInformation("Testing EGD connection to {Host}:{Port}", _config.Host, _config.Port);

            await _client.ConnectAsync();
            await Task.Delay(1000);
            await _client.DisconnectAsync();

            StatusMessage = "Connection test successful";
            _logger.LogInformation("EGD connection test successful");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection test failed: {ex.Message}";
            _logger.LogError(ex, "EGD connection test failed");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void AddTag()
    {
        var newTag = new TagDefinition
        {
            Name = $"Tag_{Tags.Count + 1}",
            Address = $"{Tags.Count}",
            DataType = TagDataType.Int32,
            IsReadOnly = false,
            ScanRateMs = _config.ProductionIntervalMs,
            Description = "New EGD tag"
        };

        var tagViewModel = new TagViewModel(newTag);
        Tags.Add(tagViewModel);
        SelectedTag = tagViewModel;
        
        // Initialize with test value for Producer mode (fire and forget)
        if (_config.Mode == EgdMode.Producer || _config.Mode == EgdMode.Both)
        {
            _ = _client.WriteTagAsync(newTag, tagViewModel.GetTestValueAsObject());
        }
        
        StatusMessage = $"Added new tag: {newTag.Name}";
        _logger.LogInformation("Added new tag: {TagName}", newTag.Name);
    }

    private void RemoveTag()
    {
        if (SelectedTag == null) return;

        var tagName = SelectedTag.Name;
        Tags.Remove(SelectedTag);
        SelectedTag = null;
        StatusMessage = $"Removed tag: {tagName}";
        _logger.LogInformation("Removed tag: {TagName}", tagName);
    }

    private async Task SubscribeTagsAsync()
    {
        try
        {
            if (Tags.Count == 0)
            {
                StatusMessage = "No tags to subscribe";
                return;
            }

            // Update test values for Producer mode
            if (_config.Mode == EgdMode.Producer || _config.Mode == EgdMode.Both)
            {
                foreach (var tagVm in Tags)
                {
                    await _client.WriteTagAsync(tagVm.Tag, tagVm.GetTestValueAsObject());
                }
            }

            StatusMessage = "Starting monitoring...";
            var tagDefinitions = Tags.Select(t => t.Tag).ToList();
            await _client.SubscribeAsync(tagDefinitions);
            StatusMessage = $"Monitoring {Tags.Count} tags";
            _logger.LogInformation("Started monitoring {Count} tags", Tags.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Subscribe failed: {ex.Message}";
            _logger.LogError(ex, "Failed to subscribe to tags");
        }
    }

    private async Task UnsubscribeTagsAsync()
    {
        try
        {
            StatusMessage = "Unsubscribing from tags...";
            await _client.UnsubscribeAllAsync();
            TagValues.Clear();
            StatusMessage = "Unsubscribed from all tags";
            _logger.LogInformation("Unsubscribed from all tags");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unsubscribe failed: {ex.Message}";
            _logger.LogError(ex, "Failed to unsubscribe from tags");
        }
    }

    private void AddExchange()
    {
        StatusMessage = "Add exchange functionality not yet implemented";
        _logger.LogInformation("Add exchange requested");
    }

    private void RemoveExchange()
    {
        StatusMessage = "Remove exchange functionality not yet implemented";
        _logger.LogInformation("Remove exchange requested");
    }

    private void ImportConfig()
    {
        StatusMessage = "Import config functionality not yet implemented";
        _logger.LogInformation("Import config requested");
    }

    private void OnClientStateChanged(object? sender, ConnectionState state)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectionState = state;
        });
    }

    private void OnTagValueChanged(object? sender, TagValue tagValue)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = TagValues.FirstOrDefault(tv => tv.TagId == tagValue.TagId);
            if (existing != null)
            {
                TagValues.Remove(existing);
            }
            TagValues.Add(tagValue);
        });
    }

    private void UpdateStatusMessage()
    {
        StatusMessage = ConnectionState switch
        {
            ConnectionState.Disconnected => "Disconnected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Connected => "Connected",
            ConnectionState.Disconnecting => "Disconnecting...",
            ConnectionState.Error => "Error",
            _ => "Unknown"
        };
    }

    public void Dispose()
    {
        _client.StateChanged -= OnClientStateChanged;
        _client.TagValueChanged -= OnTagValueChanged;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}

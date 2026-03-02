using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace IndustrialGateway.UI.ViewModels;

public class S7ConfigViewModel : ViewModelBase
{
    private readonly S7Config _config;
    private readonly IProtocolClient _client;
    private readonly ILogger<S7ConfigViewModel> _logger;

    private string _host;
    private int _port;
    private S7CpuType _cpuType;
    private short _rack;
    private short _slot;
    private bool _isConnected;
    private string _statusMessage = "Disconnected";

    public string Host
    {
        get => _host;
        set
        {
            if (SetProperty(ref _host, value))
            {
                _config.Host = value;
            }
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                _config.Port = value;
            }
        }
    }

    public S7CpuType CpuType
    {
        get => _cpuType;
        set
        {
            if (SetProperty(ref _cpuType, value))
            {
                _config.CpuType = value;
            }
        }
    }

    public short Rack
    {
        get => _rack;
        set
        {
            if (SetProperty(ref _rack, value))
            {
                _config.Rack = value;
            }
        }
    }

    public short Slot
    {
        get => _slot;
        set
        {
            if (SetProperty(ref _slot, value))
            {
                _config.Slot = value;
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<S7CpuType> AvailableCpuTypes { get; }
    public ObservableCollection<TagViewModel> Tags { get; }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand ReadTagCommand { get; }

    public S7ConfigViewModel(S7Config config, IProtocolClient client, ILogger<S7ConfigViewModel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _host = config.Host;
        _port = config.Port;
        _cpuType = config.CpuType;
        _rack = config.Rack;
        _slot = config.Slot;

        AvailableCpuTypes = new ObservableCollection<S7CpuType>(Enum.GetValues<S7CpuType>());
        Tags = new ObservableCollection<TagViewModel>();

        // Load existing tags
        foreach (var tag in config.Tags)
        {
            Tags.Add(new TagViewModel(tag));
        }

        ConnectCommand = new AsyncRelayCommand(async _ => await ConnectAsync());
        DisconnectCommand = new AsyncRelayCommand(async _ => await DisconnectAsync());
        AddTagCommand = new RelayCommand(_ => AddTag());
        RemoveTagCommand = new RelayCommand(param => RemoveTag(param));
        ReadTagCommand = new AsyncRelayCommand(async param => await ReadTagAsync(param));

        _client.StateChanged += OnConnectionStateChanged;
        _client.TagValueChanged += OnTagValueChanged;
    }

    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "Connecting...";
            await _client.ConnectAsync();
            IsConnected = true;
            StatusMessage = $"Connected to {Host}";
            _logger.LogInformation("Connected to S7 PLC at {Host}", Host);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            _logger.LogError(ex, "Failed to connect to S7 PLC");
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _client.DisconnectAsync();
            IsConnected = false;
            StatusMessage = "Disconnected";
            _logger.LogInformation("Disconnected from S7 PLC");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnect failed: {ex.Message}";
            _logger.LogError(ex, "Failed to disconnect from S7 PLC");
        }
    }

    private void AddTag()
    {
        var newTag = new TagDefinition
        {
            Name = $"Tag{Tags.Count + 1}",
            Address = "DB1.DBW0",
            DataType = TagDataType.UInt16
        };

        _config.Tags.Add(newTag);
        Tags.Add(new TagViewModel(newTag));
    }

    private void RemoveTag(object? parameter)
    {
        if (parameter is TagViewModel tagVm)
        {
            var tag = _config.Tags.FirstOrDefault(t => t.Name == tagVm.Name);
            if (tag != null)
            {
                _config.Tags.Remove(tag);
                Tags.Remove(tagVm);
            }
        }
    }

    private async Task ReadTagAsync(object? parameter)
    {
        if (parameter is TagViewModel tagVm)
        {
            try
            {
                var tag = _config.Tags.FirstOrDefault(t => t.Name == tagVm.Name);
                if (tag != null)
                {
                    var result = await _client.ReadTagAsync(tag);
                    tagVm.TestValue = result.Value?.ToString() ?? "null";
                    tagVm.Description = $"Quality: {result.Quality}, Time: {result.Timestamp:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read tag {TagName}", tagVm.Name);
                tagVm.Description = "Quality: Bad";
            }
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        StatusMessage = state.ToString();
    }

    private void OnTagValueChanged(object? sender, TagValue tagValue)
    {
        var tagVm = Tags.FirstOrDefault(t => t.Name == tagValue.TagName);
        if (tagVm != null)
        {
            tagVm.TestValue = tagValue.Value?.ToString() ?? "null";
            tagVm.Description = $"Quality: {tagValue.Quality}, Time: {tagValue.Timestamp:HH:mm:ss}";
        }
    }
}

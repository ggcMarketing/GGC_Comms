using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// ViewModel for Modbus TCP configuration
/// </summary>
public class ModbusTcpConfigViewModel : ProtocolConfigViewModel
{
    private readonly ModbusTcpConfig _config;

    public string Name
    {
        get => _config.Name;
        set
        {
            _config.Name = value;
            OnPropertyChanged();
        }
    }

    public string Host
    {
        get => _config.Host;
        set
        {
            _config.Host = value;
            OnPropertyChanged();
        }
    }

    public int Port
    {
        get => _config.Port;
        set
        {
            _config.Port = value;
            OnPropertyChanged();
        }
    }

    public byte SlaveId
    {
        get => _config.SlaveId;
        set
        {
            _config.SlaveId = value;
            OnPropertyChanged();
        }
    }

    public int TimeoutMs
    {
        get => _config.TimeoutMs;
        set
        {
            _config.TimeoutMs = value;
            OnPropertyChanged();
        }
    }

    public int DefaultScanRateMs
    {
        get => _config.DefaultScanRateMs;
        set
        {
            _config.DefaultScanRateMs = value;
            OnPropertyChanged();
        }
    }

    public ModbusTcpConfigViewModel(ModbusTcpConfig config, IProtocolClient client, ILogger<ModbusTcpConfigViewModel> logger)
        : base(client, logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Load existing tags from config
        foreach (var tag in _config.Tags)
        {
            Tags.Add(tag);
        }
    }

    protected override void AddTag()
    {
        var tag = new TagDefinition
        {
            Name = $"ModbusTag_{Tags.Count + 1}",
            Address = "40001", // Holding register
            DataType = TagDataType.Int16,
            ScanRateMs = _config.DefaultScanRateMs
        };

        Tags.Add(tag);
        _config.Tags.Add(tag);
    }

    protected override async Task TestWriteAsync()
    {
        if (SelectedTag == null)
            return;

        try
        {
            // For MVP, write a test value based on data type
            object testValue = SelectedTag.DataType switch
            {
                TagDataType.Bool => true,
                TagDataType.Int16 => (short)123,
                TagDataType.UInt16 => (ushort)456,
                TagDataType.Int32 => 789,
                TagDataType.UInt32 => (uint)1011,
                TagDataType.Float => 12.34f,
                TagDataType.Double => 56.78,
                _ => 0
            };

            await _client.WriteTagAsync(SelectedTag, testValue);
            StatusMessage = $"Write: {SelectedTag.Name} = {testValue}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write tag");
            StatusMessage = $"Write failed: {ex.Message}";
        }
    }
}

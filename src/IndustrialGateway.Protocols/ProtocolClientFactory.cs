using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.Protocols.Egd;
using IndustrialGateway.Protocols.EtherNetIp;
using IndustrialGateway.Protocols.ModbusTcp;
using IndustrialGateway.Protocols.Mqtt;
using IndustrialGateway.Protocols.OpcUa;
using IndustrialGateway.Protocols.Profinet;
using Microsoft.Extensions.Logging;

namespace IndustrialGateway.Protocols;

/// <summary>
/// Factory for creating protocol client instances
/// </summary>
public class ProtocolClientFactory : IProtocolClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IProtocolClient CreateClient(ConnectionConfig config)
    {
        return config switch
        {
            ModbusTcpConfig modbusTcpConfig => new ModbusTcpClient(
                modbusTcpConfig,
                _loggerFactory.CreateLogger<ModbusTcpClient>()),

            OpcUaConfig opcUaConfig => new OpcUaClient(
                opcUaConfig,
                _loggerFactory.CreateLogger<OpcUaClient>()),

            MqttConfig mqttConfig => new MqttClient(
                mqttConfig,
                _loggerFactory.CreateLogger<MqttClient>()),

            EtherNetIpConfig etherNetIpConfig => new EtherNetIpClient(
                etherNetIpConfig,
                _loggerFactory.CreateLogger<EtherNetIpClient>()),

            EgdConfig egdConfig => new EgdClient(
                egdConfig,
                _loggerFactory.CreateLogger<EgdClient>()),

            ProfinetConfig profinetConfig => new ProfinetClient(
                profinetConfig,
                _loggerFactory.CreateLogger<ProfinetClient>()),

            _ => throw new NotSupportedException($"Protocol type {config.ProtocolType} is not supported")
        };
    }

    public bool SupportsProtocol(ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.ModbusTcp => true,
            ProtocolType.OpcUa => true,
            ProtocolType.Mqtt => true,
            ProtocolType.EtherNetIp => true,
            ProtocolType.Egd => true,
            ProtocolType.Profinet => true,
            _ => false
        };
    }
}

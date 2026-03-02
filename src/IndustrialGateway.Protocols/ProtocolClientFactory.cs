using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.Protocols.Egd;
using IndustrialGateway.Protocols.EtherNetIp;
using IndustrialGateway.Protocols.ModbusTcp;
using IndustrialGateway.Protocols.S7;
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

            EtherNetIpConfig etherNetIpConfig => new EtherNetIpClient(
                etherNetIpConfig,
                _loggerFactory.CreateLogger<EtherNetIpClient>()),

            EgdConfig egdConfig => new EgdClient(
                egdConfig,
                _loggerFactory.CreateLogger<EgdClient>()),

            S7Config s7Config => new S7Client(
                s7Config,
                _loggerFactory.CreateLogger<S7Client>()),

            _ => throw new NotSupportedException($"Protocol type {config.ProtocolType} is not supported")
        };
    }

    public bool SupportsProtocol(ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.ModbusTcp => true,
            ProtocolType.EtherNetIp => true,
            ProtocolType.Egd => true,
            ProtocolType.S7 => true,
            _ => false
        };
    }
}

# Industrial Gateway - Industrial Automation Communication Interface

A modern .NET 8 WPF desktop application that provides a unified interface for connecting to industrial automation protocols. This MVP demonstrates clean architecture, MVVM pattern, and extensible protocol support.

## 🎯 Overview

The Industrial Gateway acts as an intermediate communication interface for industrial automation systems, supporting four core industrial protocols:

- ✅ **Modbus TCP** - Complete implementation using NModbus library
- ✅ **Siemens S7** - Complete implementation using S7.NET+ library (S7-200/300/400/1200/1500)
- ⚠️ **EtherNet/IP** - MVP stub (ready for libplctag integration)
- ⚠️ **GE EGD** - MVP stub (Ethernet Global Data protocol)

## 🏗️ Architecture Overview

### Solution Structure

```
IndustrialGateway.sln
├── src/
│   ├── IndustrialGateway.Core/           # Core abstractions and models
│   │   ├── Interfaces/
│   │   │   ├── IProtocolClient.cs        # Protocol client interface
│   │   │   ├── IProtocolClientFactory.cs # Factory interface
│   │   │   └── IConfigurationService.cs  # Configuration service interface
│   │   ├── Models/
│   │   │   ├── ConnectionConfig.cs       # Base configuration class
│   │   │   ├── ModbusTcpConfig.cs        # Modbus TCP configuration
│   │   │   ├── EtherNetIpConfig.cs       # EtherNet/IP configuration
│   │   │   ├── EgdConfig.cs              # EGD configuration
│   │   │   ├── TagDefinition.cs          # Tag/data point model
│   │   │   ├── TagValue.cs               # Tag value with metadata
│   │   │   └── ConnectionState.cs        # Connection state enum
│   │   └── Services/
│   │       └── JsonConfigurationService.cs # JSON persistence
│   │
│   ├── IndustrialGateway.Protocols/      # Protocol implementations
│   │   ├── ModbusTcp/
│   │   │   └── ModbusTcpClient.cs        # Full Modbus TCP implementation
│   │   ├── EtherNetIp/
│   │   │   └── EtherNetIpClient.cs       # Stub implementation
│   │   ├── Egd/
│   │   │   └── EgdClient.cs              # Stub implementation
│   │   └── ProtocolClientFactory.cs      # Factory implementation
│   │
│   └── IndustrialGateway.UI/             # WPF UI with MVVM
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs          # Base ViewModel
│       │   ├── MainViewModel.cs          # Main window ViewModel
│       │   ├── ProtocolConfigViewModel.cs # Base protocol ViewModel
│       │   └── ModbusTcpConfigViewModel.cs # Modbus-specific ViewModel
│       ├── Views/
│       │   └── ModbusTcpConfigView.xaml  # Modbus configuration UI
│       ├── Commands/
│       │   └── RelayCommand.cs           # ICommand implementations
│       ├── Converters/
│       │   ├── ConnectionStateToColorConverter.cs
│       │   └── BooleanToVisibilityConverter.cs
│       ├── MainWindow.xaml               # Main application window
│       └── App.xaml.cs                   # Application bootstrap with DI
│
└── tests/
    └── IndustrialGateway.Tests/          # Unit tests
        ├── Core/
        │   └── JsonConfigurationServiceTests.cs
        ├── Protocols/
        │   └── EtherNetIpClientTests.cs
        └── UI/
            └── MainViewModelTests.cs
```

### Key Architecture Principles

1. **Separation of Concerns**
   - Core: Protocol abstractions, models, interfaces
   - Protocols: Protocol-specific implementations
   - UI: WPF views and ViewModels following MVVM

2. **Dependency Injection**
   - All dependencies injected via Microsoft.Extensions.DependencyInjection
   - Services configured in `App.xaml.cs`

3. **Protocol Plugin Architecture**
   - All protocols implement `IProtocolClient`
   - Factory pattern for creating protocol instances
   - Easy to add new protocols by implementing interface

4. **Configuration Management**
   - JSON-based persistence to disk
   - Strongly-typed configuration classes
   - Polymorphic serialization using System.Text.Json

5. **Logging**
   - Microsoft.Extensions.Logging throughout
   - Configured for Console and Debug outputs

## 🔌 Protocol Client Interface

All protocol implementations follow the `IProtocolClient` interface:

```csharp
public interface IProtocolClient : IDisposable
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;
    event EventHandler<TagValue>? TagValueChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default);
    Task<IEnumerable<TagValue>> ReadTagsAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);

    Task WriteTagAsync(TagDefinition tag, object value, CancellationToken cancellationToken = default);
    Task WriteTagsAsync(IDictionary<TagDefinition, object> tagValues, CancellationToken cancellationToken = default);

    Task SubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default);
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}
```

## 📦 NuGet Dependencies

### Complete Implementation
- **NModbus** (3.0.72) - Modbus TCP implementation
- **S7netplus** (0.20.0) - Siemens S7 PLC communication

### Framework Dependencies
- **Microsoft.Extensions.DependencyInjection** (9.0.0) - Dependency injection
- **Microsoft.Extensions.Logging** (9.0.0) - Logging abstractions
- **System.Text.Json** (9.0.0) - JSON serialization

### Stub Implementations (Ready for Production Libraries)
- **EtherNet/IP**: Use `libplctag` or `libplctag.NETWrapper`
- **GE EGD**: Requires proprietary GE libraries or reverse-engineered implementation

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 (or VS Code with C# extension)
- Windows OS (for WPF)

### Quick Start

1. Clone the repository
2. Open `IndustrialGateway.sln` in Visual Studio
3. Build and run the application
4. Click "Add Siemens S7" or "Add Modbus TCP" to create a connection
5. Configure connection settings and add tags
6. Click "Connect" to start communicating with your PLC

### Supported Protocols

#### Siemens S7 PLCs
- S7-200, S7-300, S7-400, S7-1200, S7-1500
- Native Ethernet connectivity (port 102)
- Full read/write support for data blocks, memory, inputs, outputs
- See [S7 Implementation Guide](docs/S7-Implementation.md) for details

#### Modbus TCP
- Standard Modbus TCP/IP protocol
- Holding registers, input registers, coils, discrete inputs
- Configurable slave ID and timeout

### Building the Solution

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run the application
dotnet run --project src/IndustrialGateway.UI/IndustrialGateway.UI.csproj

# Run tests
dotnet test
```

### Running the Application

1. Launch the application
2. Select a protocol from the dropdown (Modbus TCP, EtherNet/IP, or EGD)
3. Configure connection settings:
   - Name: Friendly connection name
   - Host: IP address or hostname
   - Port: Protocol-specific port
   - Other protocol-specific settings
4. Add tags to monitor/control
5. Click "Connect" to establish connection
6. Use "Test Read" / "Test Write" to interact with tags
7. Subscribe to tags for live updates

## 🧪 End-to-End Example: Modbus TCP

### 1. Configuration

```csharp
var config = new ModbusTcpConfig
{
    Name = "PLC-001",
    Host = "192.168.1.100",
    Port = 502,
    SlaveId = 1,
    TimeoutMs = 5000,
    DefaultScanRateMs = 1000
};
```

### 2. Create Client

```csharp
var factory = new ProtocolClientFactory(loggerFactory);
var client = factory.CreateClient(config);
```

### 3. Connect and Read

```csharp
await client.ConnectAsync();

var tag = new TagDefinition
{
    Name = "Temperature",
    Address = "40001",  // Holding register
    DataType = TagDataType.Float
};

var value = await client.ReadTagAsync(tag);
Console.WriteLine($"{value.TagName}: {value.Value} ({value.Quality})");
```

### 4. Subscribe to Changes

```csharp
client.TagValueChanged += (sender, tagValue) =>
{
    Console.WriteLine($"Update: {tagValue.TagName} = {tagValue.Value}");
};

await client.SubscribeAsync(new[] { tag });
```

### 5. Write a Value

```csharp
await client.WriteTagAsync(tag, 25.5f);
```

## 🎨 UI Features

### Main Window
- Protocol selector dropdown (Modbus TCP, EtherNet/IP, EGD)
- Clean, modern interface
- Status indicators with color coding
- Real-time tag value display

### Modbus TCP Configuration View
- Connection settings panel
- Tag configuration grid
- Live tag values table
- Test read/write buttons
- Connection status indicator (Green=Connected, Gray=Disconnected, Red=Error)

## 🧩 Adding New Protocols

To add a new protocol:

1. **Create Configuration Class**
   ```csharp
   public class MyProtocolConfig : ConnectionConfig
   {
       public override ProtocolType ProtocolType => ProtocolType.MyProtocol;
       public string MyProperty { get; set; }
   }
   ```

2. **Implement Protocol Client**
   ```csharp
   public class MyProtocolClient : IProtocolClient
   {
       // Implement all interface members
   }
   ```

3. **Update Factory**
   ```csharp
   public IProtocolClient CreateClient(ConnectionConfig config)
   {
       return config switch
       {
           MyProtocolConfig myConfig => new MyProtocolClient(myConfig, logger),
           // ... other protocols
       };
   }
   ```

4. **Create ViewModel and View** (Optional for UI)

## 🔒 Best Practices Implemented

1. **Async/Await**: All I/O operations are asynchronous
2. **Cancellation Tokens**: Support for operation cancellation
3. **Exception Handling**: Proper try-catch with logging
4. **Resource Disposal**: IDisposable pattern throughout
5. **Thread Safety**: Lock objects for shared state
6. **MVVM Pattern**: Clean separation in UI
7. **Data Binding**: No code-behind logic
8. **Dependency Injection**: Loose coupling via DI container

## 📊 Configuration Persistence

Configurations are saved as JSON files in:
```
%AppData%\IndustrialGateway\Configurations\
```

Example Modbus TCP configuration:
```json
{
  "$type": "ModbusTcp",
  "Host": "192.168.1.100",
  "Port": 502,
  "SlaveId": 1,
  "TimeoutMs": 5000,
  "UseRtuOverTcp": false,
  "Id": "abc-123-def-456",
  "Name": "PLC-001",
  "Enabled": true,
  "Tags": [
    {
      "Id": "tag-001",
      "Name": "Temperature_Zone1",
      "Address": "40001",
      "DataType": "Float",
      "IsReadOnly": false,
      "ScanRateMs": 1000
    }
  ],
  "DefaultScanRateMs": 1000
}
```

## 🧪 Testing

The solution includes unit tests covering:
- Configuration service (save/load/delete)
- Protocol client stubs (connection, read, write, subscribe)
- ViewModel logic

Run tests:
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 📈 Future Enhancements

### For Production
1. **Replace Protocol Stubs**
   - Integrate production libraries for EtherNet/IP, EGD
   - Implement full protocol specifications

2. **Security**
   - Encrypt sensitive configuration data (passwords)
   - Implement user authentication

3. **Advanced Features**
   - Data logging and historian
   - Alarming and notifications
   - Trending and visualization
   - Multiple simultaneous connections
   - Connection redundancy and failover

4. **Performance**
   - Optimize bulk read/write operations
   - Implement connection pooling
   - Add caching strategies

5. **UI Enhancements**
   - Implement configuration views for all protocols
   - Add import/export functionality
   - Create dashboard view
   - Add diagnostics and troubleshooting tools

## 📄 License

This is an MVP demonstration project. Adapt as needed for your use case.

## 🤝 Contributing

To extend this MVP:
1. Follow the existing architecture patterns
2. Implement `IProtocolClient` for new protocols
3. Add comprehensive unit tests
4. Update documentation

---

**Version:** 1.0.0
**Framework:** .NET 8
**UI Framework:** WPF with MVVM
**Architecture:** Clean Architecture with DI
**Protocols:** Modbus TCP, Siemens S7, EtherNet/IP (stub), GE EGD (stub)

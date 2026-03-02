# Siemens S7 PLC Implementation

## Overview

The Industrial Gateway now supports communication with Siemens S7 PLCs using the [S7.NET+](https://github.com/S7NetPlus/s7netplus) library. This implementation provides native .NET connectivity to Siemens Step7 devices without requiring proprietary DLLs.

## Supported PLCs

- S7-200
- S7-300
- S7-400
- S7-1200
- S7-1500

## Features

### Connection Management
- Ethernet/IP connectivity (default port 102)
- Configurable CPU type selection
- Rack and slot addressing
- Connection state monitoring
- Automatic reconnection handling

### Data Access
- Read/Write operations for individual tags
- Batch read/write operations
- Support for S7 address formats:
  - Data Blocks: `DB1.DBW0`, `DB1.DBD10`, `DB1.DBB5`
  - Memory: `M0.0`, `MW10`, `MD20`
  - Inputs: `I0.0`, `IW10`
  - Outputs: `Q0.0`, `QW10`

### Data Types
- Bool (Bit)
- Byte
- Word (UInt16)
- DWord (UInt32)
- Int (Int16)
- DInt (Int32)
- Real (Float)
- String

## Configuration

### Connection Settings

```json
{
  "Name": "Siemens S7 1",
  "Host": "192.168.1.1",
  "Port": 102,
  "CpuType": "S71200",
  "Rack": 0,
  "Slot": 1,
  "Tags": [
    {
      "Name": "Temperature",
      "Address": "DB1.DBW0",
      "DataType": "UInt16"
    },
    {
      "Name": "Pressure",
      "Address": "DB1.DBD10",
      "DataType": "Float"
    }
  ]
}
```

### CPU Types
- `S7200` - S7-200 series
- `S7300` - S7-300 series
- `S7400` - S7-400 series
- `S71200` - S7-1200 series (default)
- `S71500` - S7-1500 series

### Rack and Slot
- **Rack**: Typically 0 for most configurations
- **Slot**: CPU slot number (usually 1 or 2)
  - Check your hardware configuration in TIA Portal or Step7

## Address Format Examples

### Data Blocks (DB)
- `DB1.DBW0` - Word at byte 0 in DB1
- `DB1.DBD10` - Double Word at byte 10 in DB1
- `DB1.DBB5` - Byte at byte 5 in DB1
- `DB1.DBX0.0` - Bit 0 of byte 0 in DB1

### Memory (M)
- `M0.0` - Memory bit 0.0
- `MW10` - Memory word at byte 10
- `MD20` - Memory double word at byte 20

### Inputs (I)
- `I0.0` - Input bit 0.0
- `IW10` - Input word at byte 10

### Outputs (Q)
- `Q0.0` - Output bit 0.0
- `QW10` - Output word at byte 10

## Usage in UI

1. Click "Add Siemens S7" button
2. Configure connection settings:
   - Enter PLC IP address
   - Select CPU type
   - Set rack and slot numbers
3. Click "Connect" to establish connection
4. Add tags with S7 addresses
5. Use "Read" button to read individual tag values

## Implementation Details

### S7Client Class
Located in `src/IndustrialGateway.Protocols/S7/S7Client.cs`

Key features:
- Implements `IProtocolClient` interface
- Thread-safe connection management
- Proper state tracking
- Error handling and logging

### S7Config Model
Located in `src/IndustrialGateway.Core/Models/S7Config.cs`

Properties:
- Host, Port
- CpuType (enum)
- Rack, Slot (short)
- Tags collection

### UI Components
- **ViewModel**: `S7ConfigViewModel.cs`
- **View**: `S7ConfigView.xaml`
- Integrated into main connection tree

## Limitations

1. **No Native Subscriptions**: S7.NET+ doesn't support native tag subscriptions. Polling must be used for continuous monitoring.

2. **Optimized Data Blocks**: Cannot read from optimized data blocks. Use standard (non-optimized) data blocks.

3. **Connection Overhead**: Each read/write operation requires a TCP request. Use batch operations when possible.

## Troubleshooting

### Connection Issues
- Verify PLC IP address is reachable (ping test)
- Ensure port 102 is not blocked by firewall
- Check rack and slot numbers in hardware configuration
- Verify PLC allows external connections (security settings)

### Read/Write Errors
- Verify address format is correct
- Ensure data block exists and is not optimized
- Check data type matches PLC configuration
- Verify sufficient access rights

### Performance
- Use batch read operations for multiple tags
- Avoid excessive polling rates
- Consider network latency in scan rate configuration

## References

- [S7.NET+ GitHub Repository](https://github.com/S7NetPlus/s7netplus)
- [S7.NET+ Documentation](https://github.com/S7NetPlus/s7netplus/wiki)
- [NuGet Package](https://www.nuget.org/packages/S7netplus/)

## Future Enhancements

- Polling service for continuous tag monitoring
- Bulk read optimization using S7.NET+ multi-var read
- Support for struct/class mapping
- Connection pooling for multiple S7 connections
- Advanced diagnostics and PLC information retrieval

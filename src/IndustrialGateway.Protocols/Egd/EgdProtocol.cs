namespace IndustrialGateway.Protocols.Egd;

/// <summary>
/// EGD Protocol packet builder and parser
/// Based on GE's Ethernet Global Data specification
/// </summary>
public static class EgdProtocol
{
    /// <summary>
    /// Build an EGD packet for transmission with proper 32-byte header
    /// </summary>
    /// <param name="exchangeId">Exchange ID (32-bit)</param>
    /// <param name="producerId">Producer ID (32-bit)</param>
    /// <param name="data">Data payload</param>
    /// <param name="requestId">Request ID (default 1)</param>
    /// <returns>Complete EGD packet ready for UDP transmission</returns>
    public static byte[] BuildEgdPacket(uint exchangeId, uint producerId, byte[] data, ushort requestId = 0x0001)
    {
        // EGD Packet Structure (32-byte header + data):
        // Bytes 0-1:   PDUTypeVersion (0x010D = type 13, version 1) - little-endian
        // Bytes 2-3:   RequestID - little-endian
        // Bytes 4-7:   ProducerID (32-bit) - little-endian
        // Bytes 8-11:  ExchangeID (32-bit) - little-endian
        // Bytes 12-15: TimeStampSec (32-bit) - little-endian
        // Bytes 16-19: TimeStampNanoSec (32-bit) - little-endian
        // Bytes 20-23: Status (32-bit) - little-endian
        // Bytes 24-27: ConfigSignature (32-bit) - little-endian
        // Bytes 28-31: Reserved (32-bit) - little-endian
        // Bytes 32+:   Data payload
        
        const int headerSize = 32;
        int totalSize = headerSize + data.Length;
        byte[] packet = new byte[totalSize];
        
        int offset = 0;
        
        // PDUTypeVersion (0x010D) - little-endian
        ushort pduTypeVersion = 0x010D;
        Buffer.BlockCopy(BitConverter.GetBytes(pduTypeVersion), 0, packet, offset, 2);
        offset += 2;
        
        // RequestID - little-endian
        Buffer.BlockCopy(BitConverter.GetBytes(requestId), 0, packet, offset, 2);
        offset += 2;
        
        // ProducerID - little-endian
        Buffer.BlockCopy(BitConverter.GetBytes(producerId), 0, packet, offset, 4);
        offset += 4;
        
        // ExchangeID - little-endian
        Buffer.BlockCopy(BitConverter.GetBytes(exchangeId), 0, packet, offset, 4);
        offset += 4;
        
        // TimeStampSec - little-endian (current Unix timestamp)
        uint timeStampSec = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Buffer.BlockCopy(BitConverter.GetBytes(timeStampSec), 0, packet, offset, 4);
        offset += 4;
        
        // TimeStampNanoSec - little-endian
        uint timeStampNanoSec = 0x00000000;
        Buffer.BlockCopy(BitConverter.GetBytes(timeStampNanoSec), 0, packet, offset, 4);
        offset += 4;
        
        // Status - little-endian (0x00000001 = good)
        uint status = 0x00000001;
        Buffer.BlockCopy(BitConverter.GetBytes(status), 0, packet, offset, 4);
        offset += 4;
        
        // ConfigSignature - little-endian
        uint configSignature = 0x00000000;
        Buffer.BlockCopy(BitConverter.GetBytes(configSignature), 0, packet, offset, 4);
        offset += 4;
        
        // Reserved - little-endian
        uint reserved = 0x00000000;
        Buffer.BlockCopy(BitConverter.GetBytes(reserved), 0, packet, offset, 4);
        offset += 4;
        
        // Copy data payload
        Array.Copy(data, 0, packet, headerSize, data.Length);
        
        return packet;
    }
    
    /// <summary>
    /// Parse an EGD packet received from UDP
    /// </summary>
    public static (bool isValid, uint exchangeId, uint producerId, byte[] data) ParseEgdPacket(byte[] packet)
    {
        if (packet.Length < 32)
            return (false, 0, 0, Array.Empty<byte>());
        
        int offset = 0;
        
        // Parse PDUTypeVersion
        ushort pduTypeVersion = BitConverter.ToUInt16(packet, offset);
        offset += 2;
        
        // Verify PDU type (should be 0x010D)
        if (pduTypeVersion != 0x010D)
            return (false, 0, 0, Array.Empty<byte>());
        
        // Skip RequestID
        offset += 2;
        
        // Parse ProducerID
        uint producerId = BitConverter.ToUInt32(packet, offset);
        offset += 4;
        
        // Parse ExchangeID
        uint exchangeId = BitConverter.ToUInt32(packet, offset);
        offset += 4;
        
        // Skip timestamp, status, config signature, reserved (20 bytes)
        offset += 20;
        
        // Extract data payload (everything after 32-byte header)
        int dataLength = packet.Length - 32;
        byte[] data = new byte[dataLength];
        if (dataLength > 0)
        {
            Array.Copy(packet, 32, data, 0, dataLength);
        }
        
        return (true, exchangeId, producerId, data);
    }
    
    /// <summary>
    /// Convert Producer ID string to 32-bit integer
    /// </summary>
    public static uint ProducerIdToUInt32(string producerId)
    {
        if (string.IsNullOrEmpty(producerId))
            return 0xC0A80064; // Default: 192.168.0.100 as uint
        
        // Try to parse as number first
        if (uint.TryParse(producerId, out uint result))
            return result;
        
        // Try to parse as IP address
        if (System.Net.IPAddress.TryParse(producerId, out var ipAddress))
        {
            byte[] bytes = ipAddress.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return BitConverter.ToUInt32(bytes, 0);
            }
        }
        
        // Otherwise, create a hash from the string
        return (uint)Math.Abs(producerId.GetHashCode());
    }
}

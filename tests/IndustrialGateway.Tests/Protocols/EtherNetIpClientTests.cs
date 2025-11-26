using FluentAssertions;
using IndustrialGateway.Core.Models;
using IndustrialGateway.Protocols.EtherNetIp;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IndustrialGateway.Tests.Protocols;

public class EtherNetIpClientTests : IDisposable
{
    private readonly EtherNetIpClient _client;
    private readonly Mock<ILogger<EtherNetIpClient>> _loggerMock;
    private readonly EtherNetIpConfig _config;

    public EtherNetIpClientTests()
    {
        _config = new EtherNetIpConfig
        {
            Name = "Test EIP",
            Host = "127.0.0.1",
            Port = 44818
        };

        _loggerMock = new Mock<ILogger<EtherNetIpClient>>();
        _client = new EtherNetIpClient(_config, _loggerMock.Object);
    }

    [Fact]
    public async Task ConnectAsync_ShouldChangeStateToConnected()
    {
        // Arrange
        ConnectionState? capturedState = null;
        _client.StateChanged += (sender, state) => capturedState = state;

        // Act
        await _client.ConnectAsync();

        // Assert
        _client.State.Should().Be(ConnectionState.Connected);
        capturedState.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldChangeStateToDisconnected()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.DisconnectAsync();

        // Assert
        _client.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task ReadTagAsync_WhenConnected_ShouldReturnValue()
    {
        // Arrange
        await _client.ConnectAsync();
        var tag = new TagDefinition
        {
            Name = "TestTag",
            Address = "TestAddress",
            DataType = TagDataType.Int16
        };

        // Act
        var result = await _client.ReadTagAsync(tag);

        // Assert
        result.Should().NotBeNull();
        result.TagName.Should().Be("TestTag");
        result.Quality.Should().Be(TagQuality.Good);
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task WriteTagAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        await _client.ConnectAsync();
        var tag = new TagDefinition
        {
            Name = "TestTag",
            Address = "TestAddress",
            DataType = TagDataType.Int16
        };

        // Act
        Func<Task> act = async () => await _client.WriteTagAsync(tag, (short)123);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldRaiseTagValueChangedEvent()
    {
        // Arrange
        await _client.ConnectAsync();
        var tag = new TagDefinition
        {
            Name = "TestTag",
            Address = "TestAddress",
            DataType = TagDataType.Int16,
            ScanRateMs = 100
        };

        TagValue? receivedValue = null;
        _client.TagValueChanged += (sender, value) => receivedValue = value;

        // Act
        await _client.SubscribeAsync(new[] { tag });
        await Task.Delay(200); // Wait for at least one scan

        // Assert
        receivedValue.Should().NotBeNull();
        receivedValue!.TagName.Should().Be("TestTag");
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

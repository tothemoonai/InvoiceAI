using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;
using Moq;

namespace InvoiceAI.Core.Tests;

public class ProviderFallbackManagerTests
{
    private readonly Mock<IAppSettingsService> _mockSettings;
    private readonly AppSettings _testSettings;
    private readonly ProviderFallbackManager _manager;

    public ProviderFallbackManagerTests()
    {
        _mockSettings = new Mock<IAppSettingsService>();
        _testSettings = new AppSettings();
        _mockSettings.Setup(s => s.Settings).Returns(_testSettings);
        _mockSettings.Setup(s => s.SaveAsync())
            .Returns(Task.CompletedTask);
        _manager = new ProviderFallbackManager(_mockSettings.Object);
    }

    [Fact]
    public async Task MarkProviderVerifiedAsync_AddsNewProvider()
    {
        // Act
        await _manager.MarkProviderVerifiedAsync("zhipu");

        // Assert
        Assert.Contains("zhipu", _testSettings.Glm.VerifiedProviders);
        _mockSettings.Verify(s => s.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task MarkProviderVerifiedAsync_DoesNotDuplicate()
    {
        // Arrange
        await _manager.MarkProviderVerifiedAsync("zhipu");
        _mockSettings.Invocations.Clear(); // Clear previous calls

        // Act
        await _manager.MarkProviderVerifiedAsync("zhipu");

        // Assert
        Assert.Single(_testSettings.Glm.VerifiedProviders);
        _mockSettings.Verify(s => s.SaveAsync(), Times.Once);
    }

    [Fact]
    public void TryGetNextProvider_ReturnsNextInSequence()
    {
        // Arrange
        _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia", "cerebras" });

        // Act
        var next = _manager.TryGetNextProvider("zhipu", "test failure");

        // Assert
        Assert.Equal("nvidia", next);
    }

    [Fact]
    public void TryGetNextProvider_ReturnsNullWhenNoMoreProviders()
    {
        // Arrange
        _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu" });

        // Act
        var next = _manager.TryGetNextProvider("zhipu", "test failure");

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetVerifiedProviders_ReturnsReadOnlyList()
    {
        // Arrange
        _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia" });

        // Act
        var verified = _manager.GetVerifiedProviders();

        // Assert
        Assert.Equal(2, verified.Count);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(verified);
    }
}

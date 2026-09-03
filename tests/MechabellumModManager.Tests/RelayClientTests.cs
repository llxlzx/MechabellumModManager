using FluentAssertions;
using MechabellumModManager.Services;

public class RelayClientTests
{
    [Theory]
    [InlineData("https://example.workers.dev", "/v1/reports", "https://example.workers.dev/v1/reports")]
    [InlineData("https://example.workers.dev/", "v1/reports", "https://example.workers.dev/v1/reports")]
    [InlineData("https://example.workers.dev", "v1/submissions", "https://example.workers.dev/v1/submissions")]
    [InlineData("", "/v1/reports", "/v1/reports")]
    public void JoinUrl_joins_base_and_path(string baseUrl, string path, string expected)
    {
        RelayClient.JoinUrl(baseUrl, path).Should().Be(expected);
    }

    [Fact]
    public void IsConfigured_false_for_placeholder()
    {
        new RelayClient(RelayClient.DefaultPlaceholderBaseUrl).IsConfigured.Should().BeFalse();
        new RelayClient("").IsConfigured.Should().BeFalse();
        new RelayClient("https://mechabellum-mod-relay.example.workers.dev").IsConfigured.Should().BeTrue();
    }
}

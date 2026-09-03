using FluentAssertions;
using MechabellumModManager.ViewModels;

public class PreviewImageLoaderTests
{
    [Fact]
    public async Task TryLoadAsync_null_url_returns_null()
    {
        var result = await PreviewImageLoader.TryLoadAsync(null);
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryLoadAsync_whitespace_url_returns_null()
    {
        var result = await PreviewImageLoader.TryLoadAsync("   ");
        result.Should().BeNull();
    }
}

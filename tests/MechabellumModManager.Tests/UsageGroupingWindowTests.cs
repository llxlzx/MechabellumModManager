using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using MechabellumModManager.Converters;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

[Collection(StaCollection.Name)]
public class UsageGroupingWindowTests
{
    readonly StaTestHost _host;

    public UsageGroupingWindowTests(StaTestHost host) => _host = host;

    [Theory]
    [InlineData("CatalogModsGrid")]
    [InlineData("LibraryModsGrid")]
    public void Mod_grid_has_usage_group_header_and_keeps_virtualizing(string gridName)
    {
        _host.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                var grid = (DataGrid)window.FindName(gridName);
                grid.GroupStyle.Should().ContainSingle();
                grid.GroupStyle[0].HeaderTemplate.Should().BeSameAs(
                    Application.Current.FindResource("UsageGroupHeaderTemplate"));
                VirtualizingPanel.GetIsVirtualizingWhenGrouping(grid).Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        }, TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData(ModUsage.Tournament)]
    [InlineData(ModUsage.Casual)]
    [InlineData(ModUsage.Unrated)]
    public void Converter_returns_localized_label(ModUsage usage)
    {
        var converter = new ModUsageToLabelConverter();
        converter.Convert(usage, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(LocalizationService.T(ModTaxonomy.UsageResourceKey(usage)));
    }

    [Fact]
    public void Converter_returns_empty_for_non_usage()
    {
        new ModUsageToLabelConverter().Convert("x", typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("");
    }
}

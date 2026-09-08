using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;
using MechabellumModManager.ViewModels;
using Xunit;

namespace MechabellumModManager.Tests;

/// <summary>
/// What a catalog of thousands of entries does to the browse UI. WPF's DataGrid virtualizes
/// rows by default, but that default is silently lost whenever the grid ends up with unbounded
/// height, so it is worth measuring on the real window rather than assuming.
/// </summary>
[Collection(StaCollection.Name)]
public class CatalogScaleTests
{
    const int ItemCount = 5000;

    readonly StaTestHost _host;

    public CatalogScaleTests(StaTestHost host) => _host = host;

    [Fact]
    public void The_catalog_grid_realises_a_screenful_of_rows_not_the_whole_catalog()
    {
        var realised = -1;
        var total = -1;

        using var fx = MainViewModelFixture.CreateReady();

        _host.Run(() =>
        {
            var vm = fx.CreateVm();
            vm.ActiveContentPage = MainContentPage.Catalog;
            for (var i = 0; i < ItemCount; i++)
            {
                vm.CatalogMods.Add(new CatalogModItemViewModel(
                    new CatalogMod
                    {
                        Id = $"mod-{i:D5}",
                        Name = $"Mod {i:D5}",
                        File = $"mods/mod-{i:D5}/Mod.dll",
                        Size = 1024,
                        Sha256 = new string('0', 64)
                    },
                    CatalogEntryState.NotInstalled));
            }

            var window = new MainWindow
            {
                DataContext = vm,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowState = WindowState.Normal,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Width = 1280,
                Height = 800,
                Opacity = 0
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                StaTestHost.Pump();

                var grid = (DataGrid)window.FindName("CatalogModsGrid");
                grid.UpdateLayout();
                StaTestHost.Pump();

                total = grid.Items.Count;
                realised = 0;
                for (var i = 0; i < total; i++)
                {
                    if (grid.ItemContainerGenerator.ContainerFromIndex(i) is not null)
                        realised++;
                }
            }
            finally
            {
                window.Close();
                MainWindowSmokeTests.CloseOwnedWindows();
            }
        }, TimeSpan.FromSeconds(120));

        total.Should().Be(ItemCount, "the filter must not be hiding the rows this test is counting");

        // Measured: 13 of 5000 on a 1280x800 window.
        realised.Should().BeGreaterThan(0, "a visible grid has to realise the rows actually on screen");
        realised.Should().BeLessThan(
            ItemCount / 4,
            $"row virtualization has to hold: {realised} of {ItemCount} containers were realised, which "
            + "means the grid was handed unbounded height somewhere in its parent chain");
    }
}

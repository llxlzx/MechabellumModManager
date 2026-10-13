using System.Globalization;
using System.Windows.Data;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Converters;

public sealed class ModUsageToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ModUsage usage ? LocalizationService.T(ModTaxonomy.UsageResourceKey(usage)) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

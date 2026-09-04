using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed class CategoryFilterOption
{
    public CategoryFilterOption(ModCategory? category, string label)
    {
        Category = category;
        Label = label;
    }

    public ModCategory? Category { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class TagFilterOption
{
    public TagFilterOption(string? tag, string label)
    {
        Tag = tag;
        Label = label;
    }

    public string? Tag { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class SortModeOption
{
    public SortModeOption(ModSortMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public ModSortMode Mode { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

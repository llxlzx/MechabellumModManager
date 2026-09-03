namespace MechabellumModManager.ViewModels;

public sealed class LanguageOption
{
    public LanguageOption(string code, string label)
    {
        Code = code;
        Label = label;
    }

    public string Code { get; }
    public string Label { get; set; }

    public override string ToString() => Label;
}

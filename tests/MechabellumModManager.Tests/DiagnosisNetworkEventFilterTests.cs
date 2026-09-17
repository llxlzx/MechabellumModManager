using FluentAssertions;
using MechabellumModManager.Services;
using System.Windows.Markup;

public class DiagnosisNetworkEventFilterTests
{
    [Fact]
    public void Detects_static_resource_message()
    {
        DiagnosisNetworkEventFilter.LooksLikeUiFailureMessage(
                "在“System.Windows.StaticResourceExtension”上提供值时引发了异常。，行号为“53”，行位置为“32”。")
            .Should().BeTrue();
    }

    [Fact]
    public void Detects_xaml_parse_exception_type()
    {
        var ex = new XamlParseException("missing key");
        DiagnosisNetworkEventFilter.IsLikelyUiPresentationFailure(ex).Should().BeTrue();
    }

    [Fact]
    public void Rejects_timeout_as_ui()
    {
        DiagnosisNetworkEventFilter.LooksLikeUiFailureMessage(
                "The request was canceled due to the configured HttpClient.Timeout")
            .Should().BeFalse();
    }
}

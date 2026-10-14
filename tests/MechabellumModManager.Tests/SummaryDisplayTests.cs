using FluentAssertions;
using MechabellumModManager.Services;

public class SummaryDisplayTests
{
    [Theory]
    [InlineData("从管理器安装或更新需要 1.3.13 或更高版本。 按 G 开关永久地面格线；支持正式服与测试服。", "按 G 开关永久地面格线；支持正式服与测试服。")]
    [InlineData("Installing or updating through the manager requires 1.3.13 or newer. Press G to toggle a permanent ground grid.", "Press G to toggle a permanent ground grid.")]
    [InlineData("Installation oder Aktualisierung über den Manager erfordert 1.3.13 oder neuer. Mit G das Bodengitter umschalten.", "Mit G das Bodengitter umschalten.")]
    [InlineData("マネージャーからのインストールと更新には 1.3.13 以降が必要です。 G キーで切り替え。", "G キーで切り替え。")]
    [InlineData("Установка и обновление через менеджер требуют версию 1.3.13 или новее. Клавиша G включает сетку.", "Клавиша G включает сетку.")]
    [InlineData("Установка и обновление через менеджер требуют версию 1.3.13 или новее。 Клавиша G включает сетку.", "Клавиша G включает сетку.")]
    [InlineData("从管理器安装或更新需要 1.3.15 或更高版本。 悬停单位文字时展开科技说明。", "悬停单位文字时展开科技说明。")]
    [InlineData("  从管理器安装或更新需要 1.3.13 或更高版本。按 G 开关格线。", "按 G 开关格线。")]
    public void Strips_the_leading_manager_version_sentence(string input, string expected) =>
        SummaryDisplay.StripManagerFloorPrefix(input).Should().Be(expected);

    [Fact]
    public void Keeps_game_versions_in_the_rest_of_the_text() =>
        SummaryDisplay.StripManagerFloorPrefix(
                "从管理器安装或更新需要 1.3.13 或更高版本。 数据仅含 2.0.0.0.2305。正式服 2.0.0.0.2320 未登记。")
            .Should().Be("数据仅含 2.0.0.0.2305。正式服 2.0.0.0.2320 未登记。");

    [Fact]
    public void Leaves_the_sentence_when_it_is_not_at_the_start()
    {
        const string text = "按 G 开关格线。从管理器安装或更新需要 1.3.13 或更高版本。";
        SummaryDisplay.StripManagerFloorPrefix(text).Should().Be(text);
    }

    [Theory]
    [InlineData("独立好友叠加面板：关注 / 关注我的人。")]
    [InlineData("")]
    public void Leaves_text_without_the_sentence_unchanged(string text) =>
        SummaryDisplay.StripManagerFloorPrefix(text).Should().Be(text);

    [Fact]
    public void Null_stays_null() =>
        SummaryDisplay.StripManagerFloorPrefix(null).Should().BeNull();

    [Fact]
    public void Sentence_alone_becomes_empty() =>
        SummaryDisplay.StripManagerFloorPrefix("Installing or updating through the manager requires 1.3.13 or newer.")
            .Should().BeEmpty();
}

using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class ModConflictGateTests
{
    static readonly ModConflictText Chinese = new(
        InstalledWithReason: "不能安装「{0}」。已安装的「{1}」与它冲突。{2}请先卸载「{1}」。",
        InstalledGeneric: "不能安装「{0}」。已安装的「{1}」与它冲突，不能同时安装。请先卸载「{1}」。",
        SelectionWithReason: "「{0}」和「{1}」不能同时安装。{2}请只选择其中一个。这次都没有安装。",
        SelectionGeneric: "「{0}」和「{1}」不能同时安装。请只选择其中一个。这次都没有安装。");

    [Fact]
    public void Fresh_install_is_blocked_when_partner_is_already_in_the_library()
    {
        var catalog = Pair();
        var library = new[] { Package("battle-suite") };

        var decision = ModConflictGate.Evaluate(catalog, library, ["damage-rank"], "zh-CN");

        decision.AllowedIds.Should().BeEmpty();
        decision.Blocks.Should().ContainSingle();
        var block = decision.Blocks[0];
        block.IncomingId.Should().Be("damage-rank");
        block.PartnerId.Should().Be("battle-suite");
        block.Cause.Should().Be(ModConflictCause.PartnerInLibrary);
        block.Reason.Should().Be("两者都包含伤害排名，同时安装进游戏会坏。");
    }

    [Fact]
    public void Updating_an_installed_mod_stays_allowed_while_its_partner_is_installed()
    {
        var catalog = Pair();
        var library = new[] { Package("damage-rank"), Package("battle-suite") };

        var decision = ModConflictGate.Evaluate(catalog, library, ["damage-rank"], "zh-CN");

        decision.AllowedIds.Should().Equal("damage-rank");
        decision.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Library_membership_blocks_a_fresh_install_without_looking_at_a_profile()
    {
        var catalog = Pair();
        var presentButNotEnabledAnywhere = Package("battle-suite");

        var decision = ModConflictGate.Evaluate(
            catalog,
            [presentButNotEnabledAnywhere],
            ["damage-rank"],
            "zh-CN");

        decision.AllowedIds.Should().BeEmpty();
        decision.Blocks.Should().ContainSingle(block => block.Cause == ModConflictCause.PartnerInLibrary);
    }

    [Fact]
    public void Two_fresh_conflicting_mods_are_both_blocked_and_an_unrelated_one_proceeds()
    {
        var catalog = Pair();
        catalog.Add(Mod("quick-camera", "快速相机"));

        var decision = ModConflictGate.Evaluate(
            catalog,
            [],
            ["damage-rank", "battle-suite", "quick-camera"],
            "zh-CN");

        decision.AllowedIds.Should().Equal("quick-camera");
        decision.Blocks.Select(block => block.IncomingId).Should().BeEquivalentTo("damage-rank", "battle-suite");
        decision.Blocks.Should().OnlyContain(block => block.Cause == ModConflictCause.PartnerInSelection);
    }

    [Fact]
    public void A_chain_allows_the_two_ends_and_blocks_all_three_when_the_middle_is_requested()
    {
        var catalog = Chain();

        ModConflictGate.Evaluate(catalog, [], ["a", "c"], "zh-CN")
            .AllowedIds.Should().Equal("a", "c");

        var all = ModConflictGate.Evaluate(catalog, [], ["a", "b", "c"], "zh-CN");
        all.AllowedIds.Should().BeEmpty();
        all.Blocks.Select(block => block.IncomingId).Distinct().Should().BeEquivalentTo("a", "b", "c");
        all.Blocks.Count(block => block.IncomingId == "b").Should().Be(2);
    }

    [Fact]
    public void One_sided_edge_blocks_both_directions()
    {
        var catalog = new List<CatalogMod>
        {
            Mod("damage-rank", "伤害排名", Conflict("battle-suite", "排名冲突。")),
            Mod("battle-suite", "钢铁战局助手")
        };

        var left = ModConflictGate.Evaluate(catalog, [Package("battle-suite")], ["damage-rank"], "zh-CN");
        left.Blocks.Should().ContainSingle();
        left.Blocks[0].Reason.Should().Be("排名冲突。");

        var right = ModConflictGate.Evaluate(catalog, [Package("damage-rank")], ["battle-suite"], "zh-CN");
        right.AllowedIds.Should().BeEmpty();
        right.Blocks.Should().ContainSingle();
        right.Blocks[0].Reason.Should().Be("排名冲突。");
    }

    [Fact]
    public void Self_blank_and_unknown_targets_are_ignored()
    {
        var catalog = new List<CatalogMod>
        {
            Mod("damage-rank", "伤害排名",
                Conflict("damage-rank", "自己"),
                Conflict("  ", "空白"),
                Conflict("missing", "不存在"),
                Conflict("battle-suite", "第二条才是真的。"),
                Conflict("battle-suite", "重复的不采用。")),
            Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", "反向。"))
        };

        var decision = ModConflictGate.Evaluate(catalog, [Package("battle-suite")], ["damage-rank"], "zh-CN");

        decision.Blocks.Should().ContainSingle();
        decision.Blocks[0].PartnerId.Should().Be("battle-suite");
        decision.Blocks[0].Reason.Should().Be("第二条才是真的。");
    }

    [Fact]
    public void Reason_falls_back_through_language_default_and_the_reverse_edge()
    {
        var withEnglish = Pair();
        ModConflictGate.Evaluate(withEnglish, [Package("battle-suite")], ["damage-rank"], "en")
            .Blocks[0].Reason.Should().Be("Both include a damage ranking. Installing them together breaks the game.");

        var defaultOnly = new List<CatalogMod>
        {
            Mod("damage-rank", "伤害排名", Conflict("battle-suite", "默认说明。")),
            Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", "反向默认。"))
        };
        ModConflictGate.Evaluate(defaultOnly, [Package("battle-suite")], ["damage-rank"], "en")
            .Blocks[0].Reason.Should().Be("默认说明。");

        var reverseOnly = new List<CatalogMod>
        {
            Mod("damage-rank", "伤害排名", Conflict("battle-suite", null)),
            Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", "用反向。"))
        };
        ModConflictGate.Evaluate(reverseOnly, [Package("battle-suite")], ["damage-rank"], "zh-CN")
            .Blocks[0].Reason.Should().Be("用反向。");

        var empty = new List<CatalogMod>
        {
            Mod("damage-rank", "伤害排名", Conflict("battle-suite", "  ")),
            Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", ""))
        };
        ModConflictGate.Evaluate(empty, [Package("battle-suite")], ["damage-rank"], "zh-CN")
            .Blocks[0].Reason.Should().BeNull();
    }

    [Fact]
    public void Names_use_the_catalog_locale()
    {
        var damage = Mod("damage-rank", "伤害排名", Conflict("battle-suite", "原因。"));
        damage.Locales = new Dictionary<string, CatalogModLocale>
        {
            ["en"] = new CatalogModLocale { Name = "Damage Ranking Mod" }
        };
        var battle = Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", "原因。"));
        battle.Locales = new Dictionary<string, CatalogModLocale>
        {
            ["en"] = new CatalogModLocale { Name = "BattleSuite" }
        };

        var block = ModConflictGate.Evaluate([damage, battle], [Package("battle-suite")], ["damage-rank"], "en")
            .Blocks[0];

        block.IncomingName.Should().Be("Damage Ranking Mod");
        block.PartnerName.Should().Be("BattleSuite");
    }

    [Fact]
    public void Format_uses_uninstall_and_choose_one_sentences_and_mentions_a_pair_once()
    {
        var installed = new ModConflictBlock(
            "damage-rank",
            "battle-suite",
            ModConflictCause.PartnerInLibrary,
            "伤害排名",
            "钢铁战局助手",
            "两者都包含伤害排名，同时安装进游戏会坏。");
        var freshA = new ModConflictBlock(
            "a", "b", ModConflictCause.PartnerInSelection, "甲", "乙", "一起会坏。");
        var freshB = new ModConflictBlock(
            "b", "a", ModConflictCause.PartnerInSelection, "乙", "甲", null);

        var text = ModConflictGate.Format([installed, freshA, freshB], Chinese);

        text.Should().Be(
            "不能安装「伤害排名」。已安装的「钢铁战局助手」与它冲突。两者都包含伤害排名，同时安装进游戏会坏。请先卸载「钢铁战局助手」。\n" +
            "「甲」和「乙」不能同时安装。一起会坏。请只选择其中一个。这次都没有安装。");
    }

    [Fact]
    public void Format_uses_the_generic_sentence_when_the_pair_has_no_reason()
    {
        var block = new ModConflictBlock(
            "a", "b", ModConflictCause.PartnerInSelection, "甲", "乙", null);
        var reverse = new ModConflictBlock(
            "b", "a", ModConflictCause.PartnerInSelection, "乙", "甲", "  ");

        ModConflictGate.Format([block, reverse], Chinese).Should().Be(
            "「甲」和「乙」不能同时安装。请只选择其中一个。这次都没有安装。");
    }

    [Fact]
    public void Imported_packages_are_judged_against_the_library_from_before_this_import()
    {
        var catalog = Pair();
        var imported = new[] { Package("damage-rank"), Package("battle-suite") };
        var already = Package("quick-camera");
        var libraryAfterImport = new[] { already, imported[0], imported[1] };

        var decision = ModConflictGate.Evaluate(
            catalog,
            libraryAfterImport.Where(pkg => imported.All(item => item.Id != pkg.Id)),
            ["damage-rank", "battle-suite"],
            "zh-CN");

        decision.AllowedIds.Should().BeEmpty();
        var keep = ModConflictGate.PackagesToKeep(imported, catalog, decision);
        keep.Should().BeEmpty();
    }

    [Fact]
    public void A_package_that_matches_no_catalog_mod_is_kept()
    {
        var imported = new[] { Package("local-only"), Package("damage-rank") };
        var decision = ModConflictGate.Evaluate(
            Pair(),
            [Package("battle-suite")],
            ["damage-rank"],
            "zh-CN");

        ModConflictGate.PackagesToKeep(imported, Pair(), decision)
            .Select(pkg => pkg.Id)
            .Should().Equal("local-only");
    }

    static List<CatalogMod> Pair() =>
    [
        Mod("damage-rank", "伤害排名", Conflict("battle-suite", "两者都包含伤害排名，同时安装进游戏会坏。",
            ("en", "Both include a damage ranking. Installing them together breaks the game."))),
        Mod("battle-suite", "钢铁战局助手", Conflict("damage-rank", "两者都包含伤害排名，同时安装进游戏会坏。",
            ("en", "Both include a damage ranking. Installing them together breaks the game.")))
    ];

    static List<CatalogMod> Chain() =>
    [
        Mod("a", "A", Conflict("b", "ab")),
        Mod("b", "B", Conflict("a", "ba"), Conflict("c", "bc")),
        Mod("c", "C", Conflict("b", "cb"))
    ];

    static CatalogMod Mod(string id, string name, params CatalogConflict[] conflicts) => new()
    {
        Id = id,
        Name = name,
        Conflicts = conflicts.Length == 0 ? null : conflicts.ToList()
    };

    static CatalogConflict Conflict(string id, string? reason, params (string Lang, string Reason)[] locales) => new()
    {
        Id = id,
        Reason = reason,
        Locales = locales.Length == 0
            ? null
            : locales.ToDictionary(
                item => item.Lang,
                item => new CatalogConflictLocale { Reason = item.Reason },
                StringComparer.OrdinalIgnoreCase)
    };

    static ModPackage Package(string catalogId) => new()
    {
        Id = catalogId,
        CatalogId = catalogId,
        DisplayName = catalogId
    };
}

using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Read-only primary-cause diagnosis. Does not touch disk.
/// Status bar and diagnostics zip must call the same Evaluate().
/// </summary>
public static class DiagnosisEngine
{
    public static Diagnosis Evaluate(DiagnosisSnapshot snap)
    {
        snap ??= new DiagnosisSnapshot();

        if (snap.HasInterruptedCriticalOp)
            return Build(DiagnosisCodes.CriticalOpInterrupted, snap, CriticalOpEvidence(snap));

        if (snap.IsWizardOrSettleBlocking || snap.IsAwaitingSteamSettle || snap.SettleAbandonedUnaligned)
            return Build(DiagnosisCodes.WizardOrSettleBlocking, snap, WizardEvidence(snap));

        if (snap.GameMissing || snap.LinkIncompleteButStoreValid)
            return Build(DiagnosisCodes.GameOrLinkIncomplete, snap, GameEvidence(snap));

        if (snap.SteamUpdateUnhealthy || snap.AcfNotSettled)
            return Build(DiagnosisCodes.SteamUpdateUnhealthy, snap, SteamEvidence(snap));

        if (snap.MelonNeedsUpgrade)
            return Build(DiagnosisCodes.MelonNeedsUpgrade, snap, MelonEvidence(snap));

        if (snap.LoaderNotInjected)
            return Build(DiagnosisCodes.LoaderNotInjected, snap, InjectEvidence(snap));

        if (snap.CatalogOrUpdateNetworkFailed)
            return Build(DiagnosisCodes.CatalogOrUpdateNetwork, snap, NetworkEvidence(snap));

        if (snap.DeployBlockedOrFailed)
            return Build(DiagnosisCodes.DeployBlockedOrFailed, snap, DeployEvidence(snap));

        if (snap.AcfBetaMismatch)
            return Build(DiagnosisCodes.AcfBetaMismatch, snap, MismatchEvidence(snap));

        return Build(DiagnosisCodes.Healthy, snap, Array.Empty<string>());
    }

    static Diagnosis Build(string code, DiagnosisSnapshot snap, IReadOnlyList<string> evidence)
    {
        var (title, action) = Copy(code, snap);
        return new Diagnosis
        {
            Code = code,
            Title = title,
            Action = action,
            Evidence = evidence,
            RuledOut = RuledOut(snap, code),
            Also = Also(snap, code)
        };
    }

    static IReadOnlyList<string> RuledOut(DiagnosisSnapshot snap, string code)
    {
        var list = new List<string>();
        if (!snap.DualFolderLooksBroken && code != DiagnosisCodes.GameOrLinkIncomplete)
            list.Add(DiagnosisCodes.DualFolderBroken);
        if (!snap.GameMissing && !snap.LinkIncompleteButStoreValid && code != DiagnosisCodes.GameOrLinkIncomplete)
            list.Add(DiagnosisCodes.GameOrLinkIncomplete);
        if (!snap.MelonNeedsUpgrade && code != DiagnosisCodes.MelonNeedsUpgrade)
            list.Add(DiagnosisCodes.MelonNeedsUpgrade);
        return list;
    }

    static IReadOnlyList<string> Also(DiagnosisSnapshot snap, string primary)
    {
        var list = new List<string>();
        void Add(string code, bool cond)
        {
            if (cond && code != primary)
                list.Add(code);
        }

        Add(DiagnosisCodes.AcfBetaMismatch, snap.AcfBetaMismatch);
        Add(DiagnosisCodes.LoaderNotInjected, snap.LoaderNotInjected);
        Add(DiagnosisCodes.CatalogOrUpdateNetwork, snap.CatalogOrUpdateNetworkFailed);
        Add(DiagnosisCodes.DeployBlockedOrFailed, snap.DeployBlockedOrFailed);
        Add(DiagnosisCodes.MelonNeedsUpgrade, snap.MelonNeedsUpgrade);
        return list;
    }

    static IReadOnlyList<string> CriticalOpEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(snap.CriticalOpKind))
            list.Add($"criticalOpKind={snap.CriticalOpKind}");
        if (!string.IsNullOrWhiteSpace(snap.CriticalOpDetail))
            list.Add($"criticalOpDetail={snap.CriticalOpDetail}");
        return list;
    }

    static IReadOnlyList<string> WizardEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(snap.BranchWizardStep))
            list.Add($"wizardStep={snap.BranchWizardStep}");
        if (snap.IsAwaitingSteamSettle)
            list.Add("isAwaitingSteamSettle=true");
        if (snap.SettleAbandonedUnaligned)
            list.Add("settleAbandonedUnaligned=true");
        return list;
    }

    static IReadOnlyList<string> GameEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (snap.GameMissing)
            list.Add("gameMissing=true");
        if (snap.LinkIncompleteButStoreValid)
            list.Add("linkIncompleteButStoreValid=true");
        return list;
    }

    static IReadOnlyList<string> SteamEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (snap.SteamUpdateUnhealthy)
            list.Add("steamUpdateUnhealthy=true");
        if (snap.AcfNotSettled)
            list.Add("acfNotSettled=true");
        return list;
    }

    static IReadOnlyList<string> MelonEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(snap.MelonVersion))
            list.Add($"melonVersion={snap.MelonVersion}");
        if (snap.LatestLogAge is { } age)
            list.Add($"latestLogAgeSeconds={Math.Round(age.TotalSeconds, 1)}");
        return list;
    }

    static IReadOnlyList<string> InjectEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string> { "loaderNotInjected=true" };
        if (snap.LastLaunchRequestedAt is { } at)
            list.Add($"lastLaunchRequestedAt={at:o}");
        if (snap.LatestLogAge is { } age)
            list.Add($"latestLogAgeSeconds={Math.Round(age.TotalSeconds, 1)}");
        return list;
    }

    static IReadOnlyList<string> NetworkEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string> { "catalogOrUpdateNetworkFailed=true" };
        if (!string.IsNullOrWhiteSpace(snap.NetworkFailureSource))
            list.Add($"source={snap.NetworkFailureSource}");
        return list;
    }

    static IReadOnlyList<string> DeployEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string> { "deployBlockedOrFailed=true" };
        if (!string.IsNullOrWhiteSpace(snap.DeployFailure))
            list.Add($"deployFailure={snap.DeployFailure}");
        return list;
    }

    static IReadOnlyList<string> MismatchEvidence(DiagnosisSnapshot snap)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(snap.SteamBetaKey))
            list.Add($"steamBetaKey={snap.SteamBetaKey}");
        if (!string.IsNullOrWhiteSpace(snap.ActiveGameBranch))
            list.Add($"activeGameBranch={snap.ActiveGameBranch}");
        return list;
    }

    static (string Title, string Action) Copy(string code, DiagnosisSnapshot snap)
    {
        if (code == DiagnosisCodes.WizardOrSettleBlocking)
            return WizardSettleCopy(snap);

        var titleKey = "DiagnosisTitle_" + code;
        var actionKey = "DiagnosisAction_" + code;
        var title = LocalizationService.T(titleKey);
        var action = LocalizationService.T(actionKey);
        if (string.Equals(title, titleKey, StringComparison.Ordinal))
            title = FallbackTitle(code);
        if (string.Equals(action, actionKey, StringComparison.Ordinal))
            action = FallbackAction(code);
        return (title, action);
    }

    static (string Title, string Action) WizardSettleCopy(DiagnosisSnapshot snap)
    {
        if (snap.SettleAbandonedUnaligned)
            return LocalizedOrFallback(
                "DiagnosisTitle_settle_abandoned_unaligned",
                "DiagnosisAction_settle_abandoned_unaligned",
                "结算未对齐，请先处理 Steam 清单",
                "请先在 Steam 把清单对齐到可开始游戏，再回管理器继续。不要只改 Steam 测试勾选。");

        if (snap.IsAwaitingSteamSettle && !IsMidWizardStep(snap.BranchWizardStep))
            return LocalizedOrFallback(
                "DiagnosisTitle_awaiting_steam_settle",
                "DiagnosisAction_awaiting_steam_settle",
                "请在设置页点金色继续，完成 Steam 结算",
                "先在 Steam 等到可开始游戏（目录含 Mechabellum.exe 与 GameAssembly.dll），再回设置页点金色继续。结算不必先退出 Steam。");

        return LocalizedOrFallback(
            "DiagnosisTitle_wizard_or_settle_blocking",
            "DiagnosisAction_wizard_or_settle_blocking",
            "双服向导未完成",
            "请按管理器提示继续双服向导（如下载另一服后退出 Steam）。不要反复点启用。");
    }

    static bool IsMidWizardStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
            return false;
        return step is not nameof(BranchWizardStep.None)
            and not nameof(BranchWizardStep.Ready)
            and not nameof(BranchWizardStep.AwaitingSteamSettle);
    }

    static (string Title, string Action) LocalizedOrFallback(
        string titleKey, string actionKey, string titleFb, string actionFb)
    {
        var title = LocalizationService.T(titleKey);
        var action = LocalizationService.T(actionKey);
        if (string.Equals(title, titleKey, StringComparison.Ordinal))
            title = titleFb;
        if (string.Equals(action, actionKey, StringComparison.Ordinal))
            action = actionFb;
        return (title, action);
    }

    static string FallbackTitle(string code) => code switch
    {
        DiagnosisCodes.CriticalOpInterrupted => "上次关键操作未完成",
        DiagnosisCodes.WizardOrSettleBlocking => "双服向导未完成",
        DiagnosisCodes.GameOrLinkIncomplete => "游戏路径或目录联接不完整",
        DiagnosisCodes.SteamUpdateUnhealthy => "Steam 更新未完成或状态异常",
        DiagnosisCodes.MelonNeedsUpgrade => "MelonLoader 版本低于内置 0.7.3",
        DiagnosisCodes.LoaderNotInjected => "Loader 未在本次启动注入",
        DiagnosisCodes.CatalogOrUpdateNetwork => "目录或检查更新网络失败",
        DiagnosisCodes.DeployBlockedOrFailed => "方案部署被阻止或失败",
        DiagnosisCodes.AcfBetaMismatch => "Steam 分支与管理器当前仓不一致",
        DiagnosisCodes.Healthy => "未发现阻断问题",
        _ => code
    };

    static string FallbackAction(string code) => code switch
    {
        DiagnosisCodes.CriticalOpInterrupted => "按管理器提示完成或放弃未完成的关键操作，不要同时再切服或覆盖安装。",
        DiagnosisCodes.WizardOrSettleBlocking => "请按管理器提示继续双服向导（如下载另一服后退出 Steam）。不要反复点启用。",
        DiagnosisCodes.GameOrLinkIncomplete => "勿反复点继续；等 Steam 下载完成，或验证游戏文件后再打开管理器。",
        DiagnosisCodes.SteamUpdateUnhealthy => "在 Steam 把游戏更新到可开始游戏；勿停在更新失败。然后完全退出 Steam。",
        DiagnosisCodes.MelonNeedsUpgrade => "在管理器右上角点「安装 MelonLoader」升级到内置 0.7.3，再启动游戏。",
        DiagnosisCodes.LoaderNotInjected => "完全退出游戏与 Steam 后，用管理器「应用并启动」再等约 20 秒；若仍未注入，导出诊断包。",
        DiagnosisCodes.CatalogOrUpdateNetwork => "检查系统代理或自建镜像基址后重试刷新/检查更新。",
        DiagnosisCodes.DeployBlockedOrFailed => "按弹窗处理缺包或占用；关闭游戏后再应用方案。",
        DiagnosisCodes.AcfBetaMismatch => "这不是双服损坏。切服请走管理器，不要只在 Steam 勾选测试分支。",
        DiagnosisCodes.Healthy => "按管理器提示操作即可。",
        _ => "导出诊断包发给维护者。"
    };
}

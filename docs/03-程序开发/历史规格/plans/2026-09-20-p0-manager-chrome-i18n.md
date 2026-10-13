# P0 Manager Chrome i18n Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Localize the highest-visibility hardcoded Chinese chrome in the Mod Manager (settings browse/profile buttons, launch-mode labels, dirty/status labels) so switching UI language refreshes them.

**Architecture:** Add keys to all five `Strings*.resx`, expose button labels via `UiStrings`, bind XAML; make `LaunchModeOption.Label` mutable and refresh on `ApplyUiLanguage`; resolve `DirtyHint` / `StatusKindLabel` via `LocalizationService.T`. Defer bulk `AppendLog` migration to a later plan.

**Tech Stack:** WPF, .NET resx, xUnit + FluentAssertions, `tools/add-resource-key.ps1`

**Working branch:** `fix/p0-manager-chrome-i18n` (working tree only until user asks to commit — **no Commit steps**)

## Global Constraints

- Supported cultures remain exactly: `zh-CN`, `en`, `ru`, `ja`, `de` (+ `system`)
- Every new key must exist in all five resx files (ResourceKeyParityTests)
- Prefer `Ui.Property` bindings for XAML; `LocalizationService.T` for computed VM strings
- Do not migrate MainViewModel log flood in this plan

---

### Task 1: Resx keys for chrome strings

**Files:**
- Modify: `src/MechabellumModManager/Resources/Strings.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.en.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ru.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ja.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.de.resx`
- Test: `tests/MechabellumModManager.Tests/ResourceKeyParityTests.cs` (existing)

**Keys to add** (anchor after `LaunchMode` unless noted):

| Key | zh-CN | en | ru | ja | de |
|-----|-------|----|----|----|-----|
| BrowseEllipsis | 浏览… | Browse… | Обзор… | 参照… | Durchsuchen… |
| ProfileNew | 新建 | New | Создать | 新規 | Neu |
| ProfileRename | 重命名 | Rename | Переименовать | 名前変更 | Umbenennen |
| ProfileDuplicate | 复制 | Duplicate | Дублировать | 複製 | Duplizieren |
| ProfileDelete | 删除 | Delete | Удалить | 削除 | Löschen |
| LaunchModeSteamThenExe | Steam 优先，失败则直启 | Steam first, then exe | Сначала Steam, иначе exe | Steam優先、失敗時は直接起動 | Steam zuerst, sonst exe |
| LaunchModeSteamOnly | 仅 Steam | Steam only | Только Steam | Steamのみ | Nur Steam |
| LaunchModeExeOnly | 仅直启 exe | Exe only | Только exe | exe直接起動のみ | Nur exe |
| DirtyHintDirty | 方案已改，游戏目录未同步 — 请点击「应用方案」 | Profile changed; game folder not synced — click Apply Profile | Профиль изменён; папка игры не синхронизирована — нажмите «Применить» | 構成が変更され、ゲームフォルダー未同期 — 「構成を適用」をクリック | Profil geändert; Spielordner nicht synchron — «Profil anwenden» klicken |
| DirtyHintClean | 已与游戏目录同步 | Synced with game folder | Синхронизировано с папкой игры | ゲームフォルダーと同期済み | Mit Spielordner synchron |
| StatusKindReady | 就绪 | Ready | Готово | 準備完了 | Bereit |
| StatusKindAssembliesMissing | 待生成程序集 | Assemblies pending | Сборки не сгенерированы | アセンブリ未生成 | Assemblies ausstehend |
| StatusKindLoaderMissing | 缺少 Loader | Loader missing | Нет Loader | Loaderなし | Loader fehlt |
| StatusKindLoaderPartial | Loader 不完整 | Loader incomplete | Loader неполный | Loader不完全 | Loader unvollständig |
| StatusKindGameMissing | 未找到游戏 | Game not found | Игра не найдена | ゲーム未検出 | Spiel nicht gefunden |

- [x] **Step 1: Add all keys via `tools/add-resource-key.ps1`** (one invocation per key, anchor `LaunchMode`)

- [x] **Step 2: Run parity tests**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter ResourceKeyParityTests
```

Expected: PASS

---

### Task 2: UiStrings + XAML bindings for buttons

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`
- Modify: `src/MechabellumModManager/MainWindow.xaml` (Browse… / 新建 / 重命名 / 复制 / 删除)

**Interfaces:**
- Produces: `UiStrings.BrowseEllipsis`, `ProfileNew`, `ProfileRename`, `ProfileDuplicate`, `ProfileDelete`

- [ ] **Step 1: Add properties to UiStrings** after `LaunchMode`:

```csharp
public string BrowseEllipsis => T("BrowseEllipsis");
public string ProfileNew => T("ProfileNew");
public string ProfileRename => T("ProfileRename");
public string ProfileDuplicate => T("ProfileDuplicate");
public string ProfileDelete => T("ProfileDelete");
```

- [ ] **Step 2: Bind MainWindow.xaml**

```xml
<Button Content="{Binding Ui.BrowseEllipsis}" .../>
<Button Content="{Binding Ui.ProfileNew}" .../>
<Button Content="{Binding Ui.ProfileRename}" .../>
<Button Content="{Binding Ui.ProfileDuplicate}" .../>
<Button Content="{Binding Ui.ProfileDelete}" .../>
```

- [ ] **Step 3: Build**

```powershell
dotnet build src/MechabellumModManager/MechabellumModManager.csproj -c Debug
```

Expected: 0 errors

---

### Task 3: LaunchMode labels refresh on language change (TDD)

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/LaunchModeOption.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (ctor init + `ApplyUiLanguage`)
- Test: `tests/MechabellumModManager.Tests/UiLanguageSwitchTests.cs`

**Interfaces:**
- Consumes: `LocalizationService.T("LaunchModeSteamThenExe"|…)`
- Produces: `LaunchModeOption` with mutable `Label` + `INotifyPropertyChanged`; `RefreshLaunchModeLabels()` called from `ApplyUiLanguage` when `refreshUi`

- [ ] **Step 1: Write failing test** in `UiLanguageSwitchTests.cs`:

```csharp
[Fact]
public void Switching_language_refreshes_launch_mode_labels()
{
    using var fx = Fixture.CreateReady(uiLanguage: "zh-CN");
    var vm = fx.CreateVm();
    vm.LaunchModeOptions.Single(o => o.Mode == LaunchMode.SteamOnly).Label
        .Should().Be(LocalizationService.T("LaunchModeSteamOnly")); // Chinese while zh-CN

    vm.SelectedUiLanguageCode = "en";

    vm.LaunchModeOptions.Single(o => o.Mode == LaunchMode.SteamOnly).Label
        .Should().Be("Steam only");
    vm.LaunchModeOptions.Single(o => o.Mode == LaunchMode.ExeOnly).Label
        .Should().Be("Exe only");
}
```

- [ ] **Step 2: Run test — expect FAIL** (labels stay Chinese)

```powershell
dotnet test tests/MechabellumModManager.Tests --filter Switching_language_refreshes_launch_mode_labels
```

- [ ] **Step 3: Implement** — convert `LaunchModeOption` to mutable class (mirror `LanguageOption`); init labels via `T(...)` in ctor; add `RefreshLaunchModeLabels()` and call it inside `ApplyUiLanguage` when `refreshUi` is true (before or after `Ui.Refresh()`).

- [ ] **Step 4: Run test — expect PASS**

---

### Task 4: DirtyHint + StatusKindLabel via resx (TDD)

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`DirtyHint`, `StatusKindLabel`)
- Test: `tests/MechabellumModManager.Tests/UiLanguageSwitchTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void Switching_language_refreshes_dirty_and_status_labels()
{
    using var fx = Fixture.CreateReady(uiLanguage: "zh-CN");
    var vm = fx.CreateVm();
    vm.DirtyHint.Should().Be(LocalizationService.T("DirtyHintClean"));
    vm.StatusKindLabel.Should().Be(LocalizationService.T("StatusKindReady"));

    vm.SelectedUiLanguageCode = "en";

    vm.DirtyHint.Should().Be("Synced with game folder");
    vm.StatusKindLabel.Should().Be("Ready");
}
```

- [ ] **Step 2: Run — expect FAIL** if properties still hardcoded (or pass if already using T after Task 1 culture apply — in that case still assert notify on switch by checking English values)

- [ ] **Step 3: Implement**

```csharp
public string StatusKindLabel => GameStatus?.Kind switch
{
    GameStatusKind.Ready => LocalizationService.T("StatusKindReady"),
    GameStatusKind.LoaderPresentAssembliesMissing => LocalizationService.T("StatusKindAssembliesMissing"),
    GameStatusKind.GameOkLoaderMissing => LocalizationService.T("StatusKindLoaderMissing"),
    GameStatusKind.LoaderPartial => LocalizationService.T("StatusKindLoaderPartial"),
    GameStatusKind.GameMissing => LocalizationService.T("StatusKindGameMissing"),
    _ => ""
};

public string DirtyHint => IsDirty
    ? LocalizationService.T("DirtyHintDirty")
    : LocalizationService.T("DirtyHintClean");
```

Also raise `OnPropertyChanged(nameof(DirtyHint))` and `OnPropertyChanged(nameof(StatusKindLabel))` inside `ApplyUiLanguage` when `refreshUi`.

- [ ] **Step 4: Run test — expect PASS**

---

### Task 5: Full verification

- [ ] **Step 1: Run relevant tests**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UiLanguageSwitchTests|FullyQualifiedName~ResourceKeyParityTests|FullyQualifiedName~LocalizationServiceTests"
```

Expected: all PASS

- [ ] **Step 2: Confirm no leftover hardcoded chrome**

```powershell
Select-String -Path src/MechabellumModManager/MainWindow.xaml -Pattern 'Content="(浏览|新建|重命名|复制|删除)'
Select-String -Path src/MechabellumModManager/ViewModels/MainViewModel.cs -Pattern 'Steam 优先|仅 Steam|仅直启|方案已改|已与游戏目录同步|"就绪"'
```

Expected: no matches for those chrome strings

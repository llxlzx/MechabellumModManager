# UI Icons, Catalog Multi-select, Apply Fix, Installer 5 Languages (v1.0.9) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v1.0.9 with fixed Apply enablement/feedback, catalog multi-select + detail collapse/report, labeled browse collapse, Path icons on key actions, and a five-language installer that seeds `UiLanguage`.

**Architecture:** Keep logic in `MainViewModel` + thin `MainWindow` selection bridges; add a shared `Icons.xaml` ResourceDictionary of Path geometries; harden Inno `WriteManagerConfigNative` to merge JSON and write `uiLanguage`. No third-party icon packs.

**Tech Stack:** WPF (.NET 8), CommunityToolkit.Mvvm, FluentAssertions/xUnit, Inno Setup 6.

## Global Constraints

- Target version remains **1.0.9** (csproj / ISS already bumped).
- Do **not** make library multi-select drive Apply (`CanApplyProfile := CanDeployOrLaunch && IsDirty` only).
- Icons = **Path geometries only** (no MahApps/Material).
- Installer languages: **chinesesimplified / english / russian / japanese / german** → `zh-CN` / `en` / `ru` / `ja` / `de`.
- `WriteManagerConfigNative` must **merge**, never clobber, `config.json`.
- Browse collapse toggle must remain visible when body is collapsed (header outside `CatalogExpanded` gate).
- Do **not** git commit unless the user explicitly asks in this session (user rule overrides plan commit steps — treat commit steps as optional/skipped).
- Spec: `docs/superpowers/specs/2026-09-05-ui-icons-catalog-multiselect-installer-langs-design.md`.

## File map

| File | Responsibility |
|------|----------------|
| `ViewModels/MainViewModel.cs` | CanApply, catalog selection set, batch add, clear catalog selection, notify on apply |
| `ViewModels/CatalogModItemViewModel.cs` | Optional `IsCatalogSelected` if used; prefer grid SelectedItems as authority |
| `ViewModels/UiStrings.cs` + `Resources/Strings*.resx` | CollapseBrowse / ExpandBrowse / Apply success notify / icon-adjacent strings if needed |
| `MainWindow.xaml` / `.xaml.cs` | Layout: header split, checkboxes, icons, Apply style, selection handlers |
| `Assets/Icons.xaml` (+ App.xaml merge) | Shared Path geometries |
| `App.xaml` | Merge Icons dictionary; optional Apply button style with dirty trigger |
| `installer/MechabellumModManager.iss` | 5 languages, CustomMessages, merge config + uiLanguage |
| `installer/EULA.ru.txt`, `EULA.ja.txt`, `EULA.de.txt` | License texts |
| `tests/.../MainViewModelTests.cs` | CanApplyProfile / selection count tests |
| `release/v1.0.9/latest.json` | Notes update after build |

---

### Task 1: Fix CanApplyProfile + Apply feedback + unit test

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`CanApplyProfile`, `ApplyProfile`, `OnLibrarySelectionCountChanged` notify cleanup)
- Modify: `src/MechabellumModManager/Resources/Strings.resx`, `Strings.en.resx`, `Strings.de.resx`, `Strings.ja.resx`, `Strings.ru.resx`
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`
- Modify: `src/MechabellumModManager/MainWindow.xaml` (Apply button style binding)
- Test: `tests/MechabellumModManager.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: existing `CanDeployOrLaunch`, `IsDirty`, `LibrarySelectionCount`, `_notify`, `ApplyProfile()`
- Produces: `CanApplyProfile => CanDeployOrLaunch && IsDirty`; property `UseApplyAccent => CanApplyProfile` (or bind style via `IsDirty` + ready); string key `NotifyApplySucceeded`

- [ ] **Step 1: Write the failing test**

Add to `MainViewModelTests.cs`:

```csharp
[Fact]
public void CanApplyProfile_false_when_only_library_selection_and_not_dirty()
{
    using var fx = Fixture.CreateReady();
    var vm = fx.CreateVm(confirmHighRisk: _ => true);

    vm.IsDirty.Should().BeFalse();
    vm.LibrarySelectionCount = 3;

    vm.CanApplyProfile.Should().BeFalse();
    vm.ApplyProfileCommand.CanExecute(null).Should().BeFalse();
}

[Fact]
public void CanApplyProfile_true_when_dirty_and_ready()
{
    using var fx = Fixture.CreateReady();
    var vm = fx.CreateVm(confirmHighRisk: _ => true);

    vm.Mods[0].IsEnabled = true;
    vm.IsDirty.Should().BeTrue();
    vm.CanApplyProfile.Should().BeTrue();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
Set-Location -LiteralPath "D:\gongzuo\钢铁指挥官mod管理器开发"
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~CanApplyProfile" -v n
```

Expected: `CanApplyProfile_false_when_only_library_selection_and_not_dirty` **FAIL** (currently true because of `LibrarySelectionCount > 0`).

- [ ] **Step 3: Implement predicate + notify + style**

In `MainViewModel.cs` change:

```csharp
public bool CanApplyProfile =>
    CanDeployOrLaunch && IsDirty;

public bool UseApplyAccent => CanApplyProfile;
```

In `ApplyProfile` success path (after `RecomputeDirty(); return true;`), before return:

```csharp
_notify(LocalizationService.T("NotifyApplySucceeded"));
```

Add resx (all five language files) + `UiStrings`:

- `NotifyApplySucceeded`: zh「方案已应用到游戏目录」 / en「Profile applied to the game folder」 / de/ja/ru equivalents.

`MainWindow.xaml` Apply button — replace fixed `AccentButtonStyle` with style trigger on `UseApplyAccent` (example):

```xml
<Button Command="{Binding ApplyProfileCommand}"
        MinHeight="32" MinWidth="96" Margin="8,0,0,0" Padding="12,8">
  <Button.Style>
    <Style TargetType="Button" BasedOn="{StaticResource GhostButtonStyle}">
      <Style.Triggers>
        <DataTrigger Binding="{Binding UseApplyAccent}" Value="True">
          <Setter Property="Background" Value="#FF2A2418" />
          <Setter Property="BorderBrush" Value="{StaticResource AccentAmberBrush}" />
          <Setter Property="Foreground" Value="{StaticResource AccentAmberHotBrush}" />
          <Setter Property="FontWeight" Value="SemiBold" />
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </Button.Style>
  <!-- Content with icon added in Task 6; text-only OK here -->
  <TextBlock Text="{Binding Ui.ApplyProfile}" />
</Button>
```

Remove redundant `IsEnabled="{Binding CanApplyProfile}"` if Command CanExecute already gates (keep one source: Command only).

Ensure `OnIsDirtyChanged` / status refresh still call `ApplyProfileCommand.NotifyCanExecuteChanged()` and `OnPropertyChanged(nameof(UseApplyAccent))`. `OnLibrarySelectionCountChanged` may still update selection UI but must **not** be required for Apply (can leave NotifyCanExecuteChanged — harmless).

- [ ] **Step 4: Run tests**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~CanApplyProfile|FullyQualifiedName~Toggle_enable_marks_dirty" -v n
```

Expected: PASS.

- [ ] **Step 5: Commit** — skip unless user asked.

---

### Task 2: Clear selection UnselectAll (library + catalog)

**Files:**
- Modify: `MainWindow.xaml.cs`
- Modify: `MainViewModel.cs` (`ClearLibraryModSelection`, add `ClearCatalogModSelection`)
- Modify: `MainWindow.xaml` (wire catalog clear; fix library clear)

**Interfaces:**
- Produces: `ClearCatalogModSelectionCommand`; event or callback so code-behind can `UnselectAll`
- Pattern: VM sets selection properties null + raises; code-behind listens via existing grid names

- [ ] **Step 1: Add code-behind helpers**

In `MainWindow.xaml.cs`:

```csharp
internal void UnselectLibraryMods()
{
    if (LibraryModsGrid != null)
        LibraryModsGrid.UnselectAll();
}

internal void UnselectCatalogMods()
{
    if (CatalogModsGrid != null)
        CatalogModsGrid.UnselectAll();
}
```

Give catalog DataGrid `x:Name="CatalogModsGrid"`.

- [ ] **Step 2: VM clear commands**

```csharp
[RelayCommand]
void ClearLibraryModSelection()
{
    SelectedLibraryMod = null;
    LibrarySelectionCount = 0;
    // Code-behind: call from CommandParameter or inject Action
}

[RelayCommand]
void ClearCatalogModSelection()
{
    SelectedCatalogMod = null;
    CatalogSelectionCount = 0;
    SelectedCatalogMods.Clear(); // if using ObservableCollection tracking
}
```

Inject optional `Action? unselectLibrary` / `Action? unselectCatalog` in ctor (like other UI callbacks), wired in `App.xaml.cs` / `MainWindow` ctor to the helpers above. Call them from the clear methods.

- [ ] **Step 3: Manual check** — open app, select library row, 收起介绍 → detail gone and selection count 0.

- [ ] **Step 4: Commit** — skip unless user asked.

---

### Task 3: Catalog Extended multi-select + checkboxes + batch 加入本地库

**Files:**
- Modify: `MainWindow.xaml` (catalog DataGrid)
- Modify: `MainWindow.xaml.cs` (`CatalogModsGrid_SelectionChanged`)
- Modify: `MainViewModel.cs` (`CatalogSelectionCount`, selected list, `AddCatalogModToLibraryAsync`, `CanAddCatalogMod`)

**Interfaces:**
- Produces: `int CatalogSelectionCount`; `IReadOnlyList<CatalogModItemViewModel> GetSelectedCatalogMods()` or private list updated from UI
- Consumes: `IsInLibrary` on items

- [ ] **Step 1: XAML grid**

```xml
<DataGrid x:Name="CatalogModsGrid"
          ItemsSource="{Binding CatalogModsView}"
          SelectedItem="{Binding SelectedCatalogMod}"
          SelectionMode="Extended"
          SelectionChanged="CatalogModsGrid_SelectionChanged"
          IsReadOnly="True" ...>
  <DataGrid.Columns>
    <DataGridTemplateColumn Width="36">
      <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
          <CheckBox HorizontalAlignment="Center" VerticalAlignment="Center"
                    IsChecked="{Binding IsSelected, RelativeSource={RelativeSource AncestorType=DataGridRow}, Mode=TwoWay}"
                    IsHitTestVisible="False" Focusable="False" />
        </DataTemplate>
      </DataGridTemplateColumn.CellTemplate>
    </DataGridTemplateColumn>
    <!-- existing columns -->
  </DataGrid.Columns>
</DataGrid>
```

Note: Binding row `IsSelected` keeps checkbox synced with Extended selection without a separate VM flag (avoids desync on refresh). If `IsSelected` on DataGridRow is not DP-bindable in this WPF version, use code-behind checkbox Click that toggles `row.IsSelected` instead.

- [ ] **Step 2: SelectionChanged bridge**

```csharp
private void CatalogModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (DataContext is not MainViewModel vm) return;
    if (sender is not DataGrid grid) return;
    vm.SetCatalogSelection(grid.SelectedItems.Cast<CatalogModItemViewModel>().ToList());
}
```

```csharp
// MainViewModel
readonly List<CatalogModItemViewModel> _catalogSelection = new();
public int CatalogSelectionCount { get; private set; }

public void SetCatalogSelection(IReadOnlyList<CatalogModItemViewModel> items)
{
    _catalogSelection.Clear();
    _catalogSelection.AddRange(items);
    CatalogSelectionCount = _catalogSelection.Count;
    OnPropertyChanged(nameof(CatalogSelectionCount));
    if (SelectedCatalogMod is null || !_catalogSelection.Contains(SelectedCatalogMod))
    {
        // keep SelectedCatalogMod as grid SelectedItem binding primary; do not fight binding
    }
    AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
}
```

On catalog rebuild (`RefreshCatalogAsync`), after replacing `CatalogMods`, call `SetCatalogSelection(Array.Empty<...>())` and ensure grid UnselectAll via injected action.

- [ ] **Step 3: Batch add**

Refactor `AddCatalogModToLibraryAsync`:

```csharp
bool CanAddCatalogMod() =>
    !_addingCatalogMod &&
    _catalogSelection.Any(i => !i.IsInLibrary);

async Task AddCatalogModToLibraryAsync()
{
    if (_addingCatalogMod) return;
    var targets = _catalogSelection.Where(i => !i.IsInLibrary).ToList();
    if (targets.Count == 0) return;
    _addingCatalogMod = true;
    AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
    try
    {
        foreach (var item in targets)
            await AddOneCatalogModAsync(item).ConfigureAwait(true); // extract current body
    }
    finally
    {
        _addingCatalogMod = false;
        AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
    }
}
```

Extract current single-item body into `AddOneCatalogModAsync(CatalogModItemViewModel item)` unchanged aside from using `item` parameter. For already-in-library rows in selection, skip with log (they are filtered out of targets; optionally log skipped count).

- [ ] **Step 4: Test filter (optional unit)** — if hard to mock download, skip automated batch test; manually verify Ctrl+click two not-in-library rows → 加入本地库.

- [ ] **Step 5: Commit** — skip unless user asked.

---

### Task 4: Catalog detail 举报 + 收起介绍

**Files:**
- Modify: `MainWindow.xaml` (catalog detail strip ~490–544)

- [ ] **Step 1: Add buttons next to detail** (mirror library):

```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
  <Button Content="{Binding Ui.Report}"
          Command="{Binding ReportCatalogModCommand}" ... Style Ghost />
  <Button Content="{Binding Ui.ActionCollapseDetail}"
          Command="{Binding ClearCatalogModSelectionCommand}" ... Style Ghost />
</StackPanel>
```

Ensure `ReportCatalogModCommand` uses `SelectedCatalogMod` (already). Icons added in Task 6.

- [ ] **Step 2: Smoke** — select catalog row → detail → 收起介绍 hides detail; 举报 opens dialog.

- [ ] **Step 3: Commit** — skip unless user asked.

---

### Task 5: Labeled browse collapse (header outside visibility gate)

**Files:**
- Modify: `MainWindow.xaml` (catalog section structure)
- Modify: `Strings*.resx` + `UiStrings.cs` — keys `CollapseBrowse`, `ExpandBrowse`

- [ ] **Step 1: Add strings**

- zh: 收起浏览 / 展开浏览  
- en: Collapse browser / Expand browser  
- de/ja/ru: proper short labels  

Expose `Ui.CollapseBrowse` / `Ui.ExpandBrowse`. Optional computed on VM:

```csharp
public string CatalogToggleLabel => CatalogExpanded ? Ui.CollapseBrowse : Ui.ExpandBrowse;
```

Raise on `OnCatalogExpandedChanged`.

- [ ] **Step 2: Restructure XAML**

Replace single Border with Visibility on whole block by:

```xml
<!-- Always visible header -->
<DockPanel Grid.Row="2" Margin="10,0,10,0">
  <Border ... Padding="18,8">
    <DockPanel>
      <TextBlock Text="{Binding Ui.BrowseMods}" ... />
      <Button Command="{Binding ToggleCatalogCommand}"
              Content="{Binding CatalogToggleLabel}"
              ToolTip="{Binding CatalogToggleLabel}"
              Style="{StaticResource GhostButtonStyle}" ... />
    </DockPanel>
  </Border>
</DockPanel>

<!-- Body only -->
<Border Grid.Row="3" ... Visibility="{Binding CatalogExpanded, Converter={StaticResource BoolToVis}}">
  <!-- filters, actions, grid, detail (move collapse glyph button out) -->
</Border>
```

Adjust parent `Grid.RowDefinitions` so library section shifts correctly (insert row or nest: outer Grid row contains DockPanel with header + collapsible body).

**Preferred nest (fewer row renumbers):**

```xml
<Grid Grid.Row="2">
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
  </Grid.RowDefinitions>
  <Border Grid.Row="0"> <!-- always-on header + toggle --> </Border>
  <Border Grid.Row="1" Visibility="{Binding CatalogExpanded,...}"> <!-- body --> </Border>
</Grid>
```

Remove the old `Content="▾"` button.

- [ ] **Step 3: Verify** — collapse via header button; header remains; expand again; top toolbar「Mod 浏览」still toggles.

- [ ] **Step 4: Commit** — skip unless user asked.

---

### Task 6: Icons.xaml + wire primary actions

**Files:**
- Create: `src/MechabellumModManager/Assets/Icons.xaml`
- Modify: `src/MechabellumModManager/App.xaml` (MergedDictionaries)
- Modify: `src/MechabellumModManager/MechabellumModManager.csproj` if page include needed (`Resource` / `Page`)
- Modify: `MainWindow.xaml` (button contents / search adorners)

- [ ] **Step 1: Create Icons.xaml** with at least:

`IconGearGeometry`, `IconSearchGeometry`, `IconRefreshGeometry`, `IconPlusGeometry`, `IconFlagGeometry`, `IconChevronUpGeometry`, `IconChevronDownGeometry`, `IconCheckGeometry`, `IconPlayGeometry`, `IconUploadGeometry`, `IconImportGeometry`

Use simple 16x16-ish Path Data (standard Fluent-like strokes). Example gear (abbreviated — implement full valid mini paths):

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Geometry x:Key="IconGearGeometry">M...,...</Geometry>
  <!-- ... -->
</ResourceDictionary>
```

- [ ] **Step 2: Merge in App.xaml**

```xml
<ResourceDictionary.MergedDictionaries>
  <ResourceDictionary Source="Assets/Icons.xaml" />
</ResourceDictionary.MergedDictionaries>
```

- [ ] **Step 3: Helper content pattern** — for each key button use:

```xml
<StackPanel Orientation="Horizontal">
  <Path Width="14" Height="14" Margin="0,0,6,0"
        Stretch="Uniform" Fill="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"
        Data="{StaticResource IconGearGeometry}" />
  <TextBlock Text="{Binding Ui.Settings}" VerticalAlignment="Center" />
</StackPanel>
```

Wire at minimum: Settings, ApplyProfile, ApplyAndLaunch, BrowseMods (toolbar), Submit/投稿, RefreshCatalog, AddToLibrary, Report (both), Collapse/Expand browse, Import DLL/Zip/Folder, search boxes (DockPanel with Path + TextBox).

- [ ] **Step 4: Visual smoke at 100% and 125% DPI** — icons not clipped; contrast on Accent/Ghost.

- [ ] **Step 5: Commit** — skip unless user asked.

---

### Task 7: Installer five languages + merge config `uiLanguage`

**Files:**
- Modify: `installer/MechabellumModManager.iss`
- Create: `installer/EULA.ru.txt`, `EULA.ja.txt`, `EULA.de.txt`
- Modify: English/Chinese CustomMessages already present — extend ru/ja/de
- Update: `release/v1.0.9/latest.json` notes after rebuild

**Interfaces:**
- `WriteManagerConfigNative(GamePath)` reads existing JSON, sets `gamePath` + `uiLanguage`, preserves other keys when possible

- [ ] **Step 1: Languages section**

```iss
[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"; LicenseFile: "EULA.zh-CN.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "EULA.en.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"; LicenseFile: "EULA.ru.txt"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; LicenseFile: "EULA.ja.txt"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"; LicenseFile: "EULA.de.txt"
```

- [ ] **Step 2: EULA files** — translate from `EULA.en.txt` structure (sections 1–5) into ru/ja/de. Do not leave empty files.

- [ ] **Step 3: CustomMessages** — duplicate every `chinesesimplified.*` / `english.*` key for `russian.` / `japanese.` / `german.` (AppDisplayName, tasks, components, status, game path, errors). Use natural short translations.

- [ ] **Step 4: Rewrite WriteManagerConfigNative merge**

Logic:

1. `ConfigPath := {userappdata}\MechabellumModManager\config.json`
2. `Lang := MapInstallerLanguageToUi()` where ActiveLanguageName → zh-CN/en/ru/ja/de
3. If file exists, `LoadStringFromFile` into string; try extract existing `gamePath` / `launchMode` / `activeProfileId` / `dataRoot` / `uiLanguage` with simple string scans; keep values except overwrite `gamePath` (resolved) and `uiLanguage` (from installer)
4. Write UTF-8 JSON:

```json
{
  "gamePath": "...",
  "launchMode": 0,
  "activeProfileId": "default",
  "dataRoot": null,
  "uiLanguage": "en"
}
```

(Use preserved launchMode/activeProfileId/dataRoot when parsed.)

```pascal
function MapInstallerLanguageToUi: string;
begin
  if ActiveLanguage = 'chinesesimplified' then Result := 'zh-CN'
  else if ActiveLanguage = 'russian' then Result := 'ru'
  else if ActiveLanguage = 'japanese' then Result := 'ja'
  else if ActiveLanguage = 'german' then Result := 'de'
  else Result := 'en';
end;
```

- [ ] **Step 5: Build installer**

```powershell
Set-Location -LiteralPath "D:\gongzuo\钢铁指挥官mod管理器开发"
.\installer\build-installer.ps1
```

Expected: `dist\MechabellumModManager_Setup_v1.0.9.exe` success; copy to `release\v1.0.9\`; update `latest.json` notes to mention UI icons, catalog multi-select, apply fix, 5 installer languages + UiLanguage seed.

- [ ] **Step 6: Manual installer smoke** — language dialog shows 5; pick English → English EULA; after install open `config.json` and confirm `"uiLanguage": "en"`; app UI English on first launch. Repeat spot-check German.

- [ ] **Step 7: Full test suite**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -v n
```

Expected: all PASS.

- [ ] **Step 8: Commit / GitHub release** — only if user explicitly requests.

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| CanApply = dirty only | 1 |
| Ghost↔Accent by dirty (not opacity-only) | 1 |
| Apply success `_notify` | 1 |
| Catalog Extended + checkboxes | 3 |
| Batch 加入本地库 | 3 |
| Catalog detail 举报 + 收起介绍 | 4 |
| Clear + UnselectAll | 2 |
| Labeled collapse outside visibility | 5 |
| Path icons | 6 |
| Installer 5 langs + EULA | 7 |
| Merge config + uiLanguage seed | 7 |
| v1.0.9 package | 7 |

## Placeholder / consistency self-review

- No TBD left; `ActiveLanguage` mapping names match `[Languages] Name` values.
- `CatalogSelectionCount` / `SetCatalogSelection` / `ClearCatalogModSelectionCommand` naming consistent across tasks 2–3.
- `UseApplyAccent` introduced in Task 1 and used for style; icons in Task 6 wrap same button.
- Commit steps globally skipped per user rule unless requested.

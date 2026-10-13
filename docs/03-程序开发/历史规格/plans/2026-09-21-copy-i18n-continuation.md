# Copy / i18n Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Continue five-language copy work: sync friend-overlay catalog locales to 0.3.39, then localize the next highest-visibility manager chrome still hardcoded in Chinese (folder dialogs, default profile name, installer status Describe, startup path log).

**Architecture:** Catalog copy lives in MechabellumMods `catalog.json` locales. Manager strings continue via `Strings*.resx` + `LocalizationService.T` / `UiStrings`. Defer bulk MainViewModel `AppendLog` migration and friend-overlay in-game UI to a later plan (too large for one gate).

**Tech Stack:** JSON catalog, .NET resx, xUnit, `tools/add-resource-key.ps1` (inserts after closing `</data>`)

**Working branch:** manager `fix/p0-manager-chrome-i18n` (working tree; no Commit steps). Mods repo: working tree on `master` for catalog-only edit unless user asks to commit.

## Global Constraints

- Supported cultures: `zh-CN`, `en`, `ru`, `ja`, `de`
- Every new manager key in all five resx; ResourceKeyParityTests must pass
- Do not ship incomplete P0+this work without user commit ask
- friend-overlay in-DLL UI out of scope here

---

### Task 1: friend-overlay catalog locales → 0.3.39

**Files:**
- Modify: `D:/gongzuo/独立工作区/MechabellumMods/catalog.json` (`friend-overlay.locales.de|ja|ru.summary`)

**Target copy** (align with en + zh changelog: IME composition + paste in search):

| Lang | Summary |
|------|---------|
| de | Eigenes Freundes-Overlay mit Suche, Filter, Sortierung und Schnellaktionen. Kampfart-Auswahl beim Einladen, Follower-Tab, angepinnte Freunde und spielgetreue Profilbilder. 0.3.39: Suchfeld unterstuetzt chinesische IME-Eingabe und Einfuegen. |
| ja | 検索・絞り込み・並び替えと行内ショートカット付きの独立フレンドパネル。ルーム外招待で戦闘タイプを選択して自動作成、フォロワータブとフォロー返し、フレンドのピン留め、ゲーム内と同じアバター表示。0.3.39：検索ボックスが中国語IMEの変換入力と貼り付けに対応。 |
| ru | Отдельная панель друзей с поиском, фильтром, сортировкой и быстрыми действиями. Выбор типа боя при приглашении, вкладка подписчиков, закреплённые друзья и аватары как в игре. 0.3.39: поле поиска поддерживает ввод через китайский IME и вставку. |

- [ ] **Step 1: Edit the three summaries** in `catalog.json`
- [ ] **Step 2: Confirm** zh top-level + en already mention 0.3.39; no version field change

---

### Task 2: Manager — folder dialog titles + default profile name

**Files:**
- Modify: `src/MechabellumModManager/App.xaml.cs`
- Modify: `src/MechabellumModManager/Services/ProfileService.cs`
- Modify: `src/MechabellumModManager/Services/InstallConfigSeeder.cs`
- Modify: all five `Strings*.resx`
- Test: extend `tests/MechabellumModManager.Tests/LocalizationServiceTests.cs` or small new test

**Keys:**

| Key | zh-CN | en |
|-----|-------|-----|
| PickGameFolderTitle | 选择 Mechabellum 游戏目录 | Select Mechabellum game folder |
| PickImportFolderTitle | 选择要导入的文件夹 | Select folder to import |
| DefaultProfileName | 默认 | Default |

de/ja/ru: sensible short translations.

- [ ] **Step 1: Add keys** via add-resource-key (anchor `BrowseEllipsis` or `GamePath`)
- [ ] **Step 2: Wire App.xaml.cs** `Title = LocalizationService.T(...)`
- [ ] **Step 3: Wire ProfileService + InstallConfigSeeder** for `"默认"`
- [ ] **Step 4: Test** `LocalizationService.T("PickGameFolderTitle")` under en equals English

---

### Task 3: MelonLoaderInstaller.Describe + startup path AppendLog

**Files:**
- Modify: `src/MechabellumModManager/Services/MelonLoaderInstaller.cs` — reuse existing `StatusKind*` keys
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` — startup “未自动找到游戏目录…”

**Key:** `LogGamePathNotFound` = zh「未自动找到游戏目录。请在「设置」中浏览选择 Mechabellum 安装路径。」 / en equivalent.

- [ ] **Step 1: Add `LogGamePathNotFound` to five resx**
- [ ] **Step 2: Replace hardcoded strings**
- [ ] **Step 3: Run** ResourceKeyParity + UiLanguageSwitch + LocalizationService tests

---

### Task 4: Verification

- [ ] **Step 1:** `dotnet test --filter "ResourceKeyParity|UiLanguageSwitch|LocalizationService"`
- [ ] **Step 2:** Confirm catalog de/ja/ru contain `0.3.39` not `0.3.6`
- [ ] **Step 3:** Report remaining AppendLog CJK count (inventory only; do not migrate all in this plan)

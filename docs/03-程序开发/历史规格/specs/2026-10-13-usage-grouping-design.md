# Mod 列表按用途分栏：赛事专用 / 日常化

**日期：** 2026-10-13

**版本：** 本规格只定稿设计。编码、版本号上调与发版另开任务。

Mod 浏览与 Mod 库两张列表都按用途分成三组：赛事专用 Mod、日常化 Mod、未判定。三组在同一张表里上下排列，各有一行分组标题。

- **赛事专用 Mod**：对对局公平完全没有影响，比赛里允许使用。画面、镜头、观战、回放、操作便利都在这里。
- **日常化 Mod**：会影响对局公平（信息优势、辅助决策、改数值），只在日常对局里用，比赛里不用。
- **未判定**：目录没标、本地也没覆盖。

用途是一条新的、独立的轴，由目录维护者在 `catalog.json` 里标注，本地包可以覆盖。它不从现有内容分类（`category`）推断，也不从 `LogicFrameGrade` 风险扫描推断。

为什么不能推断：同是 `OverlayUI`，`show-grid` 只画格线，`opponent-economy` 显示对手经济；同是 `ReplayDebug`，都是观战回放类，不影响对局；`auto-speed` 挂在 `CombatAssist` 下，实际只改播放速度。`LogicFrameGrade` 判断的是 DLL 有没有 Hook 模拟帧，和「信息优势」这类问题不是一回事。

## 枚举与取值

新增 `Models/ModUsage.cs`：

| 值 | 界面文案 | 含义 | 能否写进 catalog |
| --- | --- | --- | --- |
| `Tournament` | 赛事专用 Mod | 不影响公平，比赛允许 | 能 |
| `Casual` | 日常化 Mod | 影响公平，只在日常用 | 能 |
| `Unrated` | 未判定 | 目录没标、本地没覆盖。只用于展示、筛选和分组 | 不能，与 `Uncategorized` 同一性质 |

`ModTaxonomy` 增加，紧挨着 `ResolveEffectiveCategory`：

- `CatalogWritableUsages`：`[Tournament, Casual]`。
- `TryParseUsage(string?, out ModUsage)`：区分大小写的枚举名；`Unrated`、空白、未知值都返回 `false`。
- `ResolveEffectiveUsage(string? usageOverride, string? catalogUsage)`：覆盖合法用覆盖；否则目录合法用目录；否则 `Unrated`。

枚举的声明顺序就是分组顺序：`Tournament` = 0、`Casual` = 1、`Unrated` = 2。不另设组序函数。

## 数据

### `catalog.json`

每个 mod 条目可选字段 `"usage"`，字符串，只允许 `"Tournament"` 或 `"Casual"`。缺省即未判定。由维护者标注，投稿邮件模板不加这一项，作者不自报。

`CatalogMod`（`ModCatalogService.cs`）增加 `public string? Usage { get; set; }`。目录 enrichment 把它写进 `ModPackage.CatalogUsage`，和 `CatalogCategory` 同一处。

### `package.json`

`ModPackage` 增加：

- `UsageOverride`（持久化，`string?`）：只接受 `Tournament`/`Casual`；null 表示跟随目录。
- `CatalogUsage`（运行时，`[JsonIgnore]`）。

`ModLibraryService` 里读写 `package.json` 的元数据类（现有 `CategoryOverride`、`ExtraTags` 所在处）同步增加 `UsageOverride`。目录刷新与同步不得覆盖或清除它。

### 非法值

- 目录值非空但不合法：按 `Unrated` 处理，写 `LogInvalidCatalogUsage`（和 `LogInvalidCatalogCategory` 同一位置），整份目录照常加载。
- 本地覆盖非空但不合法：按「跟随目录」处理，写 `LogInvalidUsageOverride`，和 `LogInvalidCategoryOverride` 同一位置。
- 都不弹窗，不阻断。

## 界面

两页同样处理：Mod 浏览 `CatalogModsGrid`、Mod 库 `LibraryModsGrid`。精选区（Featured）不分组。Type 列、Risk 列、列宽、多选、详情面板都不动。

### 分组

- `CatalogModsView`、`LibraryModsView` 各加一条 `PropertyGroupDescription("EffectiveUsage")`。
- `CatalogModItemViewModel`、`ModItemViewModel` 各加一个只读属性 `EffectiveUsage`（`ModUsage`）。编辑覆盖后对它发 `PropertyChanged` 并刷新 View，列表立即重分组。
- 两张 DataGrid 的 `GroupStyle` 共用 `Themes/AppTheme.xaml` 里的标题模板 `UsageGroupHeaderTemplate`：标题行左侧是 `AccentBarStyle` 竖条，接组名，再接「(N)」计数，N 是 `CollectionViewGroup.ItemCount`。组键是 `ModUsage` 枚举，组名经 `Converters/ModUsageToLabelConverter.cs` 转成本地化文案，这样切换界面语言时只需刷新 View，不必重建分组。三组都用默认强调色，不按组变色。标题行不可选中、不可勾选。
- 筛选后为空的组由 WPF 自动隐藏，不会出现空标题。
- DataGrid 设 `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`，保住现有行虚拟化。

### 排序与分组顺序

没有额外设置时，WPF 分组的组顺序取决于排序后第一个出现的元素。两张表的排序方式又不同：Mod 浏览用 `ListCollectionView.CustomSort`（`ApplySort` 里的比较器），Mod 库用 `SortDescriptions` 并允许点列头排序。

因此组顺序不靠行排序，而是由分组描述自己排：`PropertyGroupDescription.SortDescriptions` 加一条 `new SortDescription("Name", Ascending)`，组的 `Name` 是 `ModUsage` 枚举值，按声明顺序排。

- `ApplySort`、`CustomSort` 比较器、Mod 库的 `SortDescriptions` 都不改。
- 不处理 `DataGrid.Sorting`。点任何列头、切换任何排序，三组顺序都不变，只在组内排序。
- 列头的排序箭头照旧。

### 筛选栏

- Mod 浏览：在「分类」左侧增加「用途」下拉。Mod 库的工具栏目前只有搜索框，没有分类、标签下拉：在搜索框右侧加「用途」下拉。宽度都是 140，用现有 `FilterFieldLabelStyle` 与 `FilterOptionLabelTemplate`。选项：全部、赛事专用 Mod、日常化 Mod、未判定。
- 新增 `UsageFilterOption(ModUsage? Usage, string Label)`，放进 `ViewModels/FilterOptions.cs`，和 `CategoryFilterOption` 同一写法。null 表示全部。
- `ModListFilter.MatchesUsage(ModUsage? filter, ModUsage effective)`：filter 为 null 放行，否则相等才通过。
- `FilterCatalogItem`、`FilterLibraryItem` 各加这一个判断。筛选仍是 AND：搜索 ∩ 用途 ∩ 分类 ∩ 标签。
- 会话内记住选择，不跨启动持久化，和分类、标签一致。

### 本地覆盖

`EditModTaxonomyDialog` 在「分类」下方加一个「用途」下拉：跟随目录、赛事专用 Mod、日常化 Mod。`Result` 元组多一项 `UsageOverride`，沿现有路径写入：`App.xaml.cs` 的 `PromptEditTaxonomy` → `MainViewModel` 的编辑命令 → `ModLibraryService` 保存 `package.json`。

## 文案

| 键 | zh-CN（`Strings.resx`） | en | de | ja | ru |
| --- | --- | --- | --- | --- | --- |
| `UsageTournament` | 赛事专用 Mod | Tournament mods | Turnier-Mods | 大会用 Mod | Турнирные моды |
| `UsageCasual` | 日常化 Mod | Casual mods | Alltags-Mods | 日常用 Mod | Повседневные моды |
| `UsageUnrated` | 未判定 | Not rated | Nicht bewertet | 未判定 | Не оценено |
| `FilterUsage` | 用途 | Usage | Verwendung | 用途 | Назначение |
| `UsageFollowCatalog` | 跟随目录 | Follow catalog | Katalog folgen | カタログに従う | Как в каталоге |
| `LogInvalidCatalogUsage` | 目录条目 '{0}'：无效用途 '{1}'，按未判定处理。 | Catalog entry '{0}': invalid usage '{1}'; treating as not rated. | Katalogeintrag '{0}': ungültige Verwendung '{1}'; als nicht bewertet behandelt. | カタログ項目 '{0}'：無効な用途 '{1}'。未判定として処理。 | Запись каталога '{0}': неверное назначение '{1}'; не оценено. |
| `LogInvalidUsageOverride` | 本地包 '{0}'：无效用途覆盖 '{1}'，按目录/未判定处理。 | Local package '{0}': invalid usage override '{1}'; using catalog / not rated. | Lokales Paket '{0}': ungültige Verwendungs-Überschreibung '{1}'; Katalog / nicht bewertet wird verwendet. | ローカルパッケージ '{0}'：用途の上書き '{1}' が無効です。カタログ／未判定として扱います。 | Локальный пакет '{0}': недопустимое переопределение назначения '{1}'; используется каталог / не оценено. |

五个 resx 用 `tools/add-resource-key.ps1` 一起加。`UiStrings` 增加对应访问器和 `UsageLabel(ModUsage)`。

## 社区目录仓库

MechabellumMods 仓库（本机 `D:\gongzuo\钢铁指挥官\社区目录`）独立改动，先于管理器发版推送：

- `scripts/validate_catalog.py`：在 `category` 校验旁加 `usage`。缺省通过；存在时必须是 `"Tournament"` 或 `"Casual"`，否则报 `invalid usage`。`test_validate_catalog.py` 加合法、非法、缺省三条用例。
- `README.md` 的 catalog 字段表加一行 `usage`，说明由维护者标注。
- `catalog.json` 首批标注如下。待定的先不写，进「未判定」，管理器不会出错。只前进目录根上的 `updatedAt`，不改任何 `sha256`、`size`、`parts`。

| 写 `Tournament` | 写 `Casual` | 待定，先不写 |
| --- | --- | --- |
| show-grid、quick-camera、quick-mark、replay-tool、replay-reset、friend-overlay、cut-guide、leaderboard-fix、auto-speed | opponent-economy、predict-position | quick-item、undo-plus、damage-rank、show-dps、sales-calculation、missile-direction、battle-suite、team-beacon、unit-ledger |

管理器仓库 `tools/import_inbox.py` 不生成这个字段；维护者审核时手填。

## 测试

纯函数与服务，不靠点界面：

- `ModTaxonomy.TryParseUsage`：`Tournament`、`Casual` 通过；`Unrated`、`tournament`、空、空白、未知值拒绝。
- `ModTaxonomy.ResolveEffectiveUsage`：覆盖合法则用覆盖，无视目录；覆盖非法、目录合法则用目录；都无或都非法则 `Unrated`。
- `ModUsage` 声明顺序：`Tournament` < `Casual` < `Unrated`。
- `ModListFilter.MatchesUsage`：null 放行；相等通过；不等拒绝。
- `ModLibraryService`：写入 `usageOverride` 后重载仍在；目录 enrichment 不清掉它。
- 目录解析：含 `usage` 的条目读进 `CatalogMod.Usage`；非法值按 `Unrated` 显示，整份目录照常加载。
- `MainViewModel`：两个 View 各有且只有一条 `GroupDescription`；名称排序、更新时间排序、列头式 `SortDescriptions` 三种情况下组顺序都是 `Tournament`、`Casual`、`Unrated`；「用途」筛选只留对应一组。
- 编辑命令：注入的编辑回调返回用途后，`UsageOverride` 写进 `package.json`，该行 `EffectiveUsage` 随之改变。
- 主窗口：两张 DataGrid 各有一个 `GroupStyle`，且开着 `IsVirtualizingWhenGrouping`。

## 不做

- 不从 `category` 或 `LogicFrameGrade` 推断用途。
- 不改 `LogicFrameGrade` 风险列的语义和颜色。
- 不加「用途」列；分组标题已经承担这个信息。
- 不在投稿邮件模板里让作者自报用途。
- 不做三组之间的拖拽移动；改组只通过编辑对话框。
- 不做跨启动的筛选持久化。
- 本规格不实现功能、不打包、不上调版本号。

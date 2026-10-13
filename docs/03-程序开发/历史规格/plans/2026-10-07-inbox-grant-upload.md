# 白名单投递区 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 作者在现有「白名单直传」窗口里填写邀请码和 Mod 文件，文件只进入私有投递桶的指定前缀；作者电脑上不留下腾讯云长期密钥，玩家在维护者合并进 GitHub 目录并同步镜像之前看不到这个 Mod。

**Architecture:** 已有的 `services/mod-publish-worker` 继续校验邀请码。它不再接收 DLL 正文，也不再写 GitHub。校验通过后，它用投递桶子账号调用 STS `GetFederationToken`，发一张 3600 秒的临时凭证，资源只覆盖 `inbox/<inviteId>/<modId>/*`。管理器用这张凭证和一份用完即删的 coscli 配置上传。预期哈希在签发时写入 KV。维护者的导入脚本按 KV 记录下载并重新计算哈希，通过后才写入本地 `MechabellumMods` 工作副本。玩家客户端的下载地址不变。

**Tech Stack:** Cloudflare Worker（现有 Vitest）、腾讯云 STS / COS、C# / xUnit、Python 3 导入脚本、coscli `--config-path`。

**Working branch:** none (working tree only, no commits)

## Global Constraints

- 作者窗口不出现 SecretId、SecretKey，不调用 `CoscliUserConfig.Save`，不读写 `%USERPROFILE%\.cos.yaml`。
- 临时凭证时长 `DurationSeconds = 3600`。调用方必须是子账号；不要用主账号密钥。子账号上限是 129600 秒，3600 在范围内。
- STS `Name` 固定为 `inboxgrant`（只含字母）。`Policy` 字符串做一次 `encodeURIComponent`，不要做两次。
- 投递桶名 `mmm-inbox-1312774738`，地域 `ap-shanghai`，权限私有读写。不要用现有的公有读桶 `mmm-mirror-1312774738`。桶名不得包含 `github`。
- 对象键前缀是 `inbox/<inviteId>/<modId>/`。`inviteId` 来自 KV，并且必须匹配 `^[a-z0-9][a-z0-9._-]{0,63}$`。不得把邀请码明文放进对象键。`buildInboxPolicy` 对不合格的 `inviteId` 或 `modId` 抛出 `unsafe id`。
- 新 Mod id 必须匹配 `^[a-z0-9][a-z0-9._-]{0,63}$`。
- 单个文件大小是 1 到 `104857599` 字节（`100 * 1024 * 1024 - 1`，与 `validate_catalog.py` 的 `LARGE_FILE_BYTES` 一致）。预览图只允许 `preview.png`，最大 `8388608` 字节。一次申请最多 20 个文件，合计最大 `536870912` 字节。
- `kind` 只能是 `single`、`parts`、`bundle` 之一。`parts` 与 `bundle` 不能同时出现。
- 临时凭证的 action 只有 `cos:PutObject`、`cos:HeadObject`、`cos:InitiateMultipartUpload`、`cos:ListParts`、`cos:UploadPart`、`cos:CompleteMultipartUpload`、`cos:AbortMultipartUpload`。没有 `cos:DeleteObject`、`cos:GetObject`、`cos:GetBucket`。
- 作者提交里的 `minManagerVersion` 和 `originUrl` 不进入目录。导入脚本只在命令行显式传入合法 `--floor` 时才写 `minManagerVersion`。
- `/v1/mods/publish` 改为返回 410 和 `{"error":"publish_disabled"}`。不要再把 DLL 经 Worker 写入 GitHub。
- 玩家客户端不新增投递桶地址。`sync-mirror.ps1` 不接收这个桶。
- 上传成功的界面文字必须说明玩家还看不到。
- 不修改镜像桶的现有 CAM 策略，不删除镜像上的整包 DLL，不打包安装程序，不推送。
- 五份 `Strings.*.resx` 同时改同一批键。

## 手工准备（不写进仓库）

1. 新建桶 `mmm-inbox-1312774738`，地域上海，访问权限选私有读写。
2. 生命周期：前缀 `inbox/` 的对象 14 天后删除；未完成的分片上传 1 天后删除。
3. 新建只允许编程访问的子用户，自定义策略只包含上面列出的 action，再加维护者自己要用的 `cos:GetObject` 和 `cos:DeleteObject`。资源只有 `qcs::cos:ap-shanghai:uid/1312774738:mmm-inbox-1312774738/inbox/*`。不要加桶根资源，不要加镜像桶。
4. 临时凭证由 STS 再收窄，作者那张凭证没有 Get 和 Delete。维护者导入用的长期密钥仍是这一个子用户，放在仓库外的配置文件里。
5. Worker 密钥只放在 `wrangler secret`：`COS_SECRET_ID`、`COS_SECRET_KEY`、`ADMIN_TOKEN`。不要配置 `GITHUB_TOKEN`。邀请码仍按现有 README 以 SHA-256 写入 `invite:<hash>`。

---

### Task 1: 投递授权与临时策略

**Files:**
- Modify: `services/mod-publish-worker/src/ownership.ts`
- Modify: `services/mod-publish-worker/src/ownership.test.ts`
- Create: `services/mod-publish-worker/src/inboxPolicy.ts`
- Create: `services/mod-publish-worker/src/inboxPolicy.test.ts`

**Interfaces:**
- Consumes: 现有 `authorizePublish` 保持原样，供旧测试使用。
- Produces: `authorizeInbox({ catalogHasMod, ownerInviteId, inviteId }): "allow" | "forbidden_not_owner"`。`buildInboxPolicy(inviteId, modId): { policy: CamPolicy, encoded: string, resource: string }`。`CamPolicy` 为 `{ version: "2.0"; statement: [{ effect: "allow"; action: string[]; resource: string[] }] }`。

- [ ] **Step 1: Write the failing test**

在 `ownership.test.ts` 末尾增加：

```typescript
import { authorizeInbox } from "./ownership";

describe("authorizeInbox", () => {
  it("rejects when another invite already owns the id even if the catalog entry is gone", () => {
    expect(authorizeInbox({
      catalogHasMod: false,
      ownerInviteId: "alice",
      inviteId: "bob",
    })).toBe("forbidden_not_owner");
  });

  it("rejects an existing catalog id that has no owner", () => {
    expect(authorizeInbox({
      catalogHasMod: true,
      ownerInviteId: null,
      inviteId: "alice",
    })).toBe("forbidden_not_owner");
  });

  it("allows the recorded owner to submit again", () => {
    expect(authorizeInbox({
      catalogHasMod: true,
      ownerInviteId: "alice",
      inviteId: "alice",
    })).toBe("allow");
  });

  it("allows a new id with no owner", () => {
    expect(authorizeInbox({
      catalogHasMod: false,
      ownerInviteId: null,
      inviteId: "alice",
    })).toBe("allow");
  });
});
```

在 `inboxPolicy.test.ts` 写：

```typescript
import { describe, expect, it } from "vitest";
import { buildInboxPolicy } from "./inboxPolicy";

describe("buildInboxPolicy", () => {
  it("scopes put and multipart to one mod prefix and url-encodes once", () => {
    const built = buildInboxPolicy("author1", "quick-mark");
    expect(built.resource).toBe(
      "qcs::cos:ap-shanghai:uid/1312774738:mmm-inbox-1312774738/inbox/author1/quick-mark/*",
    );
    expect(built.policy.statement[0].action).toEqual([
      "cos:PutObject",
      "cos:HeadObject",
      "cos:InitiateMultipartUpload",
      "cos:ListParts",
      "cos:UploadPart",
      "cos:CompleteMultipartUpload",
      "cos:AbortMultipartUpload",
    ]);
    expect(built.encoded.startsWith("%7B")).toBe(true);
    expect(built.encoded.includes("%257B")).toBe(false);
    expect(built.encoded.length).toBeLessThan(2048);
    expect(JSON.stringify(built.policy)).not.toContain("DeleteObject");
    expect(JSON.stringify(built.policy)).not.toContain("GetBucket");
    expect(JSON.stringify(built.policy)).not.toContain("principal");
  });

  it("refuses an invite id that could widen the prefix", () => {
    expect(() => buildInboxPolicy("a/b", "quick-mark")).toThrow(/unsafe id/);
    expect(() => buildInboxPolicy("author1", "..")).toThrow(/unsafe id/);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test` from `services/mod-publish-worker`

Expected: FAIL because `authorizeInbox` and `buildInboxPolicy` are not exported.

- [ ] **Step 3: Write minimal implementation**

`ownership.ts` 增加：

```typescript
export function authorizeInbox(args: {
  catalogHasMod: boolean;
  ownerInviteId: string | null;
  inviteId: string;
}): "allow" | "forbidden_not_owner" {
  if (args.ownerInviteId != null && args.ownerInviteId !== args.inviteId)
    return "forbidden_not_owner";
  if (args.catalogHasMod && args.ownerInviteId !== args.inviteId)
    return "forbidden_not_owner";
  return "allow";
}
```

`inboxPolicy.ts`：

```typescript
const ACTIONS = [
  "cos:PutObject",
  "cos:HeadObject",
  "cos:InitiateMultipartUpload",
  "cos:ListParts",
  "cos:UploadPart",
  "cos:CompleteMultipartUpload",
  "cos:AbortMultipartUpload",
] as const;

export interface CamPolicy {
  version: "2.0";
  statement: [{ effect: "allow"; action: string[]; resource: string[] }];
}

const SAFE_ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;

export function buildInboxPolicy(inviteId: string, modId: string) {
  if (!SAFE_ID.test(inviteId) || !SAFE_ID.test(modId))
    throw new Error("unsafe id");
  const resource =
    `qcs::cos:ap-shanghai:uid/1312774738:mmm-inbox-1312774738/inbox/${inviteId}/${modId}/*`;
  const policy: CamPolicy = {
    version: "2.0",
    statement: [{ effect: "allow", action: [...ACTIONS], resource: [resource] }],
  };
  return { policy, encoded: encodeURIComponent(JSON.stringify(policy)), resource };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test` from `services/mod-publish-worker`

Expected: PASS，原有 `authorizePublish` 四个测试仍然通过。

---

### Task 2: 申请体校验

**Files:**
- Create: `services/mod-publish-worker/src/inboxGrant.ts`
- Create: `services/mod-publish-worker/src/inboxGrant.test.ts`

**Interfaces:**
- Consumes: 无
- Produces: `inspectGrant(body: unknown): { ok: true, value: GrantRequest } | { ok: false, error: "validation_failed" | "payload_too_large" }`。`GrantRequest` 含 `id, name, summary, version, kind, files`。`assignKeys(inviteId, req): { key, role, size, sha256 }[]`。

文件角色：`single` 恰好一个 `role: "dll"`，名字匹配 `^[A-Za-z0-9._-]+\.dll$`，可选一个 `role: "preview"`。`parts` 的 `role: "part"` 名字是从 `0000` 连续递增的四位数字，至少一块，可选预览。`bundle` 至少两个 `role: "bundle"`，恰好一个 `main: true`，每个名字匹配 `^[A-Za-z0-9._-]+\.dll$`，可选预览。预览的名字必须是 `preview.png`。`sha256` 是 64 位小写十六进制。`name` 1 到 80 字符且不含换行。`summary` 空或不超过 400 字符。`version` 空或不超过 32 字符。对象键由服务端分配：dll 用 `inbox/<inviteId>/<id>/<文件名>`，分块用 `inbox/<inviteId>/<id>/parts/<四位>`，资源包用 `inbox/<inviteId>/<id>/payload/<文件名>`，预览用 `inbox/<inviteId>/<id>/preview.png`。

- [ ] **Step 1: Write the failing test**

```typescript
import { describe, expect, it } from "vitest";
import { assignKeys, inspectGrant } from "./inboxGrant";

const sha = "a".repeat(64);

describe("inspectGrant", () => {
  it("rejects a file at the git limit", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "single",
      files: [{ role: "dll", name: "QuickMark.dll", size: 100 * 1024 * 1024, sha256: sha }],
    });
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toBe("payload_too_large");
  });

  it("rejects parts and a bundle file together", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "parts",
      files: [
        { role: "part", name: "0000", size: 10, sha256: sha },
        { role: "bundle", name: "Other.dll", size: 10, sha256: sha, main: true },
      ],
    });
    expect(result.ok).toBe(false);
  });

  it("rejects an uppercase mod id", () => {
    const result = inspectGrant({
      id: "QuickMark",
      name: "Quick Mark",
      kind: "single",
      files: [{ role: "dll", name: "QuickMark.dll", size: 10, sha256: sha }],
    });
    expect(result.ok).toBe(false);
  });

  it("assigns server keys for a contiguous part list", () => {
    const result = inspectGrant({
      id: "quick-mark",
      name: "Quick Mark",
      kind: "parts",
      files: [
        { role: "part", name: "0000", size: 10, sha256: sha },
        { role: "part", name: "0001", size: 12, sha256: sha },
      ],
    });
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(assignKeys("author1", result.value).map((item) => item.key)).toEqual([
      "inbox/author1/quick-mark/parts/0000",
      "inbox/author1/quick-mark/parts/0001",
    ]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/inboxGrant.test.ts` from `services/mod-publish-worker`

Expected: FAIL，模块不存在。

- [ ] **Step 3: Write minimal implementation**

`inspectGrant` 先判断 `kind`，再按角色过滤。出现不属于该 kind 的角色就返回 `validation_failed`。大小不在 1 到 104857599 内、预览大于 8388608、文件数大于 20、合计大于 536870912，返回 `payload_too_large`。分块名字排序后必须等于 `0000` 起的连续整数。资源包必须恰好一个 `main === true`。`assignKeys` 只使用校验后的文件名，不使用客户端传来的路径。

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/inboxGrant.test.ts` from `services/mod-publish-worker`

Expected: PASS

---

### Task 3: Worker 接口改为签发凭证

**Files:**
- Modify: `services/mod-publish-worker/src/index.ts`
- Create: `services/mod-publish-worker/src/inboxHttp.test.ts`
- Modify: `services/mod-publish-worker/README.md`

**Interfaces:**
- Consumes: `resolveInvite`、`authorizeInbox`、`inspectGrant`、`assignKeys`、`buildInboxPolicy`。`CredentialIssuer = (encodedPolicy: string) => Promise<{ tmpSecretId: string; tmpSecretKey: string; sessionToken: string }>`。
- Produces: `POST /v1/inbox/grant` 返回 `{ ok, inviteId, bucket, region, durationSeconds: 3600, credentials, objects }`。`POST /v1/inbox/complete` 只把已存在的 grant 标成 `complete-claimed`。`GET /v1/admin/grants` 需要 `Authorization: Bearer <ADMIN_TOKEN>`。`POST /v1/mods/publish` 返回 410。

目录是否已有该 id，用匿名 GET `https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json` 判断。请求失败时返回 502 `upstream_github`，不要当成新 id 放行。KV 记录键为 `grant:<inviteId>:<modId>`，值包含申请时的对象列表、sha256、size、kind、name、summary、version，不含邀请码，不含临时密钥。

- [ ] **Step 1: Write the failing test**

用内存 KV 和固定 issuer 调用 `default.fetch`。断言：

- 无主的已有目录 id 返回 403 `forbidden_not_owner`。
- 合法新 id 的响应里 `durationSeconds` 为 3600，对象键只有该前缀。假 issuer 返回的 `tmpSecretKey` 是 `tmp-secret`。写入 KV 的文本不包含 `tmp-secret`，也不包含邀请码明文。生产代码的状态栏和日志同样不写临时密钥。
- `POST /v1/mods/publish` 返回 410，且 issuer 未被调用。
- 没有 `ADMIN_TOKEN` 匹配时，`GET /v1/admin/grants` 返回 401。

issuer 记录收到的 `encodedPolicy`，断言它以 `%7B` 开头。

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/inboxHttp.test.ts` from `services/mod-publish-worker`

Expected: FAIL，路由不存在。

- [ ] **Step 3: Write minimal implementation**

`handlePublish` 的函数体改为 `return err("publish_disabled", 410)`。grant 处理顺序：解析邀请码、`inspectGrant`、拉 GitHub 目录、读 `owner:<id>`、`authorizeInbox`、写 KV、调用 issuer、返回凭证。complete 只更新状态，不接受新的哈希。admin 列表只返回 KV 里的 grant 记录。STS 调用放在 `src/sts.ts` 的默认 issuer：POST `https://sts.tencentcloudapi.com`，头 `X-TC-Action: GetFederationToken`、`X-TC-Version: 2018-08-13`、`X-TC-Region: ap-shanghai`，正文：

```json
{"Name":"inboxgrant","Policy":"<一次 encodeURIComponent 的策略>","DurationSeconds":3600}
```

签名使用腾讯云 API 3.0 TC3-HMAC-SHA256。单元测试注入假 issuer，不访问外网。

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test` from `services/mod-publish-worker`

Expected: PASS

README 的 API 表改成 grant、complete、admin。写明 `/v1/mods/publish` 已关闭，DLL 不经过 Worker。

---

### Task 4: 管理器申请凭证并隔离 coscli 配置

**Files:**
- Create: `src/MechabellumModManager/Services/InboxGrantClient.cs`
- Create: `src/MechabellumModManager/Services/InboxCoscliSession.cs`
- Modify: `src/MechabellumModManager/Services/DirectUploadDefaults.cs`（只加注释，`ApiBaseUrl` 仍保持 `""`）
- Test: `tests/MechabellumModManager.Tests/InboxGrantClientTests.cs`
- Test: `tests/MechabellumModManager.Tests/InboxCoscliSessionTests.cs`

**Interfaces:**
- Consumes: Worker 的 grant / complete JSON。
- Produces: `InboxGrantClient.RequestAsync(inviteCode, payload, ct)` 和 `CompleteAsync(inviteCode, id, ct)`。非 https 的基址返回 `NotConfigured`。`InboxCoscliSession.Write(path, secretId, secretKey, sessionToken, bucket, region)` 写出完整 yaml，含 `sessiontoken`。`RunAsync` 使用 `coscli -c <临时文件> cp <本地> cos://<桶>/<键> --meta x-cos-meta-sha256:<哈希>`，`finally` 删除该文件。

- [ ] **Step 1: Write the failing test**

`InboxGrantClientTests`：基址 `http://example.test` 时不发出请求，错误码 `NotConfigured`。基址 `https://example.test` 时 POST `/v1/inbox/grant`，把响应里的 `objects[0].key` 交回调用方。异常路径的返回值 `Detail` 不得包含 `tmpSecretKey` 的值；解析失败时丢弃正文。

`InboxCoscliSessionTests`：先写一个假的 `%USERPROFILE%\.cos.yaml` 内容 `secretid: "KEEP"`。`Write` 写到另一个临时路径。断言临时文件含 `sessiontoken: "TOKEN"`，用户目录那份仍是 `KEEP`。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~InboxGrantClientTests|FullyQualifiedName~InboxCoscliSessionTests"`

Expected: FAIL，类型不存在。

- [ ] **Step 3: Write minimal implementation**

`InboxGrantClient` 只用 `System.Text.Json` 读取 `tmpSecretId`、`tmpSecretKey`、`sessionToken`、`objects`。状态行和异常信息只使用 `DirectUploadErrorCode`，不拼接响应正文。`InboxCoscliSession.Write` 始终覆盖指定路径的完整文件，不读取、不合并已有 yaml。上传进程的参数列表包含 `-c` 和临时路径。退出码非 0 时抛出现有的 `MirrorPublishException(Network)`，消息用 coscli 的 stderr，但先删掉其中的 `sessiontoken` 行。

- [ ] **Step 4: Run test to verify it passes**

Run 与 Step 2 相同。

Expected: PASS

---

### Task 5: 窗口只投递，不写总目录

**Files:**
- Modify: `src/MechabellumModManager/Dialogs/DirectUploadDialog.xaml`
- Modify: `src/MechabellumModManager/Dialogs/DirectUploadDialog.xaml.cs`
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`
- Modify: `src/MechabellumModManager/Resources/Strings.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.en.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.de.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ja.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ru.resx`
- Create: `src/MechabellumModManager/Services/InboxSubmission.cs`
- Test: `tests/MechabellumModManager.Tests/InboxSubmissionTests.cs`
- Test: `tests/MechabellumModManager.Tests/DirectUploadDialogSourceTests.cs`

**Interfaces:**
- Consumes: `inspect` 规则在 C# 侧再做一遍，错误码与 Worker 相同。`InboxSubmission.Build(kind, id, name, summary, version, files)` 返回 grant 请求体。`files` 元素为 `{ Role, Name, FullPath, Size, Sha256, Main }`。
- Produces: 窗口保存按钮只写 `AppConfig.AuthorInviteCode`。发布按钮调用 `Build`、`RequestAsync`、逐个 `RunAsync`、`CompleteAsync`。

文案：

| 键 | zh | en | de | ja | ru |
|---|---|---|---|---|---|
| DirectUploadTitle | 投递 Mod | Submit a mod | Mod einreichen | Mod を提出 | Отправить мод |
| DirectUploadKind | 文件形式 | File layout | Dateiform | ファイル形式 | Вид файлов |
| DirectUploadKindSingle | 单个 DLL | Single DLL | Einzelne DLL | 単一 DLL | Один DLL |
| DirectUploadKindParts | 分块 | Parts | Teile | 分割 | Части |
| DirectUploadKindBundle | 资源包 | Bundle | Paket | リソースパック | Набор |
| DirectUploadWaiting | 已传到投递区。玩家还看不到。维护者合并进目录并同步镜像之后才会出现。 | Uploaded to the intake area. Players cannot see it yet. It appears after the maintainer merges it into the catalog and syncs the mirror. | In den Eingangsbereich hochgeladen. Spieler sehen es noch nicht. Es erscheint, nachdem der Betreuer es in den Katalog übernommen und den Spiegel abgeglichen hat. | 受付領域へアップロードしました。プレイヤーにはまだ表示されません。管理者がカタログへ取り込み、ミラーを同期したあと表示されます。 | Загружено в зону приёма. Игроки пока её не видят. Она появится после того, как сопровождающий добавит её в каталог и синхронизирует зеркало. |

- [ ] **Step 1: Write the failing test**

`InboxSubmissionTests` 断言：单个 DLL 生成一个 `dll` 角色；分块缺 `0001` 时失败；资源包两个主文件时失败；文件长度为 `100 * 1024 * 1024` 时失败。哈希用流式 `SHA256`，不要把整个文件读进一个字节数组。

`DirectUploadDialogSourceTests` 读取这两个源文件的文本，断言不包含 `SecretKeyBox`、`SecretIdBox`、`UpdateCatalogBox`、`MergeCatalog_Click`、`CoscliUserConfig.Save`、`CatalogKey`。

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~InboxSubmissionTests|FullyQualifiedName~DirectUploadDialogSourceTests"`

Expected: FAIL。源文件守卫会先失败，因为现有窗口仍包含这些名字。

- [ ] **Step 3: Write minimal implementation**

从 XAML 删除 SecretId、SecretKey、密钥说明、`UpdateCatalogBox`、`MergeButton`。加上 `ComboBox x:Name="KindBox"` 和 `ListBox x:Name="FileList"`，以及「添加文件」按钮。`Publish_Click` 不再下载或上传 `catalog.json`。成功对话框只显示 `DirectUploadWaiting`。`ApiBaseUrl` 为空时状态文字仍用 `DirectUploadNotConfigured`。关闭窗口时取消 `CancellationTokenSource` 并删除临时 coscli 配置。

- [ ] **Step 4: Run test to verify it passes**

Run 与 Step 2 相同。

Expected: PASS

---

### Task 6: 导入脚本只信任重新计算的哈希

**Files:**
- Create: `tools/import_inbox.py`
- Create: `tools/test_import_inbox.py`

**Interfaces:**
- Consumes: admin grant JSON：`inviteId, id, kind, name, summary, version, objects[{key, role, size, sha256, main}]`。本地已下载文件，相对路径等于 `key` 去掉 `inbox/<inviteId>/<id>/` 前缀。
- Produces: `import_grant(grant, download_root, catalog, floor, claim) -> dict`。成功时返回要合并的目录条目，不写磁盘。失败时抛 `ImportError`，消息以 `hash mismatch`、`missing file`、`path escape`、`existing id`、`bad floor` 之一开头。`assert_isolated_config(path)` 在路径等于用户目录 `.cos.yaml` 时抛 `ImportError`。

- [ ] **Step 1: Write the failing test**

```python
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import import_inbox


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def write_object(root: Path, relative: str, payload: bytes) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


class ImportInboxTests(unittest.TestCase):
    def test_hash_mismatch_is_refused(self):
        payload = b"dll-bytes"
        digest = sha256_bytes(payload)
        grant = single_grant(digest)
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", b"tampered!")
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(grant, Path(tmp), {"mods": []}, None, claim=False)
        self.assertTrue(str(raised.exception).startswith("hash mismatch"))

    def test_author_floor_and_origin_are_ignored(self):
        payload = b"dll-bytes"
        digest = sha256_bytes(payload)
        grant = single_grant(digest)
        grant["minManagerVersion"] = "9.9.9"
        grant["originUrl"] = "https://evil.example/mod.dll"
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", payload)
            entry = import_inbox.import_grant(grant, Path(tmp), {"mods": []}, "1.3.16", claim=False)
            self.assertNotIn("originUrl", entry)
            self.assertEqual(entry["minManagerVersion"], "1.3.16")
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(grant, Path(tmp), {"mods": []}, "01.3.16", claim=False)
        self.assertTrue(str(raised.exception).startswith("bad floor"))

    def test_existing_catalog_id_without_claim_is_refused(self):
        payload = b"dll-bytes"
        grant = single_grant(sha256_bytes(payload))
        with tempfile.TemporaryDirectory() as tmp:
            write_object(Path(tmp), "QuickMark.dll", payload)
            with self.assertRaises(ImportError) as raised:
                import_inbox.import_grant(
                    grant, Path(tmp), {"mods": [{"id": "quick-mark"}]}, None, claim=False)
        self.assertTrue(str(raised.exception).startswith("existing id"))

    def test_parts_hash_is_the_joined_file(self):
        first, second = b"aaaa", b"bbbb"
        grant = {
            "inviteId": "author1",
            "id": "quick-mark",
            "kind": "parts",
            "name": "Quick Mark",
            "objects": [
                {"key": "inbox/author1/quick-mark/parts/0000", "role": "part",
                 "size": 4, "sha256": sha256_bytes(first)},
                {"key": "inbox/author1/quick-mark/parts/0001", "role": "part",
                 "size": 4, "sha256": sha256_bytes(second)},
            ],
        }
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_object(root, "parts/0000", first)
            write_object(root, "parts/0001", second)
            entry = import_inbox.import_grant(grant, root, {"mods": []}, None, claim=False)
        self.assertEqual(entry["sha256"], sha256_bytes(first + second))
        self.assertEqual(entry["size"], 8)
        self.assertEqual(entry["parts"][0]["file"], "mods/quick-mark/parts/0000")
        self.assertNotIn("originUrl", entry)

    def test_bundle_hash_is_the_main_file_only(self):
        main, extra = b"main-dll", b"extra-dll"
        grant = {
            "inviteId": "author1",
            "id": "quick-mark",
            "kind": "bundle",
            "name": "Quick Mark",
            "objects": [
                {"key": "inbox/author1/quick-mark/payload/QuickMark.dll", "role": "bundle",
                 "name": "QuickMark.dll", "size": len(main), "sha256": sha256_bytes(main), "main": True},
                {"key": "inbox/author1/quick-mark/payload/Extra.dll", "role": "bundle",
                 "name": "Extra.dll", "size": len(extra), "sha256": sha256_bytes(extra), "main": False},
            ],
        }
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_object(root, "payload/QuickMark.dll", main)
            write_object(root, "payload/Extra.dll", extra)
            entry = import_inbox.import_grant(grant, root, {"mods": []}, None, claim=False)
        self.assertEqual(entry["sha256"], sha256_bytes(main))
        self.assertEqual(entry["size"], len(main))

    def test_user_profile_cos_yaml_is_refused(self):
        home = Path.home() / ".cos.yaml"
        with self.assertRaises(ImportError):
            import_inbox.assert_isolated_config(home)


def single_grant(digest: str) -> dict:
    return {
        "inviteId": "author1",
        "id": "quick-mark",
        "kind": "single",
        "name": "Quick Mark",
        "objects": [{
            "key": "inbox/author1/quick-mark/QuickMark.dll",
            "role": "dll",
            "size": 9,
            "sha256": digest,
        }],
    }
```

`import_grant` 只返回目录条目，不写磁盘。缺文件时错误以 `missing file` 开头。字节不符时以 `hash mismatch` 开头。分块的 `sha256` 是按 `0000`、`0001` 顺序拼接后的哈希。资源包的 `sha256` 和 `size` 只取 `main` 为真的那个文件。

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m unittest discover -s tools -p test_import_inbox.py` from the manager repo root.

Expected: FAIL，模块不存在。

- [ ] **Step 3: Write minimal implementation**

`import_grant(grant, download_root, catalog, floor, claim)` 不写文件、不调用 git、不调用 coscli。下载根用 `Path.resolve()` 之后，对象的相对路径必须仍落在这个根里面，否则抛 `path escape`。哈希分块读取。`floor is None` 时条目不带 `minManagerVersion`。合法 floor 是三段数字、无前导零、每段 0 到 65535。申请 JSON 里的 `minManagerVersion` 和 `originUrl` 不读取。目录已有该 id 且 `claim` 为假时抛 `existing id`。`claim` 为真时仍要求调用者已经在 KV 写过 `owner:<id>`；脚本本身不写 KV，只放行本地条目覆盖。

命令行下载使用调用者传入的 `--config`。`assert_isolated_config` 在该路径等于 `Path.home() / ".cos.yaml"` 时抛 `ImportError`。下载命令是：

```text
coscli -c <config> cp cos://mmm-inbox-1312774738/<object key> <download root 下的相对路径>
```

只下载 grant 的 `objects` 列表。不列举桶，不删除对象，不调用 `sync-mirror.ps1`。

- [ ] **Step 4: Run test to verify it passes**

Run: `python -m unittest discover -s tools -p test_import_inbox.py` from the manager repo root.

Expected: PASS

---

### Task 7: 玩家下载路径没有投递桶

**Files:**
- Test: `tests/MechabellumModManager.Tests/InboxIsolationTests.cs`

**Interfaces:**
- Consumes: `ModCatalogService.BuildCatalogCandidates`、`UpdateChecker.BuildLatestJsonCandidates`、`RedistEnsureService.BuildArtifactCandidates`。
- Produces: 无新的生产接口。测试失败即表示玩家客户端开始请求投递桶。

- [ ] **Step 1: Write the test**

```csharp
[Fact]
public void Player_candidates_do_not_name_the_inbox_bucket()
{
    var mirror = "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com";
    var catalog = new ModCatalogService { MirrorBaseUrl = mirror };
    var updates = new UpdateChecker { MirrorBaseUrl = mirror };
    var joined = string.Join("\n",
        catalog.BuildCatalogCandidates().Select(u => u.AbsoluteUri)
        .Concat(catalog.BuildFileCandidates("mods/quick-mark/QuickMark.dll").Select(u => u.AbsoluteUri))
        .Concat(updates.BuildLatestJsonCandidates().Select(u => u.AbsoluteUri))
        .Concat(RedistEnsureService.BuildArtifactCandidates(mirror, new RedistArtifact { Path = "melonloader/MelonLoader.x64.zip", OriginUrl = "https://example.invalid/m.zip" }, DownloadRouteKind.MirrorFirst).Select(u => u.AbsoluteUri)));
    joined.Should().NotContain("mmm-inbox");
    joined.Should().NotContain("/inbox/");
}
```

`RedistArtifact` 用 `RedistEnsureService` 里现有的公开类型；若属性名不是 `Path` 和 `OriginUrl`，改成该类型已经公开的属性，不要为这个测试新增玩家可见的配置。这个测试在当前代码上应当通过。通过之后留作回归，不要为了制造失败去改下载地址。

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~InboxIsolationTests"`

Expected: PASS

- [ ] **Step 3: 若失败**

只删除玩家客户端里指向投递桶的地址。不要把投递桶加进镜像回落列表。

---

## 自检

这一节记录写完计划后核对过的问题。对应的约束和任务已经按核对结果写定，不再另留口子。

### 逻辑

- 现有 `authorizePublish` 在目录里没有该 id 时直接允许，即使 `owner:<id>` 已经属于别人。投递若复用它，撤掉目录条目之后别人仍能占同一个 id。Task 1 的 `authorizeInbox` 先看归属。
- `inviteId` 若含 `/` 或 `*`，临时策略的资源前缀会被放宽。`buildInboxPolicy` 只接受与 Mod id 相同的安全字符，否则抛 `unsafe id`。
- 目录里已有、但还没有主人的 id 必须拒绝。否则作者可以投递 `show-grid` 这类现成 Mod，导入时再靠一次确认才发现。拒绝放在申请时，导入脚本再拒绝一次。
- 判断 id 是否已在目录中，只读 GitHub 上的 `catalog.json`。镜像目录会滞后，也会被旧的直传覆盖，不能当作归属依据。GitHub 读失败时整单失败，不能当成新 id。
- 两名作者可以各自向自己的前缀投递同一个新 id。导入时先写入 `owner:<id>` 的那一次成功，后一次在 `authorizeInbox` 和导入脚本处失败。
- 临时凭证过期时间若按口头说的 30 分钟，大资源包可能传不完。官方子账号上限允许 3600 秒，计划用 3600。主账号上限只有 7200，而且主账号密钥不能放进 Worker，所以计划禁止主账号。
- `Policy` 按官方说明要 URL 编码一次，服务端再解码。编码两次会变成 `%257B`，策略无法生效。Task 1 有对这个形状的测试。
- `GetFederationToken` 的 `Name` 只能由字母组成，所以用 `inboxgrant`，不用带连字符的名字。
- 作者成功只表示投递区里有文件。玩家要等维护者写入 GitHub 目录并运行现有镜像同步。1.3.16 国内先读镜像，因此少了同步这一步时国内玩家仍看不到。

### 功能

- 窗口保留邀请码、名称、简介、版本、文件选择，并补上单个 DLL、分块、资源包三种形式。密钥框、总目录勾选和合并总目录按钮删除。
- 分块必须从 `0000` 连续编号。资源包至少两个 DLL，且恰好一个主文件。目录里的 `sha256` 对分块是拼接结果，对资源包是主文件，与 `validate_catalog.py` 一致。
- 邀请码吊销仍走现有 KV 的 `enabled: false`。`resolveInvite` 已经把它当作无效邀请码。
- `DirectUploadDefaults.ApiBaseUrl` 保持空字符串。空地址时窗口显示未配置，不会上传。部署后的地址由维护者写入这个常量或本机 `DirectUploadApiBaseUrl`，不写进本计划的默认值。
- 五份语言资源使用同一批新键。成功文案明确写玩家还看不到。

### 缺陷

- `CoscliUserConfig.Save` 在文件已存在时不更新 `sessiontoken`。临时凭证若写进这份文件，上传会用过期的空 token，同时覆盖维护者的镜像密钥。上传改为单独的临时 yaml，并在测试里证明用户目录那份不变。
- 现有镜像桶是整桶公有读。投递对象放进同一桶时，知道地址的人就能下载，流量也算在镜像桶上。计划使用单独的私有桶。
- 对象键若包含邀请码，码会留在日志和生命周期删除前的对象列表里。键里只用 `inviteId`。
- 客户端若自己写 `entry.json`，可以谎报哈希。KV 在签发时记下哈希，导入脚本重新计算文件哈希，不一致就拒绝。
- 现有 `/v1/mods/publish` 会把 DLL 读进 Worker 再直接提交 GitHub。文件可能超过 Worker 请求体限制，而且玩家会在审阅前从 GitHub 看到它。该路由改为 410，issuer 不会被调用。
- 窗口里的合并按钮会用长期密钥覆盖镜像上的 `catalog.json`。1.3.16 国内会先相信这份目录。按钮连同总目录勾选一起从窗口删除，源文件测试防止它们回到代码里。
- 临时密钥出现在状态栏或异常 `Detail` 里会进管理器日志。客户端只把错误码给界面，并在 coscli stderr 里去掉 `sessiontoken` 行。KV 记录也不保存临时密钥。
- 导入脚本若使用 `%USERPROFILE%\.cos.yaml`，会拿镜像桶的密钥去访问投递桶，或把投递桶密钥写回镜像配置。脚本拒绝这个路径。
- 申请里带上的 `minManagerVersion` 和 `originUrl` 不进入目录。错误的 floor（如前导零）在导入时拒绝。
- 玩家候选地址由 Task 7 钉住，不包含投递桶。镜像同步脚本不增加这个桶的参数。

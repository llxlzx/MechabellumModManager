# 閽㈤搧鎸囨尌瀹?Mod 绠＄悊鍣?鈥?GitHub Release 鏇存柊璇存槑

Mechabellum Mod Manager 鈥?GitHub Release guide

> **璇存槑 / Notice**  
> 鏈枃妗ｆ暣鐞嗚嚜鏈」鐩淮鎶よ€呭湪浣跨敤 Git 浠撳簱涓庡紑鍙?Mechabellum锛堥挗閾佹寚鎸ュ畼锛塎od 绠＄悊鍣ㄨ繃绋嬩腑鐨勫疄璺佃褰曪紝鏃ㄥ湪涓烘湁鎰忓紑鍙戠被浼煎伐鍏锋垨澶嶇幇鏈」鐩彂鐗堟祦绋嬬殑寮€鍙戣€呮彁渚涘弬鑰冦€傛枃涓秹鍙婄殑鐩綍銆佷唬鐞嗕笌鐜鍙橀噺鍧囦负**绀轰緥**锛岃鎸夋湰鏈虹幆澧冭嚜琛岃皟鏁达紱鍐呭涓嶆瀯鎴愬畼鏂规壙璇恒€佹湇鍔℃潯娆炬垨瀹屾暣杩愮淮瑙勮寖銆? 
> **This document collects the maintainer鈥檚 practical notes on using Git and developing the Mechabellum Mod Manager. It is shared as a reference for developers who wish to build similar tools or reproduce this release workflow. Paths, proxy settings, and environment variables are examples only鈥攁dapt them to your environment. This text is not an official commitment, terms of service, or a complete operations manual.**

> **鈿狅笍 AI 鐢熸垚澹版槑 / AI-generated notice**  
> 鏈鏄庢枃妗ｇ殑涓嫳鏂囧唴瀹逛富瑕佺敱 AI 杈呭姪鏁寸悊涓庣炕璇戯紝鍙兘瀛樺湪琛ㄨ堪鍋忓樊銆傝浠ュ疄闄呭彂鐗堣剼鏈笌浠撳簱琛屼负涓哄噯銆? 
> **This guide鈥檚 Chinese and English text was largely produced with AI assistance and may contain inaccuracies. Prefer the actual build scripts and repository behavior.**

闈㈠悜缁存姢鑰咃細濡備綍鎵撴柊鐗堟湰瀹夎鍖咃紝骞跺彂甯冨埌 GitHub Releases銆? 
For maintainers: how to build the Setup and publish a GitHub Release.

## 鐗堟湰閫熻 / Release notes

### v1.1.8

- Mod 鏇存柊妫€娴嬶細宸茶鐗堟湰钀藉悗鐩綍鏃舵爣銆屽彲鏇存柊銆嶏紝涓€閿鐩栧畨瑁呫€?
- 涓嬭浇鍘绘帀 80 MB 涓婇檺锛涙寜 catalog 鐨?`size` 鏍￠獙锛涚┖闂茶秴鏃?+ 鏂偣缁紶锛涜繘搴︽潯锛涙墍鏈夋潵婧愬己鍒?sha256銆?
- 鍊欓€夐『搴忥細鍥藉唴闀滃儚 鈫?`originUrl`锛圙itHub Release锛夆啋 raw锛涙敮鎸?catalog gzip銆?
- 闀滃儚宸ュ叿锛氱儹闂ㄥ瓙闆嗐€佸閲忓悓姝ャ€佸畨瑁呭寘闀滃儚鏃舵敼鍐?`setupUrl`锛涚淮鎶よ€呮枃妗ｅ榻?`release/v1.1.8/`銆?
- Detects outdated installed mods and offers one-click update.
- Removes the 80 MB download cap; validates against catalog `size`; idle timeout + resume; progress UI; sha256 required for every source.
- Candidates: domestic mirror 鈫?`originUrl` 鈫?raw; gzip catalogs accepted.
- Mirror tooling: hot subset, incremental sync, rewrite `setupUrl` when hosting the Setup; docs point at `release/v1.1.8/`.

### v1.1.7
- 淇鍚姩鑷姩鍥炴粴鍙兘鍒犻櫎鐜╁鍘熷瀹夎锛涘綊妗ｉ樁娈佃ˉ涓?journal锛屼腑鏂悗鍙垽瀹氬苟鎭㈠銆?
- Mod 涓嬭浇鏍￠獙 sha256锛堥暅鍍忔簮缂烘牎楠屽€肩洿鎺ユ嫆缁濓級锛涘閾惧彧鍏佽 https锛涢儴缃茶矾寰勫繀椤昏惤鍦ㄦ父鎴忕洰褰曞唴銆?
- 璇婃柇鍖呯殑 json/jsonl 璧?JSON 鑴辨晱锛岃秴澶ф棩蹇楀彧鐣欐湯灏?8 MB锛涢厤缃敼涓哄師瀛愬啓鍏ュ苟鍦ㄦ崯鍧忔椂澶囦唤銆?
- 琛ラ綈 ja/de/ru 缂哄け鐨?33 鏉℃枃妗堬紝骞跺姞閿泦涓庡崰浣嶇涓€鑷存€ф祴璇曘€?
- 缁存姢鑰呮枃妗ｃ€屽綋鍓嶆渶鏂般€嶄笌鍙戠増绀轰緥瀵归綈鍒?`release/v1.1.7/`銆?
- Startup auto-rollback no longer deletes the player's original install; the archive step is journalled.
- Mod downloads verify sha256 (mirror downloads without a hash are refused); external links are https-only; deploy paths must stay under the game root.
- Diagnostics json/jsonl use JSON-aware redaction and oversized logs keep only the last 8 MB; config writes are atomic with a backup on corruption.
- Adds the 33 missing ja/de/ru strings plus key-set and placeholder parity tests.
- Maintainer docs now treat `release/v1.1.7/` as current latest.

### v1.1.6

- 銆屾€ユ晳鎭㈠鍗曠洰褰曘€嶆敼涓恒€岃繕鍘熷畼鏂圭洰褰曘€嶏紱纭妗嗗幓鎺夋€ユ晳/浠撹璇濓紝姝ｅ紡鏈嶇己澶辨椂浠嶄笉鑳藉彉鍑烘寮忔湇銆?
- Button renamed to Restore Official folder; confirm copy drops emergency/store jargon.

### v1.1.5

- 鍒囨湇娓呯┖涓婃鍚姩鏃堕棿锛岄伩鍏嶆柊浠撳亣鎶ャ€屾湭娉ㄥ叆銆嶏紱鏈敞鍏ュ啓鍏?`loader_not_injected` 浜嬩欢涓庢椂闂寸嚎锛涘凡鏄綋鍓?Melon 涓嶅啀鍒?`upgrade_skipped`銆?
- 璺緞鏈氨缁椂缁撶畻閽鐢ㄥ苟鏀逛负绛夊緟鏂囨锛涚粨绠椾笉蹇呭厛閫€鍑?Steam锛涜仈鎺ヤ笌浠撴帰娴嬩笉涓€鑷磋 `junction_desync`銆?
- 鐘舵€佹爮涓庤瘖鏂寘缁欏嚭鍞竴涓诲洜锛涘浗鍐呴暅鍍忚疆璇?+ 鐩綍缂撳瓨銆?
- After branch switch, last-launch time is cleared so the other store is not falsely 鈥渘ot injected鈥? missing injection is recorded as `loader_not_injected`; `already_current` no longer spams `upgrade_skipped`.
- Settle Continue is disabled while the game path is missing files; settle does not require quitting Steam; junction/store mismatch is recorded as `junction_desync`.
- Status bar and diagnostics zip name one primary cause; first-party mirror then GitHub; catalog cache.

### v1.1.4

- 鐘舵€佹爮涓庤瘖鏂寘缁欏嚭鍞竴涓诲洜锛堝凡鎺掗櫎 + 璇佹嵁锛夛紱Steam 鍒嗘敮涓嶄竴鑷翠笉浼氱洊杩?Melon 鍗囩骇/鏈敞鍏ャ€?
- 鍚敤鍙屾湇鏀跺熬鎭㈠姝ｅ紡鏈?Steam 娓呭崟蹇収锛岄伩鍏?Steam 缁忚仈鎺ラ噸鍐欎笅绌?`_official`锛涚粨绠楀け璐ヤ笉鍐嶆€傛伩鎬ユ晳/鍒囦粨锛涘弻浠撶粨鏋勫畬鎴愬悗娓呴櫎 SessionOwned銆?
- Status bar and diagnostics zip name one primary cause; Steam branch mismatch does not override Melon upgrade / not-injected.
- Enable-dual restores the Official ACF snapshot so Steam does not rewrite `_official`; settle copy no longer suggests Emergency/switch; session-owned flags clear once both stores are linked.

### v1.0.9

- 鐙崰椤甸潰甯冨眬锛涚洰褰?鏈湴搴撳瘑搴︿笌瑙嗚鎶涘厜锛涖€屽凡瑁?Mod / Mod 宸ュ潑銆嶅懡鍚嶏紱杩愯鏃ュ織鍙睍寮€鏀惰捣銆?
- 璺熼殢绯荤粺璇█鎸?Windows 鏄剧ず璇█锛汳elon 鍙屾湇鍚屾璺宠繃闄堟棫 Config.cfg锛涘畨瑁呭櫒闈欓粯 .NET銆佸幓鎺夌粨鏉熷墠浜屾鍚姩銆?
- Exclusive pages; density + polish; Installed/Workshop labels; expandable activity log.
- Follow-system uses Windows display language; Melon dual-store skips stale Config.cfg; quiet .NET; no pre-finish app launch.

### v1.0.5

- 鎶曠 / 鏇存柊 / 涓炬姤 / 寤鸿鏀逛负閭欢 **llxmod@foxmail.com**锛堟爣鍑嗕富棰樺墠缂€ + 姝ｆ枃妯℃澘锛涘簲鐢ㄥ唴澶嶅埗鍒板壀璐存澘骞舵墦寮€缃戦〉閭锛夈€?
- 浣滆€呮棤闇€ Fork/PR锛涚淮鎶よ€呭鏍稿悗涓婁紶 MechabellumMods銆?
- Mod 搴?Author 鍒楋紙鑻ヤ笂涓€鐗堝凡鍚垯寤剁画锛夈€?

### v1.0.4

- Mod 搴撴柊澧?Author锛堜綔鑰咃級鍒楁樉绀恒€?

鏇村畬鏁寸殑鎶€鏈粏鑺傝鍚岀洰褰?`releasing.md`銆? 
More technical detail: `releasing.md` in this folder.

## 璇█ / Language

浣跨敤涓嬫柟閾炬帴鍦ㄦ湰椤靛悇鑺傞棿璺宠浆锛堜腑鑻辨枃鍐呭鍧囧湪鍚屼竴鏂囨。涓級銆?

Use the links below to jump within this page (Chinese and English sections share one document).

- 涓枃锛歔涓や釜浠撳簱](#1-涓や釜浠撳簱鍒嗗埆骞蹭粈涔?涓枃) 路 [鏍囧噯鍙戠増姝ラ](#3-鍙戠鐞嗗櫒鏂扮増鏈爣鍑嗘楠?涓枃) 路 [latest.json](#4-latestjson-鎬庝箞鍐?涓枃) 路 [FAQ](#8-甯歌闂-涓枃)
- English: [Two repos](#1-what-the-two-repos-are-for-english) 路 [Release steps](#3-shipping-a-new-manager-version-english) 路 [latest.json](#4-latestjson-english) 路 [FAQ](#8-faq-english)

---

## 1. 涓や釜浠撳簱鍒嗗埆骞蹭粈涔?(涓枃)

| 浠撳簱 | 鍦板潃 | 鍙戜粈涔?|
|------|------|--------|
| **绠＄悊鍣?* | https://github.com/llxlzx/MechabellumModManager | Setup 瀹夎鍖呫€佷究鎼烘湰浣撱€乴atest.json |
| **Mod 澶у叏** | https://github.com/llxlzx/MechabellumMods | catalog.json銆佸悇 Mod 鐨?dll / 棰勮鍥?|

鐜╁銆屾鏌ユ洿鏂般€嶅彧鐪?*绠＄悊鍣?*浠撳簱鐨?Release銆? 
銆孧od 娴忚 鈫?鍒锋柊鐩綍銆嶅彧鐪?**Mods** 浠撳簱鐨?`catalog.json`銆備袱鑰呭彂鐗堝彲浠ヤ笉鍚屾銆?

---

## 2. 鏈湴鏂囦欢鎬庝箞鍒嗭紙瀹夎鍖?vs 鏈綋锛?(涓枃)

鍦ㄧ鐞嗗櫒浠撳簱鐨?`release/v鐗堟湰鍙?` 涓嬶細

```
release/v1.1.8/
  瀹夎鍖?     鈫?缁欑粷澶у鏁扮敤鎴凤紙Setup.exe锛?
  鏈綋/       鈫?渚挎惡杩愯锛坋xe + Assets锛屾棤 Mod 鏁版嵁锛?
  latest.json 鈫?缁欍€屾鏌ユ洿鏂般€嶇敤
  MechabellumModManager_portable_v1.1.8.zip  鈫?鎶娿€屾湰浣撱€嶆墦鎴愮殑 zip锛屼笂浼?Release
```

| | **瀹夎鍖?* | **鏈綋锛堜究鎼猴級** |
|--|------------|------------------|
| 鏂囦欢 | `MechabellumModManager_Setup_vX.Y.Z.exe` | `MechabellumModManager.exe` + `Assets\` |
| 閫傚悎 | 鏅€氱敤鎴蜂竴閿畨瑁?| 宸茶 .NET 8銆佹兂鍏嶅畨瑁呰繍琛?|
| 鍚?Melon 绂荤嚎鍖?| 鏄紙鎵撳湪 Setup 閲岋級 | 鍚?|
| 鍚湰鍦?Mod / 鏂规 | 鍚?| 鍚?|

**绂佹**鎶娿€孲etup 瑁呭畬鍚庣殑鏂囦欢澶广€嶆暣鍖呭綋鏈綋涓婁紶銆? 
閭ｇ鐩綍閲屽父鏈?`unins000.*`銆乣installer-redist\`銆乣installer-scripts\`锛屼笉灞炰簬渚挎惡鏈綋銆? 
骞插噣鏈綋璇风敤锛歚release/vX.Y.Z/鏈綋\` 鎴栨瀯寤轰骇鐢熺殑 `publish\`銆?

---

## 3. 鍙戠鐞嗗櫒鏂扮増鏈細鏍囧噯姝ラ (涓枃)

浠ュ彂甯?**v1.1.8** 涓轰緥锛堜互鍚庢妸鐗堟湰鍙锋崲鎴愭柊鐨勫嵆鍙級銆?

### 姝ラ 1 鈥?鏀圭増鏈彿

鍚屾椂鏀硅繖涓夊锛屾暟瀛楀繀椤讳竴鑷达細

1. `src/MechabellumModManager/MechabellumModManager.csproj` 鈫?`<Version>1.1.8</Version>`
2. `packaging/installer/MechabellumModManager.iss` 鈫?`#define MyAppVersion "1.1.8"`
3. 绋嶅悗鐨?`latest.json` 鈫?`"version": "1.1.8"`

### 姝ラ 2 鈥?鍑嗗 MelonLoader 绂荤嚎鍖咃紙蹇呭仛锛?

鎶婂畼鏂?`MelonLoader.x64.zip` 鏀惧埌锛?

`packaging/installer/redist/melonloader/MelonLoader.x64.zip`

涓嬭浇锛歨ttps://github.com/LavaGang/MelonLoader/releases  

娌℃湁杩欎釜鏂囦欢鏃讹紝鏋勫缓鑴氭湰浼?*鐩存帴澶辫触**锛屼笉鍏佽鎵撴寮忓寘銆?

### 姝ラ 2b 鈥?Unity 渚濊禆 + .NET 8 绂荤嚎鍖咃紙fat Setup 蹇呴€夛級

闄?Melon zip 澶栵紝杩橀渶鏀惧叆 `packaging/installer/redist/`锛?

- `unity-deps/UnityDependencies_{鐗堟湰}.zip` 鈥?浠?https://github.com/LavaGang/Unity-Runtime-Libraries 涓嬭浇锛堝 `2022.3.62.zip`锛夛紝**閲嶅懡鍚?*涓?`UnityDependencies_2022.3.62.zip`
- `dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` 鈥?.NET 8 Desktop Runtime 绂荤嚎瀹夎鍖?

**鍥藉唴锛?* Setup 鏈韩鍙兘浠嶉渶闀滃儚/浠ｇ悊鑾峰彇锛涘畨瑁呭畬鎴愬悗鐩爣鏄湪 **鏂綉** 涓嬪畬鎴愰娆?Il2Cpp 鐢熸垚锛堢骇鍒?**B**锛夈€傚彂鐗堝墠鍦ㄧ湡鏈烘柇缃戝惎鍔ㄤ竴娆★紝纭 Melon 涓嶅啀绱㈣ Cpp2IL 绛夐澶?AG 鍖咃紙瑙?design spec acceptance **F**锛夈€傝瑙?`releasing.md` 搂1.2b銆?

### 姝ラ 3 鈥?娴嬭瘯骞舵帹浠ｇ爜

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
git add ...
git commit -m "璇存槑鏈増鏀瑰姩"
git push origin master
```

鑻ユ祻瑙堝櫒鍙闂?GitHub锛屼絾缁堢 `git` / `gh` 澶辫触锛屽彲鎸夋湰鏈轰唬鐞嗚蒋浠惰鏄庤缃?HTTP(S) 浠ｇ悊鐜鍙橀噺锛堝湴鍧€涓庣鍙ｄ互浣犵殑瀹㈡埛绔负鍑嗭紝浠ヤ笅浠呬负鍗犱綅绀轰緥锛夛細

```powershell
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'
```

### 姝ラ 4 鈥?鎵撳畨瑁呭寘

```powershell
.\packaging\installer\build-installer.ps1
```

寰楀埌锛歚dist\MechabellumModManager_Setup_v1.1.8.exe`  
锛堜綋绉ぇ绾︿簩鍗佸 MB 鎵嶆甯革紝鍥犱负鍐呭祵浜?Melon銆傦級

鎶婁骇鐗╂暣鐞嗚繘 `release/v1.1.8/瀹夎鍖?` 涓?`release/v1.1.8/鏈綋/`锛屽苟鍐欏ソ `latest.json`銆?

瀹夎鍚戝搴斾緷娆″嚭鐜帮細**閫夋嫨鐩爣浣嶇疆**锛堢鐞嗗櫒瀹夎鐩綍锛夆啋 **閫夋嫨娓告垙鐩綍**銆傝嫢鍙湅鍒版父鎴忕洰褰曢〉锛岃纭宸蹭娇鐢ㄥ惈 `DisableDirPage=no` 鐨?Setup銆?

### 姝ラ 5 鈥?鎵撴湰浣?zip

```powershell
cd release\v1.1.7
Compress-Archive -Path ".\鏈綋\*" -DestinationPath ".\MechabellumModManager_portable_v1.1.8.zip" -Force
```

瑙ｅ帇鍚庡簲鐩存帴鐪嬪埌 exe 鍜?Assets锛岃€屼笉鏄涓€灞傛棤鍏崇洰褰曘€?

### 姝ラ 6 鈥?鍦?GitHub 涓婂缓 Release

1. 鎵撳紑锛歨ttps://github.com/llxlzx/MechabellumModManager/releases  
2. **Draft a new release**  
3. **Tag**锛氳緭鍏?`v1.1.8` 鈫?Create new tag锛?*Target** 閫?`master`  
4. **Title**锛歚v1.1.8`  
5. **璇存槑**锛氬啓鏈増鏇存柊鐐癸紙鍙笌 latest.json 鐨?notes 鐩稿悓锛涘彲涓嫳鍙岃锛? 
6. **涓婁紶闄勪欢**锛堟嫋杩涜櫄绾挎锛夛細

| 蹇呬紶 / 鎺ㄨ崘 | 鏂囦欢 |
|-------------|------|
| 蹇呬紶 | `瀹夎鍖匼MechabellumModManager_Setup_v1.1.8.exe` |
| 蹇呬紶 | `latest.json`锛?*鏂囦欢鍚嶄笉鑳芥敼**锛?|
| 鎺ㄨ崘 | `MechabellumModManager_portable_v1.1.8.zip` |

7. 鍕鹃€?**Set as the latest release**  
8. **涓嶈**鍕鹃€?Pre-release  
9. **Publish release**

### 姝ラ 7 鈥?鑷

- 鎵撳紑锛歨ttps://github.com/llxlzx/MechabellumModManager/releases/tag/v1.1.8  
  纭涓変釜璧勬簮閮藉湪  
- 鎵撳紑锛? 
  `https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`  
  鑳芥樉绀?JSON  
- 鎵撳紑绠＄悊鍣?鈫?**璁剧疆 鈫?妫€鏌ユ洿鏂?*锛屽簲鎻愮ず鏈夋柊鐗堟湰锛堣嫢鏈満杩樻槸鏃х増锛?

### 姝ラ 8 鈥?鍚屾鍥藉唴闀滃儚锛堝凡鎼暅鍍忔椂蹇呭仛锛?

Release 鍙戝畬涔嬪悗璺戯細

```powershell
.\tools\sync-mirror.ps1 -Bucket <妗跺悕-APPID> `
    -ModsRepo "<MechabellumMods 鏈湴鍏嬮殕>" `
    -ReleaseDir .\release\v1.1.7

.\tools\verify-mirror.ps1 -BaseUrl https://<妗跺煙鍚? -ExpectVersion 1.1.8
```

闀滃儚涔熸墭绠″畨瑁呭寘鏃讹紝杩藉姞 `-IncludeSetup -MirrorBaseUrl https://<妗跺煙鍚?`锛氬鎴风鎶?`setupUrl` 鍘熸牱浜ょ粰娴忚鍣紝鑴氭湰浼氭妸闀滃儚閭ｄ唤 `latest.json` 鐨?`setupUrl` 鎸囧悜闀滃儚涓婄殑 exe锛屾湰鍦版枃浠朵笉鍔ㄣ€?

婕忔帀杩欎竴姝ョ殑鍚庢灉锛氶厤浜嗛暅鍍忕殑鐜╁浼樺厛璇婚暅鍍忥紝浠栦滑鐨勩€屾鏌ユ洿鏂般€嶄細涓€鐩村仠鍦ㄦ棫鐗堟湰銆?

鎼缓姝ラ涓庢垚鏈姢鏍忚 `docs/鍥藉唴闀滃儚鎼缓.md`銆?

---

## 4. latest.json 鎬庝箞鍐?(涓枃)

```json
{
  "version": "1.1.8",
  "notes": "鏈増鏇存柊璇存槑锛屽彲澶氳銆?,
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.1.8/MechabellumModManager_Setup_v1.1.8.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

| 瀛楁 | 鍚箟 |
|------|------|
| version | 涓庡畨瑁呭寘鐗堟湰涓€鑷?|
| notes | 妫€鏌ユ洿鏂版椂灞曠ず鐨勮鏄?|
| setupUrl | Setup 涓嬭浇鐩撮摼 |
| publishedAt | 鍙戝竷鏃堕棿锛堝彲閫夛級 |

涓婁紶鍒?Release 鏃讹紝闄勪欢鍚嶅繀椤绘槸 **`latest.json`**锛屽惁鍒欍€屾鏌ユ洿鏂般€嶄紭鍏堝湴鍧€浼氬け璐ャ€?

---

## 5. 鍙洿鏂?Mod 澶у叏鏃讹紙涓嶅彂绠＄悊鍣ㄦ柊鐗堟湰锛?(涓枃)

```powershell
cd <path-to-your-MechabellumMods-clone>
# 鏀?mods/銆乧atalog.json銆乸review.png 绛?
python scripts/stamp_hashes.py       # 鍔ㄨ繃浠讳綍 DLL 閮藉繀椤婚噸绠?sha256
python scripts/validate_catalog.py   # CI 璺戠殑鍚屼竴濂楁牎楠?
git add -A
git commit -m "璇存槑"
git push origin master
```

`sha256` 涓嶆槸鍙€夐」锛氱鐞嗗櫒鎷掔粷瀹夎浠讳綍缁忓浗鍐呴暅鍍忎笅杞姐€佷絾鐩綍閲屾病鏈夊搱甯岀殑 Mod锛屽悓鏃跺畠涔熸槸鍒ゆ柇鐜╁鏈湴鍓湰杩囨湡鐨勬渶鍙潬渚濇嵁銆侰I 浼氭瘮瀵瑰搱甯屼笌纾佺洏鏂囦欢锛屼笉涓€鑷寸洿鎺ュけ璐ャ€?

纭锛? 
https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json  

宸叉惌鍥藉唴闀滃儚鐨勮瘽锛宲ush 涔嬪悗杩樿鍚屾涓€娆★紙涓嶅甫 `-ReleaseDir`锛夛細

```powershell
.\tools\sync-mirror.ps1 -Bucket <妗跺悕-APPID> -ModsRepo "<MechabellumMods 鏈湴鍏嬮殕>"
```

鐜╁鍦ㄧ鐞嗗櫒閲岀偣 **Mod 娴忚 鈫?鍒锋柊鐩綍** 鍗冲彲锛?*涓嶅繀**閲嶅彂 Setup銆傜洰褰曢噷鎹簡鏂扮増 Mod 鐨勮瘽锛屽凡瑁呮棫鐗堢殑鐜╁浼氬湪鐘舵€佸垪鐪嬪埌銆屾湁鏂扮増鏈€嶃€?

浣滆€呮彁浜ゆ柟寮忚锛歚MechabellumMods` 浠撳簱鍐呯殑 `README.md`锛堣嫳姹夊弻璇柊鎵嬫暀绋嬶級銆?

---

## 6. v1.0.5 鏈増瑕佺偣 (涓枃)

- **閭欢浼樺厛锛堢幇琛岋級**锛氭姇绋?/ 鏇存柊 / 涓炬姤 / 寤鸿鍙戝線 **llxmod@foxmail.com**锛堟爣鍑嗕富棰樺墠缂€ + 姝ｆ枃妯℃澘锛涘簲鐢ㄥ唴澶嶅埗鍒板壀璐存澘骞舵墦寮€缃戦〉閭锛夈€備綔鑰?*鏃犻渶** Fork/PR銆?
- **澶勭悊鏃舵晥**锛氱淮鎶よ€呭皢鍦ㄥ悎鐞嗘椂闂村唴瀹￠槄锛涘彈鏃ュ父鐢熸椿涓庝釜浜轰簨鍔″畨鎺掑奖鍝嶏紝鎴栨湁鐭殏寤惰锛岃鍕块噸澶嶅彂閫佸悓涓€鍐呭銆?
- **鍏嶈矗涓庢硶寰嬭鏄?*锛氬簲鐢ㄥ唴銆屽叧浜庝笌澹版槑銆嶅凡鎵╁睍锛涗粨搴撴牴鐩綍瑙?`NOTICE.md` 涓?MIT `LICENSE`銆?
- **MechabellumMods 渚?*锛氶偖浠舵姇绋挎寚鍗?+ `docs/submit.html`锛沗catalog.json` CI 鏍￠獙浠嶄繚鐣欍€?

> **鍘嗗彶锛堚墹1.0.4锛?*锛氬彇娑?Cloudflare 涓户鍚庯紝鏇剧煭鏆備互 GitHub Fork/PR 鎶曠銆両ssues 涓炬姤涓轰富璺緞锛涜嚜 **1.0.5** 璧蜂互閭欢涓哄噯銆?

## 7. 褰撳墠鏈湴瀵圭収锛堝啓浣滄椂锛?(涓枃)

| 椤?| 浣嶇疆 |
|----|------|
| 鏈€鏂版暣鐞嗙洰褰曪紙褰撳墠鏈€鏂帮級 | `release/v1.1.8/`锛堢浉瀵规湰浠撳簱鏍圭洰褰曪級 |
| 娴佺▼璇︾増 | `docs/release/releasing.md` |
| 鏈鏄?| `docs/release/GitHub-Release鏇存柊璇存槑.md`锛堟湰鏂囦欢锛岃嫳姹夊弻璇級 |

---

## 8. 甯歌闂 (涓枃)

**Q锛氬畨瑁呮祴璇曠洰褰曢噷鐨勩€岄挗閾佹寚鎸ュ畼Mod绠＄悊鍣ㄣ€嶈兘鏁村寘褰撴湰浣撳悧锛?*  
A锛氫笉鑳姐€傚幓鎺夊嵏杞藉櫒鍜?installer 鐩綍鍚庯紝鐞嗚涓婂彧鐣?exe+Assets 鍙互锛屼絾璇蜂紭鍏堢敤 `release/vX.Y.Z/鏈綋\`銆?

**Q锛歅ush / 妫€鏌ユ洿鏂板け璐ワ紝缃戦〉鍗磋兘寮€ GitHub锛?*  
A锛氭祻瑙堝櫒鍙兘宸茶蛋绯荤粺/鎵╁睍浠ｇ悊锛岃€?Git 鎴栫鐞嗗櫒鏈厤缃唬鐞嗐€傝鎸夋湰鏈轰唬鐞嗗鎴风鏂囨。涓虹粓绔缃?`HTTPS_PROXY` / `HTTP_PROXY`锛堜富鏈轰笌绔彛鍥犺蒋浠惰€屽紓锛夈€?

**Q锛歁od 娴忚鍒锋柊澶辫触锛?*  
A锛氭鏌?Mods 浠撳簱鏄惁鍏紑銆乣catalog.json` 鏄惁鍦?`master` 鍒嗘敮鏍圭洰褰曪紝浠ュ強鏈満鑳藉惁璁块棶 raw.githubusercontent.com銆?

---

## 1. What the two repos are for (English)

| Repo | URL | Publishes |
|------|-----|-----------|
| **Manager** | https://github.com/llxlzx/MechabellumModManager | Setup, portable zip, `latest.json` |
| **Mods catalog** | https://github.com/llxlzx/MechabellumMods | `catalog.json`, mod DLLs / previews |

銆孋heck for updates銆?only reads the **manager** Releases.  
銆孊rowse mods 鈫?Refresh銆?only reads Mods `catalog.json`. The two can ship on different schedules.

---

## 2. Local layout (Setup vs portable) (English)

Under `release/vVERSION/` in the manager repo:

```
release/v1.1.8/
  瀹夎鍖?     鈫?Setup for most users
  鏈綋/       鈫?portable (exe + Assets, no mod data)
  latest.json 鈫?used by in-app update check
  MechabellumModManager_portable_v1.1.8.zip  鈫?zip of 鏈綋/, attach to Release
```

| | **Setup** | **Portable** |
|--|-----------|--------------|
| Files | `MechabellumModManager_Setup_vX.Y.Z.exe` | `MechabellumModManager.exe` + `Assets\` |
| Audience | One-click install | Users with .NET 8 who want no installer |
| Melon offline zip | Embedded in Setup | No |
| Local mods / profiles | No | No |

**Do not** zip a post-Setup install folder as portable (`unins000.*`, `installer-redist\`, etc.).  
Use `release/vX.Y.Z/鏈綋\` or `publish\`.

---

## 3. Shipping a new manager version (English)

Example: **v1.1.7** (replace the version everywhere for later releases).

### Step 1 鈥?Bump version (three places, same number)

1. `src/MechabellumModManager/MechabellumModManager.csproj` 鈫?`<Version>1.1.8</Version>`
2. `packaging/installer/MechabellumModManager.iss` 鈫?`#define MyAppVersion "1.1.8"`
3. `latest.json` 鈫?`"version": "1.1.8"`

### Step 2 鈥?MelonLoader offline zip (required)

Place official `MelonLoader.x64.zip` at:

`packaging/installer/redist/melonloader/MelonLoader.x64.zip`

From: https://github.com/LavaGang/MelonLoader/releases  

Without it, the build script **fails** and will not produce a release Setup.

### Step 2b 鈥?Unity deps + .NET 8 offline (fat Setup required)

Also place under `packaging/installer/redist/`:

- `unity-deps/UnityDependencies_{version}.zip` 鈥?from https://github.com/LavaGang/Unity-Runtime-Libraries (e.g. download `2022.3.62.zip`, **rename** to `UnityDependencies_2022.3.62.zip`)
- `dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` 鈥?.NET 8 Desktop Runtime offline installer

**Domestic users:** Setup download may still need a mirror/VPN; the goal is **post-install** offline first-run Il2Cpp generation (level **B**). Before release, disconnect network on a real machine and launch once; confirm Melon does not ask for extra AG packages such as Cpp2IL (design spec acceptance **F**). See `releasing.md` 搂1.2b.

### Step 3 鈥?Test and push code

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
git add ...
git commit -m "Describe this release"
git push origin master
```

If the browser can open GitHub but the terminal `git` / `gh` fails, set HTTP(S) proxy environment variables per your local proxy client (host and port vary; the following is a placeholder only):

```powershell
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'
```

### Step 4 鈥?Build the installer

```powershell
.\packaging\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_v1.1.8.exe`  
(~20+ MB is normal; Melon is embedded.)

Copy into `release/v1.1.8/瀹夎鍖?` and `release/v1.1.8/鏈綋/`, and write `latest.json`.

Wizard order should be: **Select destination** (manager install dir) 鈫?**Select game folder**. If the first page is missing, rebuild with `DisableDirPage=no`.

### Step 5 鈥?Zip portable

```powershell
cd release\v1.1.7
Compress-Archive -Path ".\鏈綋\*" -DestinationPath ".\MechabellumModManager_portable_v1.1.8.zip" -Force
```

Extracted zip should show `exe` + `Assets` at the top level (no extra junk folder).

### Step 6 鈥?Create the GitHub Release

1. Open https://github.com/llxlzx/MechabellumModManager/releases  
2. **Draft a new release**  
3. **Tag:** `v1.1.7` 鈫?create tag; **Target:** `master`  
4. **Title:** `v1.1.7`  
5. **Description:** release notes (can match `latest.json` notes; bilingual OK)  
6. **Attach:**

| Required / recommended | File |
|------------------------|------|
| Required | `瀹夎鍖匼MechabellumModManager_Setup_v1.1.8.exe` |
| Required | `latest.json` (**exact filename**) |
| Recommended | `MechabellumModManager_portable_v1.1.8.zip` |

7. Check **Set as the latest release**  
8. Do **not** check Pre-release  
9. **Publish release**

### Step 7 鈥?Verify

- https://github.com/llxlzx/MechabellumModManager/releases/tag/v1.1.8 鈥?all assets present  
- https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json 鈥?valid JSON  
- Manager 鈫?**Settings 鈫?Check for updates** should offer the new version if the installed app is older

### Step 8 鈥?Sync the domestic mirror (required once a mirror exists)

After publishing the Release:

```powershell
.\tools\sync-mirror.ps1 -Bucket <bucket-appid> `
    -ModsRepo "<MechabellumMods clone>" `
    -ReleaseDir .\release\v1.1.7

.\tools\verify-mirror.ps1 -BaseUrl https://<bucket-domain> -ExpectVersion 1.1.8
```

If the mirror also hosts the installer, add `-IncludeSetup -MirrorBaseUrl https://<bucket-domain>`.
The client opens `setupUrl` in the browser rather than downloading it, so the script repoints
`setupUrl` in the mirror's copy of `latest.json`; the local file is left alone.

Skip it and players who configured the mirror keep seeing the old version, because the
mirror is tried before GitHub.

Setup steps and cost guardrails: `docs/鍥藉唴闀滃儚鎼缓.md`.

---

## 4. latest.json (English)

```json
{
  "version": "1.1.8",
  "notes": "Release notes (can be multi-line).",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.1.8/MechabellumModManager_Setup_v1.1.8.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

| Field | Meaning |
|-------|---------|
| version | Must match Setup version |
| notes | Shown in 鈥淐heck for updates鈥?|
| setupUrl | Direct Setup download URL |
| publishedAt | Optional timestamp |

The Release attachment **must** be named `latest.json`.

---

## 5. Catalog-only updates (no new manager) (English)

```powershell
cd <path-to-your-MechabellumMods-clone>
# edit mods/, catalog.json, previews, 鈥?
python scripts/stamp_hashes.py       # re-stamp sha256 after touching any DLL
python scripts/validate_catalog.py   # the same check CI runs
git add -A
git commit -m "Describe catalog change"
git push origin master
```

`sha256` is not optional: the manager refuses mirror-served downloads for entries without
one, and uses it to detect a stale local copy. CI compares each hash against the file on
disk and fails on mismatch.

Confirm:  
https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json  

If a domestic mirror is live, sync it too (no `-ReleaseDir` needed):

```powershell
.\tools\sync-mirror.ps1 -Bucket <bucket-appid> -ModsRepo "<MechabellumMods clone>"
```

Players only need **Browse mods 鈫?Refresh catalog**. No new Setup required. Anyone still on
an older build of a mod sees "Update available" in the status column.

Author flow: bilingual beginner guide in the MechabellumMods `README.md`.

---

## 6. v1.0.5 highlights (English)

- **Email-first (current):** submit / update / report / feedback to **llxmod@foxmail.com** (standard subject prefixes + templates; in-app clipboard copy + webmail). Authors do **not** need Fork/PR.
- **Processing time:** review within a reasonable time; brief delays may occur due to daily life and personal schedule 鈥?please do not resend the same request.
- **Disclaimer / legal:** expanded in-app Credits; see repo root `NOTICE.md` and MIT `LICENSE`.
- **MechabellumMods:** email guide + `docs/submit.html`; `catalog.json` CI validation remains.

> **Historical (鈮?.0.4):** after removing the Cloudflare relay, Fork/PR submit and Issues report were briefly the primary path; from **1.0.5** email is canonical.

## 7. Local paths (at write time) (English)

| Item | Path |
|------|------|
| Packaged folder (current latest) | `release/v1.1.8/` (relative to this repo root) |
| Longer tech notes | `docs/release/releasing.md` |
| This guide | `docs/release/GitHub-Release鏇存柊璇存槑.md` (bilingual) |

---

## 8. FAQ (English)

**Q: Can I upload a full post-Setup install folder as portable?**  
A: No. Prefer `release/vX.Y.Z/鏈綋\` (exe + Assets only).

**Q: Push / update check fails but the browser opens GitHub?**  
A: The browser may already use a system/extension proxy while Git or the manager does not. Set `HTTPS_PROXY` / `HTTP_PROXY` for the terminal per your local proxy client docs (host and port vary; e.g. `http://127.0.0.1:<PORT>`).

**Q: Browse mods refresh fails?**  
A: Repo must be public, `catalog.json` on `master` root, and the machine must reach raw.githubusercontent.com.

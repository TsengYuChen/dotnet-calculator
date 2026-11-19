# 第 9 階段：Implementation Readiness 報告

**Status:** resolved（2026-11-19）

**作者決定（逐項確認）：** 22 個 Major 全部接受。其中 R-M4 選 (a)：新增 `IUnitOfWork`；R-M10 選 (b)：直接修改資料庫的 `deleted_at`；R-M14：三條法規的規則版本日期都由作者指定為 2026-01-01（不是條文實際修正日期，文件註明）。Nit 分四組（計算細節、API 和測試細節、建置與工具、文字）全部接受。已重寫詳細實作計畫，並同步修改功能規格（CUJ 10 的指令、技術計畫第 2–4 節、D-R1、OQ-1、G9-1、風險表）。

*   **審查對象：** `docs/designs/vehicle_management.md` 第 725–1066 行（Detailed Implementation Plan）
*   **審查者：** egm-implementation-readiness（implementation-readiness-standard）、egm-codebase-details（技術正確性；repo 沒有程式碼，不做重用搜尋）、egm-style
*   **日期：** 2026-11-19

## 總評

**還不能直接實作：0 個 Blocker、22 個 Major（已合併重複）、約 50 個 Nit。**

架構、檔案清單和實作順序都夠清楚。implementation-readiness 審查者用 1.3 的虛擬碼手算 CUJ 3、4、4b、4c、5、6、12，除了排氣定檢的最後期限（R-M1），期間、狀態和規則 ID 都和功能規格一致。codebase-details 確認大部分 API 的用法正確（EF Core 10 具名查詢過濾器、`xmin` 對應 `uint`、`DateOnly` 月底行為、`HKDF.DeriveKey`、OIDC 選項、`gitleaks git --staged`、`[extend] useDefault = true` 等）。style 審查沒有找到已移除內容的殘留或舊編號。

剩下的 Major 多半是「工程師要自己做設計決定」的缺口：交易邊界、錯誤回應的格式、代碼的序列化方式、時間的語意、環境變數、migration 的產生方式，以及幾處照字面實作會編譯失敗或行為不對的地方。

編號：`R-M*` 是合併後的 Major。來源：`IR` = implementation-readiness、`CD` = codebase-details、`ST` = style。

## Major

| 編號 | 來源 | 位置 | 問題 | 建議 |
| --- | --- | --- | --- | --- |
| R-M1 | IR M1 | 1.3 虛擬碼 | 排氣定檢的最後期限照「一律用 `AddMonths`」實作，11 月發照會得到 12/30，CUJ 5 要求 12/31 | 寫成 `start = (Y,M,1).AddMonths(-1)`、`deadline = LastDayOfMonth((Y,M,1).AddMonths(1))`；註明排氣定檢以月計，不套用月底往前夾；測試加 2 月、11 月發照 |
| R-M2 | IR M2 | 1.3、1.4 | `EXEMPT`／`UNCOMPUTABLE` 的 `basis`、`ruleId`、`estimated` 沒有定義；EXHAUST 分支沒有設定 `basis`，大型重機有填 L 時可能沿用 `LICENCE` | `EXEMPT`：`basis = FORMULA`、`estimated = false`；`UNCOMPUTABLE`：`ruleId = null`（DTO 改 `ruleId?`）；EXHAUST 明寫 `basis = FORMULA` |
| R-M3 | IR M3 | 1.3 | 規則引擎 `INVALID_INPUT` 檢查哪些條件、以單一檢驗還是整台車為單位，沒有定義 | 列出引擎自己檢查的條件（不做和今天有關的驗證）；`UNCOMPUTABLE` 以單一檢驗為單位 |
| R-M4 | IR M4、CD M2 | 2.3 `IVehicleRepository` | 「鎖住使用者、計數、新增」要在同一個交易，但 Application 沒有交易抽象，也不能參考 EF Core；EF 沒有 `FOR UPDATE` 的 LINQ API | (a) 新增 `IUnitOfWork.ExecuteInTransactionAsync`；或 (b) `IVehicleRepository.CreateWithinLimitAsync` 把清除、鎖定、計數、新增包成一個方法。寫明用原生 SQL 鎖定、不啟用 Npgsql 重試；補上各方法的回傳型別 |
| R-M5 | IR M5、CD M1 | 2.4 `PostgresErrorMapper` | 使用中和垃圾桶撞到的是同一個唯一索引，分不出 `PLATE_EXISTS_ACTIVE`／`IN_TRASH`，也拿不到 `trashedVehicleId`；23505 後交易已中止，不能再查 | 寫入前先用 `FindByNormalizedPlateAsync(includeTrashed: true)` 查；23505 只當同時寫入的後備，回 `PLATE_EXISTS_ACTIVE` |
| R-M6 | IR M6、CD M3 | 1.4、1.5、2.5 | `VALIDATION_FAILED` 的欄位清單格式沒定義；`[ApiController]` 自動 400、JSON 解析錯誤不會帶 `code`；`uint Version` 缺少時會默默變 0；多個 Domain 錯誤回哪一個沒寫 | `errors: { [field]: string[] }`；`InvalidModelStateResponseFactory` 統一轉成 `VALIDATION_FAILED`；`Version` 用 `uint?` + `[Required]`；Domain 驗證回第一個錯誤並寫出順序 |
| R-M7 | CD M7、IR M6 | 2.5 `AntiforgerySetup` | MVC antiforgery 失敗預設回空的 400，不是 `CSRF_INVALID`；`UseAntiforgery()` 不適用 JSON API；token 綁使用者，登入後要重新取得 | 自訂 filter：排在授權之後，非安全方法呼叫 `ValidateRequestAsync`，攔截例外輸出 `CSRF_INVALID`；`/api/me` 用 `GetAndStoreTokens` 寫 `XSRF-TOKEN` cookie（非 HttpOnly） |
| R-M8 | IR M7、CD M6、ST N15 | 1.1、1.7、1.6 | 代碼值在 C#／JSON／資料庫怎麼表示、snake_case 用哪種做法，都留給工程師；Data Protection 表名預設不是 snake_case | C# enum + `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)` + EF `HasConversion<string>`；兩個 DbContext 都用 `EFCore.NamingConventions` 的 `UseSnakeCaseNamingConvention()`；不同的 migration 歷史表；不認得的代碼回 `VALIDATION_FAILED` |
| R-M9 | IR M8、ST M7 | 1.4、2.3 | 「滿 30 天」是時間點還是日曆日；`deletedAt` 是時間點卻寫「日期一律 `YYYY-MM-DD`」；查詢方法沒有 cutoff 參數；時間戳記誰寫；畫面「將於 X 永久刪除」和實際行為不符 | `cutoff = UtcNow − 30 天`、`deleted_at <= cutoff`；時間點用 ISO 8601 UTC；關閉 deleted 過濾器的方法都帶 cutoff；時間戳記由 `TimeProvider` 寫入；畫面改「X 後自動移除」 |
| R-M10 | IR M9 | 2.7 | `FakeTimeProvider` 往後調 31 天，14 天的 cookie 會過期，30 天測試回 401 | (a) 調整時間後重新登入；或 (b) 直接改資料庫的 `deleted_at`，`FakeTimeProvider` 只用來固定「今天」 |
| R-M11 | IR M10 | 2.1 | 環境變數的確切名稱和 `ALLOWED_EMAILS` 格式沒寫 | 2.1 加一張表：`GOOGLE_CLIENT_ID`、`GOOGLE_CLIENT_SECRET`、`POSTGRES_USER`／`PASSWORD`／`DB`、`ConnectionStrings__Default`、`MASTER_KEY`、`ALLOWED_EMAILS`（逗號分隔，去空白轉小寫後比對）、`GIT_SHA`、`POSTGRES_IMAGE` |
| R-M12 | IR M11 | 2.1 | Compose 不能讀 `postgres-image.txt`；CUJ 10 直接 `docker compose up -d api` 時變數沒值 | `deploy.sh` 匯出 `POSTGRES_IMAGE`；compose 用 `${POSTGRES_IMAGE:?run deploy.sh}`、`${GIT_SHA:-unknown}`；README 註明 |
| R-M13 | IR M12 | 2.1、2.4 | 怎麼產生 migration 沒寫；啟動驗證會擋住 design-time | 加 `.config/dotnet-tools.json`、兩個 `IDesignTimeDbContextFactory`、`dotnet ef migrations add` 指令 |
| R-M14 | IR M13 | 1.2、2.6 | 各法規的修正日期和 `EffectiveFrom` 沒有確切值，例子 `111-04-01` 未確認；法規事實不該由工程師決定 | 1.2 加一張表：法規名稱、條號、修正日期（民國、西元）、對應規則 ID（**需要作者提供或確認**） |
| R-M15 | IR M14 | 2.7 | 測試登入端點直接發 cookie，`OnTokenValidated` 的名單檢查（CUJ 2）和 `email_verified = false` 沒有整合測試 | 抽出 `Auth/OidcSignInHandler.cs`，測試登入端點呼叫同一個類別；`AuthTests` 加這兩個案例 |
| R-M16 | IR M15、ST | Verification Plan | 手動驗證要求固定在 2026-11-19，正式程式沒有辦法做到 | 確切日期交給 G9-1 單元測試；手動驗證只檢查畫面（標籤、顏色、推算標示、免責聲明、排序、錯誤訊息） |
| R-M17 | IR M16 | 2.8 | 日期下拉選單的年份範圍、日不存在時的處理、民國／西元顯示格式、`calendar.spec.ts` 的案例都沒定義 | 出廠和原發照 1950 到今年、指定檢驗日期到今年 + 2；日不存在時夾到月底；民國 `115/11/19`、西元 `2026/11/19`；列出最少案例 |
| R-M18 | CD M4、IR N18 | 2.4 `Crypto/` | .NET 8 起 `new AesGcm(key)` 過時，`TreatWarningsAsErrors` 會編譯失敗；格式的長度和版本值沒寫 | `new AesGcm(key, tagSizeInBytes: 16)`、nonce 12 bytes 用 `RandomNumberGenerator.Fill`、版本 `0x01`；`HKDF.DeriveKey(SHA256, ikm, 32, salt: [], info)` |
| R-M19 | CD M5 | 2.4 `DataProtection/` | 自訂 `IXmlDecryptor` 是框架用 `IServiceProvider` 建構式建立，不是 DI 注入；缺少套件 | decryptor 提供 `(IServiceProvider)` 建構式；`Encrypt` 回傳 `EncryptedXmlInfo(..., typeof(AesGcmXmlDecryptor))`；`SetApplicationName("VehicleGo")`；加 `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`，`DataProtectionKeyContext` 實作 `IDataProtectionKeyContext` |
| R-M20 | ST M1–M4、M9 | 1.2、1.3、2.6 | 「指定日期」不符術語；規則表儲存格和 1.2 下方段落太長；虛擬碼 `addYears` 和 `AddYears` 撞名、`k` 未定義；測試命名例子有連字號，不是合法的 C# 名稱 | 改「指定檢驗日期」；1.2 段落拆成條列；改成 `gapYears = GapYears(m, issue)`；例子改 `RTS44_20220401_...`（日期待 R-M14 確認） |
| R-M21 | ST M5、M6 | 2.3 | `UNCOMPUTABLE` 被當成 log 事件名稱但沒有測試；`PurgeVehicleCommand`（使用者永久刪除）和 `PurgeExpiredTrashAsync`（自動清除）共用「Purge」 | log 事件改 `INSPECTION_UNCOMPUTABLE` 並補測試，或刪掉；`PurgeVehicleCommand` 改名 `DeleteVehicleCommand` |
| R-M22 | ST M8、IR N16 | 2.6 | G9-1 要求每個 CUJ 的確切結果都有單元測試，但測試清單缺 CUJ 5、6；CUJ 4、4b 放在汽車測試，不在指定檢驗日期測試 | 機車測試加 CUJ 5、6；CUJ 4、4b 移到 `LicenceDateTests` |

## Nit（摘要，處理時依各審查者原文）

*   **計算細節（IR）：** `age >= N` 的定義；推算期間寫成 `E = A.AddMonths(6)` 再 ±1 個月（CD N1 同）；「最早的期間」依最後期限排序、產生到第一個符合的就停；L 早於第一次公式日期時怎麼判斷不一致；`AGE10UP` 的 `estimated` 寫法。
*   **API 細節（IR）：** PUT 的錯誤檢查順序；不合法的 `sort`；正規化後的空車牌和長度限制；log 欄位和測試擷取方式；`/vehicles/:id` 就是詳細頁並顯示免責聲明；隔離測試補列表端點；具名過濾器的使用者 ID 來源。
*   **建置與工具（IR、CD）：** `.slnx` 或 `--format sln`；`web/package-lock.json`；每一步都要能建置、CI 提早；CI 的 gitleaks 下載和 SHA-256 驗證步驟；`ExecuteDeleteAsync` 也要 `IgnoreQueryFilters`；tracked 實體才能設 `OriginalValue`；upsert 用原生 SQL；OIDC `Scope.Clear()`、`DefaultChallengeScheme` 必須是 Cookie、http 只限 `localhost`；Vite proxy 不設 `changeOrigin`；健康檢查自訂 `ResponseWriter` 和 DB 檢查套件、`--healthcheck` 的 port；映像檔 tag `10.0-noble`；NuGetAudit 警告和 `TreatWarningsAsErrors`；`.gitleaks.toml` 用 `[[allowlists]]`；官方 hook 寫法 `gitleaks git --pre-commit --staged --redact`；`tsconfig` 和 `vue-tsc`、Vitest 型別；Element Plus 按需註冊的做法；ArchUnitNET 套件名稱和 xUnit 版本。
*   **文字（ST）：** 成功標準寫法統一 `G9-*`；代碼加中文對照；括號和儲存格過長；「期間」寫全；重複說明；縮排和標點；「月底往前夾」改說法；`earliestDate` = 期間起日；錯誤欄寫法統一；過濾器名稱；`--staged` 參數名稱；`.gitleaksignore` 句子；「同時第一次登入」的說法；`ReturnUrlValidator` 位置；`UpdateVehicleRequest` 的定義；「回傳 `gitSha`」。

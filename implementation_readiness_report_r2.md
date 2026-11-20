# 第 9 階段：Implementation Readiness 報告（第 2 輪重審）

**Status:** resolved（2026-11-20）

**作者決定（逐項確認）：** 9 個 Major 全部接受。R2-M1 選 (a)：取出車輛後先比對版本號；R2-M3 選 (a)：刪除 `INVALID_CALENDAR`，改回 `VALIDATION_FAILED`；R2-M9 選 (a)：維持 `aspnet:10.0-noble`，Dockerfile 加 `USER $APP_UID`。Nit 分三組（流程與定義、技術細節、文字）全部接受。已套用到設計文件。

*   **審查對象：** `docs/designs/vehicle_management.md` 第 742–1242 行（第 1 輪後重寫的詳細實作計畫）
*   **審查者：** egm-implementation-readiness、egm-codebase-details、egm-style
*   **日期：** 2026-11-19
*   **輪次：** 第 9 階段的 1 次重審額度已用完。第 1 輪報告保留在 `implementation_readiness_report.md`。

## 總評

**接近可以實作：0 個 Blocker、9 個 Major（已合併重複）、約 35 個 Nit。**

*   第 1 輪的 22 個 Major，20 個已解決；R-M8（代碼值的表示方式）和 R-M13（migration）只解決一部分，剩下的部分併入本輪的 R2-M2、R2-M5。
*   implementation-readiness 審查者重新手算 CUJ 3、4、4b、4c、5（兩種檢驗）、6、12，期間起日、最後期限、狀態、`basis`、`estimated`、規則 ID 全部和 CUJ 一致。
*   codebase-details 確認第 1 輪修正的技術寫法大多正確（原生 SQL 鎖定、具名過濾器、`xmin`、HKDF、AesGcm、Data Protection 解密器、健康檢查、`.slnx`、gitleaks、xUnit v3 套件、Vite 設定等）。
*   沒有已移除概念的殘留；術語使用一致。

剩下的 Major 是改寫後出現的前後矛盾，以及幾處照字面實作會失敗或測試寫不出來的地方。

來源：`IR` = implementation-readiness、`CD` = codebase-details、`ST` = style。

## Major

| 編號 | 來源 | 位置 | 問題 | 建議 |
| --- | --- | --- | --- | --- |
| R2-M1 | ST M1、IR N-M1、CD M3 | 1.4（PUT 檢查順序）對照 2.3 流程、Technical Plan 第 4 節 | 1.4 規定 `VEHICLE_MODIFIED` 在車牌重複之前，但流程先查車牌、儲存時才發現版本號不一致；舊版本號加重複車牌時兩邊回的代碼不同 | (a) 取出車輛後先比對版本號，`SaveAsync` 的檢查只當後備；或 (b) 改 1.4 的順序。`ConcurrencyTests` 加這個案例 |
| R2-M2 | IR N-M2、CD M1、M2 | 1.1、2.4 | `JsonNamingPolicy.SnakeCaseUpper` 不會在數字前加底線（`CarLe500` → `CAR_LE500`）；EF 的 `HasConversion<string>` 存 `HeavyMc`，和 check constraint、預設值 `'ROC'` 不符；`JsonStringEnumConverter` 預設接受數字 | 每個 enum 成員加 `[JsonStringEnumMemberName("CAR_LE_500")]`；JSON 用 `allowIntegerValues: false`；EF 用共用的 `ValueConverter` 讀同一份對照，在 `ConfigureConventions` 統一套用；測試逐一確認每個代碼來回轉換 |
| R2-M3 | ST M2、IR N-M3 | 1.1、1.4、1.5 | 不認得的代碼在 JSON 解析時就回 `VALIDATION_FAILED`，所以 `INVALID_CALENDAR` 永遠不會出現，測試寫不出來 | (a) 刪掉 `INVALID_CALENDAR`，改回 `VALIDATION_FAILED`；或 (b) `calendar` 以字串接收，由 handler 驗證後回 `INVALID_CALENDAR` |
| R2-M4 | IR N-M4 | 1.3、2.6、2.7 | `RuleTable.Current()` 是靜態的，測試無法讓某種檢驗缺規則，`NO_RULE` 測不到；`InvalidInput` 檢查整台車，做不到「以單一檢驗為單位」；`INVALID_INPUT` 的條件和資料庫 check constraint 相同，整合測試觸發不了 `INSPECTION_UNCOMPUTABLE` log | `Calculate(vehicle, today, rules)`；Application 透過 `IRuleTableProvider` 取得規則，測試可替換；改寫為「`NO_RULE` 以單一檢驗為單位；`INVALID_INPUT` 影響整台車」；log 測試用替換過的規則表觸發 |
| R2-M5 | IR N-M5、CD M7 | 2.4 | migration 指令照抄會失敗：缺 `Microsoft.EntityFrameworkCore.Design` 套件、沒指定 startup project、沒先 `dotnet tool restore`；design-time factory 要給假的連線字串和假的 `ICurrentUser` | 補套件（`PrivateAssets=all`）、`--startup-project src/VehicleGo.Infrastructure`、`dotnet tool restore`；寫明 factory 的內容 |
| R2-M6 | IR N-M6 | 2.3 | `SaveChangesAsync` 由誰呼叫沒寫；`AddAsync` 回傳 `Task`，23505 的後備錯誤無處回傳；丟進垃圾桶、還原、永久刪除的流程沒寫 | 每個寫入方法自己呼叫 `SaveChangesAsync` 並轉換錯誤，`UnitOfWork` 只負責開交易和 commit／rollback；`AddAsync` 改回 `Task<Result>`；補上三個命令的流程 |
| R2-M7 | CD M4 | 1.4 | 只修了 `version`；`manufactureDate`、`originalIssueDate`、`vehicleType` 若宣告成非 nullable 的值型別，沒帶時會默默變成 `0001-01-01` 或 enum 第一個成員，不會回 `VALIDATION_FAILED` | 所有必填的值型別都用 nullable 加 `[Required]`（和 `version` 一致） |
| R2-M8 | CD M5 | 1.4、2.5、2.7 | `errors` 的 key 預設是 C# 屬性名稱（`Version`）或 JSON 路徑（`$.manufactureDate`），不是 camelCase；測試「`errors` 含 `version`」不會通過 | 加 `SystemTextJsonValidationMetadataProvider`；`InvalidModelStateResponseFactory` 去掉 `$.` 前綴、丟掉參數名稱那一筆；1.4 寫明 key 用 camelCase |
| R2-M9 | CD M6 | 2.1 `Dockerfile` | `aspnet:10.0-noble` 有建立 `app` 使用者但預設仍是 root，「預設以非 root 執行」是錯的 | Dockerfile 的 runtime 階段寫 `USER $APP_UID` |

## Nit（摘要）

*   **流程與定義（IR、ST）：** 2.3 的步驟 2–7 才在交易裡，步驟 1 和 8 不在；`Age(m, X)` 定義成回傳整數年數；「指定檢驗日期當天」的「當天」寫明指 L 或公式日期本身；`n = -1` 加註解；`deleted_at` 改成 `FakeTimeProvider` 現在時間的 31 天前，新增失敗時清除也會 rollback；前端「今年」用 `Intl.DateTimeFormat` 加 `Asia/Taipei` 取得，`calendar.spec.ts` 加年份範圍案例；排氣定檢補最後期限剛好等於「出廠日 + 5 年」的案例；`GetForOwnerAsync` 和其他方法的 `ownerId` 參數寫法統一；Technical Plan 第 9 節改寫 G9 對應。
*   **技術細節（CD、IR）：** migration 歷史表名稱要用 `MigrationsHistoryTable` 明確指定；`INSERT ... RETURNING id AS "Value"` 搭配 `ToListAsync()`；`sort` 綁成字串比對；`/health/live` 不檢查資料庫、`/health/ready` 用 tag 只跑資料庫檢查；`ASPNETCORE_HTTP_PORTS` 取第一個；`HeaderName` 用 `AddAntiforgery(o => ...)` 設定；cookie 事件和 OIDC 事件分開寫；`StartupValidation` 從 `IConfiguration` 讀設定；`AntiforgeryFilter` 寫明型別、註冊方式，FallbackPolicy 要求登入、登入和健康檢查加 `[AllowAnonymous]`；`ElMessage` 另外 import、`components.d.ts` 放 `src/`；xUnit v3 用 VSTest 需要的套件（`xunit.v3`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk`）；`CancellationToken`；415 的回應；`TryAddSingleton(TimeProvider.System)`。
*   **文字（ST）：** 「400 驗證錯誤」在表格上方定義，錯誤欄排序不代表檢查順序；驗證順序的標題改寫；`VEHICLE_NOT_FOUND` 補垃圾桶的情況；「同一帳號第一次登入的多個請求同時到達」；過長儲存格移到表格下方；CI 句子範圍；自動清除用「清除」不用「永久刪除」；規則版本表的欄名；G9 項目加上 `G9-1`–`G9-4` 編號；統一「出廠日期」「原發照日期」、註明「汽車 = 自用小客車」；零星用詞（見 style 原文表格）。

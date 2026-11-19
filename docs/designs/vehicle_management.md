# 個人車輛管理系統（第一版：驗車期間）

*   **Author:** tsengyuchen
*   **Status:** Proposed
*   **Date:** 2026-11-19

--------------------------------------------------------------------------------

## Overview

一個網頁版的個人車輛管理系統。使用者用 Google 帳號登入，登錄自己擁有的汽機車；系統依台灣法規，在每次讀取時算出每台車每種檢驗（定期檢驗、排氣定檢）的下一個期間，顯示在網頁上。

第一版只做「車輛資料 + 驗車期間」：
- 刻意不做通知、管理頁、帳號刪除和逾期判斷，目的是先證明「依法規算出驗車期間」這套規則算得對。
- 只開放給環境變數名單上的 Google 帳號。
- 只部署在作者本機的 Docker 當作測試環境，正式的 VPS 部署延後。

之後可能會加入保養紀錄、加油紀錄，但第一版不為之後的功能預先寫程式（不預建資料表、API 或畫面）。**例外是資料：作者想先收集、之後會用到的資料，第一版可以先收集。** 目前屬於這類的是排氣量級距：第一版的驗車規則用不到它，但作者決定必填並驗證，作為之後計算牌照稅的紀錄（審查第 5 階段 M1 已再次確認）。先收集資料會有驗證、欄位和日後資料遷移的成本；若是個資，還要符合「只收集必要資料」的原則，所以每筆要先收集的資料都要個別評估。

技術：ASP.NET Core Web API（後端採用 Clean Architecture、遵守 SOLID）+ Vue SPA + PostgreSQL，用 Docker Compose 部署；介面只有繁體中文。

## The Problem

台灣的汽機車有兩種強制檢驗，主管機關不同、規則不同，車主要自己記：

- **定期檢驗**（交通部，道路交通安全規則第 44 條）：自用小客車和大型重型機車（逾 250cc）出廠滿 5 年後每年至少一次，滿 10 年後每年至少兩次，要在行照上的「指定檢驗日期」前後一個月內完成。逾期會被處 900–1,800 元罰鍰，逾期 6 個月以上註銷牌照。
- **排氣定檢**（環境部，空氣污染防制法第 40 條）：燃油機車出廠滿 5 年後每年一次，要在原發照月份的前後一個月內完成（例如 3 月發照，期限是 2 月 1 日到 4 月 30 日）。

一個人有多台車時（例如一台汽車、一台機車、一台重機），每台車要做的檢驗種類、頻率和期間都不同，而且會隨車齡改變。監理單位和環保單位雖然會通知，但各自只通知自己的項目，車主看不到「我所有的車接下來各要驗什麼」。

**解法：** 使用者輸入行照上的資料（車種、排氣量、出廠日期、原發照日期，以及選填的指定檢驗日期），系統依法規算出每台車每種檢驗的下一個期間，並在進入期間時變色提示。

**這個解法的限制，使用者必須知道：**
- 系統沒有「已驗車」的紀錄，所以不判斷逾期，也不知道使用者是否已經驗過。
- 滿 10 年後每年第二次的期間，法規沒有公開算法，系統用「當年指定檢驗日期 + 6 個月」推算，並標示為推算。
- 系統不處理期限遇假日順延，顯示的期限可能比實際早幾天。
- 部分法規細節只做過網路查證（見 Open Questions），算出的日期僅供參考。

以上都會寫進畫面上的免責聲明：結果僅供參考，以行照、監理站和環保單位的正式通知為準。

## Goals

*   **G1 登入與名單：** 用 Google OIDC 登入，只申請 `openid email` 權限。只有環境變數 `ALLOWED_EMAILS` 名單上、而且 `email_verified = true` 的帳號能登入。每次請求都重新比對名單，被移出名單的人下一個請求就被登出。使用者以 Google 的 `sub` 識別。
*   **G2 車輛管理：** 使用者可以新增、修改自己的車輛，每人最多 50 台（含垃圾桶）。
    *   車種：自用小客車、一般燃油機車、大型重型機車、電動機車。
    *   欄位：車牌、廠牌、型號、暱稱（選填）、顏色（選填）、車種、排氣量級距（電動機車以外必填）、出廠日期、原發照日期、行照指定檢驗日期（選填，只限汽車和大型重機）。
    *   兩個人同時修改同一台車時，後存的一方會被擋下，並提示「資料已被修改，請重新整理」。
*   **G3 垃圾桶：** 刪除時先丟進垃圾桶，可以還原或永久刪除；丟進垃圾桶滿 30 天的車自動永久刪除（不需要背景排程，見技術計畫）。車牌在同一使用者底下唯一（含垃圾桶），重複時提示去垃圾桶還原。
*   **G4 驗車期間計算：** 驗車規則寫在程式裡，在讀取時算出每台車每種適用檢驗的下一個期間（期間起日、最後期限、狀態、規則 ID、日期來源、是否為推算），不存進資料庫。有填行照指定檢驗日期時，定期檢驗以它為準。
*   **G5 顯示：** 列表可依出廠日或最後期限排序。進入檢驗期間時日期變色。每個狀態都有文字標籤，不只靠顏色。列表上方和車輛詳細頁各顯示一次免責聲明。
*   **G6 日期：** 日期的輸入和顯示可用民國年或西元年，由使用者在個人設定選一次，預設民國；輸入用年、月、日三個下拉選單。時間點以 UTC 存放；日曆日期不轉時區；「今天」用台北時間。
*   **G7 資料保護：** 名單、Google 金鑰、資料庫密碼和 `MASTER_KEY` 都放在不進 git 的 `.env`。使用者的 Email 和登入金鑰都以 `MASTER_KEY` 推導出的金鑰加密存放。pre-commit hook 和 CI 都會檢查有沒有密鑰被 commit 進 repo。
*   **G8 部署：** 在本機用 Docker Compose 執行一支部署腳本完成部署。
*   **G9 成功標準：** GitHub Actions CI 中以下四類測試全部通過：
    1. **規則計算的單元測試。** 每個案例的名稱附上法規條號和修正日期，涵蓋第一次定檢、滿 5 年和 10 年的邊界、排氣定檢以月份計算、行照指定檢驗日期、推算期間，以及各車種的組合。
    2. **API + 真實 PostgreSQL 的整合測試。** 涵蓋登入名單、CSRF、錯誤代碼、同時編輯、車輛上限。
    3. **使用者資料隔離測試。** 使用者 A 不能讀寫使用者 B 的任何資料，包括車輛、垃圾桶和個人設定。
    4. **民國／西元換算和日期下拉選單的前端單元測試。**

## Non-Goals

*   Email、LINE、推播通知，以及任何應用程式內的背景排程。
*   管理頁面、角色與權限、在畫面上編輯規則；規則不存資料庫。
*   帳號刪除。有人要求刪除資料時，由作者人工處理。
*   「已逾期」狀態、登記已驗車、驗車紀錄、「即將進入檢驗期間」的提前提示。
*   手動覆寫驗車日期。例外：汽車和大型重機可以選填行照上的指定檢驗日期。
*   期限遇假日的順延計算。
*   牌照稅、燃料稅、保險、保養紀錄、加油紀錄。
*   電動大型重型機車（逾 40 馬力的電動機車）、營業用車、貨車等其他車種。
*   開放任何人註冊。
*   正式 VPS（k3s）部署、自動部署（CD）、正式的備份排程、更換 `MASTER_KEY`。
*   多語系。
*   前端元件測試和 E2E 測試（民國／西元換算的單元測試除外）。

## Open Questions

> 作者已確認（2026-11-19）：排氣定檢和定期檢驗是兩種不同的檢驗。燃油機車（白牌、黃牌、紅牌，二行程和四行程）出廠未滿 5 年免排氣定檢，滿 5 年起每年一次；純電動機車終身免排氣定檢。大型重機另外還有監理單位的定期檢驗，規則和自用小客車相同（滿 10 年後每年兩次）。

### A. 法規尚未完全查證（寫規則測試前完成）

*   **A1 監理定期檢驗規則（作者已確認，2026-11-19）：** 自用小客車和大型重型機車出廠未滿 5 年免驗；滿 5 年、未滿 10 年每年一次；滿 10 年起每年兩次；期限為行照指定檢驗日期前後一個月內。測試案例名稱仍要附上條號（道路交通安全規則第 44 條）和修正日期。
*   **A2 第一次定檢日的算法（作者決定直接採用，2026-11-19）：** 發照日 +（5 − 出廠到發照的完整間隔年數）年，「未逾 N 年」包含剛好 N 年，間隔 4 年以上一律 + 1 年；車齡用完整出廠日期計算。不另外用真實行照比對；如果日後發現和行照不一致，使用者可以填行照指定檢驗日期覆蓋，系統也會記 `LICENCE_DATE_MISMATCH` log 供修正規則。
*   **A3 汽車牌照稅級距（作者已確認，2026-11-19）：** 500cc 以下、501–600、601–1200、1201–1800、1801–2400、2401–3000、3001–4200、4201–5400、5401–6600、6601–7800、7801cc 以上，加上「電動」選項。
*   **A4 過渡條款（併入 B1，作者決定）：** 作者表示相關規則目前沒有修改過，所以沒有過渡條款要處理；第一版只放現行規則。
*   **A5 期間邊界（作者已確認，2026-11-19）：** 期間起日和最後期限當天都算在期間內（例如 2/10–4/10，兩天都顯示「檢驗期間內」）。

### B. 延後決定（有觸發條件）

*   **B1 規則生效日與版本選擇（含原 A4 過渡條款）：** 第一版只放現行規則。觸發：法規修正時，依當時的過渡條款決定用哪個日期判斷新舊規則（今天、原發照日或出廠日）。第一版已預留每條規則的 `EffectiveFrom` 欄位和 `RuleTable.Current()` 選擇函式，但不寫過渡邏輯。
*   **B2 正式 VPS 部署（作者決定維持延後，2026-11-19）：** 作者還沒確定會部署到正式機，這個系統可能只會是實驗專案。VPS 是 k3s，前面有 Cloudflare 代理和原點憑證；分支安排、反向代理與 forwarded headers、自動部署、正式備份排程、獨立的 migration 步驟（migration bundle）、更換金鑰的流程，都等真的決定部署時再處理，也可能永遠不做。

### C. 本文件後續章節要由作者決定的事項

*   **C1（作者已決定，2026-11-19）** 後端分成四個專案：Domain、Application、Infrastructure、Web，依賴方向由專案參考強制（每層放什麼在技術計畫寫明）。Clean Architecture 本身只規定「依賴由外往內」，沒有規定要拆專案；拆成四個專案是為了讓編譯器強制這條規則。
*   **C2（作者已決定，2026-11-19）** 寫入用 Repository、讀取用查詢服務：新增、修改、丟進垃圾桶、還原、永久刪除透過 Application 定義的 Repository 介面（車輛、使用者），讀取（車輛列表、單台車、垃圾桶列表）透過 Application 定義的查詢介面直接回傳畫面需要的資料；例外：使用者只有曆法偏好一個讀取欄位，`/api/me` 的讀取併入 `IUserRepository`，不另設查詢介面（審查第 5 階段 M4）；兩者都由 Infrastructure 用 EF Core 實作。Application 專案不參考 EF Core。
*   **C3（作者已決定，2026-11-19）** 使用案例用自己定義的 handler 介面組織：Application 定義 `ICommandHandler<TCommand>`、`IQueryHandler<TQuery, TResult>`，每個使用案例一個實作類別，用 ASP.NET Core 內建 DI 註冊，API 端點注入對應的 handler。不使用 MediatR 等外部套件。第一版沒有 handler pipeline，也沒有共同處理（審查第 5 階段 M4：不預留 decorator 等擴充點）。
*   **C4（作者已決定，2026-11-19）測試專案：** 兩個。`UnitTests` 放不需要資料庫的測試（Domain 規則引擎、Application handler、Infrastructure 的加密與車牌正規化）和架構測試；`IntegrationTests` 放 API + PostgreSQL（Testcontainers）的整合測試和資料隔離測試。已知取捨：架構測試沒辦法在 CI 上設成獨立的必要檢查。
*   **C6（作者已決定，2026-11-19）** API 用 Controller，不用 Minimal API。
*   **C7（2026-11-19）技術選擇：** .NET 10（EF Core 10 具名查詢過濾器需要）；前端 TypeScript + Vue Router（CI 要求 `typecheck`）。以下由作者決定：前端狀態管理用 Vue 內建 composable（不用 Pinia）；UI 元件庫用 Element Plus；後端測試框架用 xUnit；架構測試用 ArchUnitNET；結構化 log 用 .NET 內建的 JSON console logger（不用 Serilog）。
*   **C5 命名（機械性選擇，先採用以下預設，作者可否決）：** 方案和根命名空間 `VehicleGo`（取自 repo 名稱）；專案 `VehicleGo.Domain`、`VehicleGo.Application`、`VehicleGo.Infrastructure`、`VehicleGo.Web`、`VehicleGo.UnitTests`、`VehicleGo.IntegrationTests`；資料夾 `src/`（後端）、`tests/`（後端測試）、`web/`（Vue 前端）。

## Critical User Journeys (CUJs)

### CUJ 1：第一次登入

*   **Given**：使用者的 Google 帳號 Email 在 `ALLOWED_EMAILS` 名單上，且已經過 Google 驗證，從來沒有登入過。
*   **When**：使用者打開網站，按「用 Google 登入」，在 Google 完成驗證。
*   **Then**：系統建立使用者資料（Google `sub`、加密的 Email，曆法預設民國），導回車輛列表。列表是空的，顯示「新增車輛」按鈕。

### CUJ 2：不在名單上的人嘗試登入

*   **Given**：使用者的 Google 帳號不在名單上。
*   **When**：使用者完成 Google 登入。
*   **Then**：系統顯示「此帳號尚未開放使用」，不建立使用者資料，也不自動重新導向 Google。

### CUJ 3：新增一台車齡超過 10 年、沒有填行照日期的汽車

*   **Given**：今天是 2026-11-19（民國 115/11/19）。使用者已登入，曆法設定為民國。
*   **When**：新增一台自用小客車，排氣量選「1201–1800cc」，出廠日期 103/05/01，原發照日期 103/08/20，行照指定檢驗日期不填。
*   **Then**：列表上這台車的「定期檢驗」顯示 **116/01/20 – 116/03/20**，狀態「下次期間」，旁邊標示「推算日期，請以行照為準」，日期不變色。
    *   計算過程：出廠到發照相隔未滿 1 年，第一次指定日期 = 108/08/20；113/05/01 滿 10 年，之後每年兩次；今年的指定日期 115/08/20 的期間（115/07/20–115/09/20）已經結束；下一個是推算的 115/08/20 + 6 個月 = 116/02/20，期間 116/01/20–116/03/20。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2027-01-20`、`deadline = 2027-03-20`、`basis = FORMULA`、`estimated = true`。

### CUJ 4：填了行照指定檢驗日期的汽車

*   **Given**：今天是 2026-11-19。使用者有一台自用小客車，出廠日期 108/06/01，原發照日期 108/07/15（車齡約 7 年），行照指定檢驗日期原本沒填。
*   **When**：使用者在編輯頁填入行照上的指定檢驗日期 116/03/10 並儲存。
*   **Then**：定期檢驗顯示 **116/02/10 – 116/04/10**，狀態「下次期間」，旁邊標示「依行照指定檢驗日期」。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2027-02-10`、`deadline = 2027-04-10`、`basis = LICENCE`、`estimated = false`。
    *   公式算出的指定日期是每年 7/15（第一次 113/07/15），和行照的 3/10 月日不同，而且車齡未滿 10 年，不比對 + 6 個月，所以判定不一致：儲存時伺服器寫一筆 `LICENCE_DATE_MISMATCH` log，畫面仍以行照日期為準。

### CUJ 5：新增一台大型重機

*   **Given**：今天是 2026-11-19。使用者已登入。
*   **When**：新增一台大型重型機車，排氣量選「551cc 以上」，出廠日期 108/09/01，原發照日期 108/11/25。
*   **Then**：列表上這台車有兩行，兩行都在檢驗期間內，日期都變色：
    *   「定期檢驗」：**115/10/25 – 115/12/25**，狀態「檢驗期間內」。第一次指定日期 113/11/25（相隔未滿 1 年），今年是 115/11/25；車齡未滿 10 年，沒有推算期間。API：`kind = PERIODIC`、`status = IN_WINDOW`、`earliestDate = 2026-10-25`、`deadline = 2026-12-25`、`basis = FORMULA`、`estimated = false`。
    *   「排氣定檢」：**115/10/01 – 115/12/31**，狀態「檢驗期間內」。原發照月份 11 月，期間是前一個月 1 日到後一個月最後一天；出廠已滿 5 年。API：`kind = EXHAUST`、`status = IN_WINDOW`、`earliestDate = 2026-10-01`、`deadline = 2026-12-31`、`basis = FORMULA`、`estimated = false`。

### CUJ 6：新增一台電動機車

*   **Given**：今天是 2026-11-19。使用者已登入。
*   **When**：新增一台電動機車，出廠日期 110/03/01，原發照日期 110/03/15。表單上不顯示排氣量和行照指定檢驗日期欄位。
*   **Then**：列表顯示「排氣定檢：免檢」，不變色。API：`kind = EXHAUST`、`status = EXEMPT`、`earliestDate` 和 `deadline` 為 null。依最後期限排序時，這台車排在有日期的車後面。

### CUJ 7：誤刪後還原

*   **Given**：使用者有一台車牌 ABC-1234 的車。
*   **When**：使用者把它丟進垃圾桶，接著又新增一台車牌 `abc1234` 的車。
*   **Then**：系統擋下這次新增，顯示「此車牌已在垃圾桶中，請至垃圾桶還原」，並提醒還原會帶回舊資料、剛填的內容不會保留。使用者到垃圾桶按「還原」後，車輛回到列表。

### CUJ 8：達到車輛上限

*   **Given**：使用者已有 50 台車，其中 3 台在垃圾桶裡。
*   **When**：使用者新增第 51 台。
*   **Then**：系統擋下這次新增，提示「已達 50 台車輛上限（其中垃圾桶 3 台）。請永久刪除不需要的車輛」。

### CUJ 9：同時修改同一台車

*   **Given**：使用者在兩個分頁打開同一台車的編輯頁。
*   **When**：先在分頁 A 儲存，再在分頁 B 儲存。
*   **Then**：分頁 B 的儲存被擋下，顯示「資料已被修改，請重新整理」。

### CUJ 10：被移出名單

*   **Given**：使用者已登入，正在使用系統。
*   **When**：作者從 `.env` 移除他的 Email，並執行 `docker compose up -d api` 重建容器。
*   **Then**：使用者的下一個請求收到 401，SPA 導向登入頁。他再次登入時，看到「此帳號尚未開放使用」。

### CUJ 11：切換曆法

*   **Given**：使用者已登入，曆法設定為民國。
*   **When**：在設定頁改成西元。
*   **Then**：所有頁面的日期顯示和輸入都改用西元。換一台裝置登入時，仍然是西元。

## Technical Plan (Architecture)

### 1. 整體架構

後端採用 Clean Architecture，分成四個 .NET 專案，依賴只能由外往內，並由專案參考強制（作者決定 C1）。前端是獨立的 Vue SPA，建置後的靜態檔由 Web 專案提供，所以前後端在同一個網域。

```text
 瀏覽器（Vue SPA）
     │  本機 http://localhost:<port>；同網域 cookie + XSRF header
     ▼
┌──────────────────────── VehicleGo.Web ────────────────────────┐
│ Controllers、OIDC + Cookie 登入、名單檢查掛勾、CSRF、          │
│ ProblemDetails、健康檢查、靜態檔與 SPA fallback、相依性組裝     │
└───────────────┬───────────────────────────────┬───────────────┘
                │ 呼叫 handler                    │ 只在組裝相依性時參考
                ▼                                 ▼
┌──── VehicleGo.Application ────┐   ┌──── VehicleGo.Infrastructure ────┐
│ 命令／查詢 handler、           │◄──│ EF Core + PostgreSQL 實作         │
│ Repository／查詢介面、         │實作│ Repository 與查詢服務、加密、     │
│ 名單檢查、錯誤代碼             │   │ Data Protection 金鑰存放          │
└───────────────┬───────────────┘   └──────────────┬───────────────────┘
                ▼                                    │
┌──────────── VehicleGo.Domain ────────────┐◄──────────┘
│ 實體、車種與級距、車牌正規化、             │
│ 驗車規則引擎（純函式）                     │
└───────────────────────────────────────────┘
                                         PostgreSQL（Docker Compose 的 db 服務）
```

### 2. 各層內容

**VehicleGo.Domain（不參考任何其他專案或框架套件）**
- **實體與值物件：** `Vehicle`（車牌、正規化車牌、廠牌、型號、暱稱、顏色、車種、排氣量級距、出廠日期、原發照日期、行照指定檢驗日期、刪除時間、擁有者 ID）、`User`。
- **固定代碼：** 車種（`CAR`、`FUEL_MC`、`HEAVY_MC`、`ELECTRIC_MC`）、排氣量級距（汽車 11 級加上 `CAR_ELECTRIC`；機車 `MC_LE_50`、`MC_51_250`、`MC_251_550`、`MC_GT_550`）、曆法（`ROC`、`GREGORIAN`）。級距清單附法規出處，並定義「車種 → 可用級距」對照表。
- **車牌正規化：** NFKC 全形轉半形、轉大寫、去掉空白和連字號。
- **驗車規則引擎：** 純函式，輸入（車輛資料、今天的日期），輸出檢驗項目清單。每一項包含：種類、狀態、期間起日、最後期限、規則 ID、日期來源、是否推算，以及只給伺服器使用的診斷資訊。規則表和期間算法（以發照月日、以發照月份、以行照指定檢驗日期、每年兩次 + 6 個月推算、免檢）都在這裡，用型別或 enum 表示。
- **領域規則：** 車輛上限 50、輸入驗證（日期先後、級距與車種相符、行照日期只限汽車和大型重機），回傳固定的錯誤代碼，不拋出框架例外。

**VehicleGo.Application（只參考 Domain）**
- **handler 介面（作者決定 C3）：** `ICommandHandler<TCommand>`、`IQueryHandler<TQuery, TResult>`，每個使用案例一個實作類別。結果用統一的型別表示成功，或失敗並附錯誤代碼。
- **命令（寫入，走 Repository，作者決定 C2）：** 新增車輛、修改車輛（帶版本號）、丟進垃圾桶、還原、永久刪除、修改曆法偏好、登入時建立或更新使用者。
- **查詢（讀取，走查詢服務）：** 車輛列表（含排序）、單台車、垃圾桶列表、目前使用者（`/api/me`）、級距清單。車輛列表和單台車的查詢服務只負責取出資料；檢驗期間由 handler 呼叫 Domain 的規則引擎計算後再組成回應，所以查詢服務本身不需要知道規則。
- **對外介面（由 Infrastructure 實作）：** `IVehicleRepository`、`IUserRepository`、`IVehicleQueries`、`IEmailProtector`（加密 Email、算查詢雜湊）、`ICurrentUser`（目前登入者 ID）。時間一律透過 .NET 的 `TimeProvider` 取得，再換算成台北時間的日期。
- **名單檢查：** `LoginAdmission`（比對 `ALLOWED_EMAILS`、要求 `email_verified = true`），由 Web 的 OIDC 事件和 cookie 驗證事件共同呼叫。
- **行照日期不一致的判斷：** 新增和修改車輛的 handler 讀取規則引擎回傳的診斷資訊，決定是否寫 `LICENCE_DATE_MISMATCH` log；讀取時不寫。

**VehicleGo.Infrastructure（參考 Application、Domain）**
- **資料庫：** EF Core + Npgsql 的 `AppDbContext`，兩個具名查詢過濾器（擁有者、已刪除）；`xmin` 當作同時編輯檢查的版本號；`(owner_id, normalized_plate)` 唯一索引；唯一索引衝突轉成 `PLATE_EXISTS_ACTIVE` 或 `PLATE_EXISTS_IN_TRASH`。
- **垃圾桶自動清空（作者決定，審查第 5 階段 M3）：** 新增車輛和登入（建立或更新使用者）時，先永久刪除目前使用者 `deleted_at` 早於「現在 − 30 天」的車。「現在」由 `TimeProvider` 傳入，不用 SQL 的 `now()`，測試才能控制時間。讀取（垃圾桶列表、單台車、還原、永久刪除）時，已滿 30 天的車視為不存在，所以就算還沒被實際刪除，使用者也看不到或操作不到。GET 不做刪除，避免讀取有副作用。
- **Repository 與查詢服務：** 實作 Application 的三個介面（`IVehicleRepository`、`IUserRepository`、`IVehicleQueries`）。垃圾桶相關查詢和車輛數量只關閉「已刪除」過濾器，不能關閉擁有者過濾器。
- **加密：** 從 `MASTER_KEY` 用 HKDF 推導三把金鑰；共用的 AES-GCM 工具；`IEmailProtector` 的實作。
- **Data Protection：** 獨立的小 DbContext 存放登入金鑰，加上自行實作的 XML 加密器和解密器。
- **migration：** EF Core migration（`AppDbContext` 和 Data Protection 的 DbContext 各一組），第一版在 `api` 啟動時自動套用（審查第 5 階段 N4）。部署到 VPS 時，要改成獨立的 migration 步驟（多個執行個體同時在啟動時套用 migration 會互相衝突），列在 B2。

**VehicleGo.Web（參考 Application；組裝相依性時參考 Infrastructure）**
- **Controllers（作者決定採用 Controller）：** `AuthController`（登入、登出）、`MeController`（讀取使用者、修改偏好）、`VehiclesController`（列表、單台、新增、修改、丟進垃圾桶、還原、永久刪除）、`TrashController`（垃圾桶列表）、`ReferenceController`（級距清單）。每個 action 只呼叫一個 handler，handler 用 `[FromServices]` 注入在 action 方法上，避免建構子變長。
- **登入：** 預設的驗證方式是 cookie，Google 透過 OIDC 登入；`/api` 未登入時回傳 401，不轉址；cookie 驗證事件每次請求都重新檢查名單。
- **CSRF：** 用框架內建的 `IAntiforgery`。先檢查登入，再檢查 CSRF。
- **錯誤處理：** Application 回傳的錯誤代碼，在這一層統一轉成 ProblemDetails 和 HTTP 狀態碼。
- **其他：** 健康檢查（`/health/live`、`/health/ready`）；提供 Vue 建置後的靜態檔，並把非 `/api` 的路徑導向 `index.html`；啟動時檢查 `MASTER_KEY`、Asia/Taipei 時區、`ALLOWED_EMAILS`。正式程式碼裡**沒有**任何測試用的登入端點（審查第 5 階段 M5）；整合測試需要的登入端點只存在於 `IntegrationTests` 專案。

**前端（`web/`，Vue SPA）**
- **頁面：** 登入、此帳號尚未開放使用、車輛列表、新增與編輯車輛、垃圾桶、設定（曆法）。
- **共用模組：**
  - API 呼叫：自動帶 `X-XSRF-TOKEN`；收到 401 時導向登入頁；收到 `CSRF_INVALID` 時重新取得 token 後重試一次。
  - 民國／西元換算和日期下拉選單的邏輯，獨立成一個模組，用 Vitest 測試。
- 前端只依 API 回傳的 `status`、`basis`、`estimated` 決定顏色和標示，**不做任何驗車日期計算**。
- 是否使用 TypeScript、狀態管理、UI 元件庫，在詳細實作計畫中由作者決定。

### 3. 主要資料流

**讀取車輛列表**
```text
SPA ── GET /api/vehicles?sort=deadline ──► VehiclesController
  → ListVehiclesQueryHandler
      → IVehicleQueries：取出目前使用者未刪除的車（擁有者 + 已刪除過濾器）
      → TimeProvider：取得今天（台北時間）
      → Domain 規則引擎：每台車算出檢驗項目清單
      → 在記憶體依最早的最後期限排序（免檢和無法計算排最後，同日依車牌）
  ← 200 車輛清單（含 inspections，不含診斷資訊）
```

**新增車輛**
```text
SPA ── POST /api/vehicles（帶 X-XSRF-TOKEN）──► 登入檢查 → CSRF 檢查 → VehiclesController
  → CreateVehicleCommandHandler
      → Domain 驗證（日期、級距與車種、行照日期）── 失敗 → 400 + 錯誤代碼
      → IVehicleRepository：先永久刪除自己滿 30 天的垃圾桶車輛
      → 同一個交易：鎖住使用者那一列（SELECT ... FOR UPDATE），再計算車輛數（含垃圾桶）≥ 50 → 409 VEHICLE_LIMIT_REACHED
        （同一使用者同時送出的新增會排隊執行，上限精確，不會變成 51 台）
      → 建立 Vehicle（正規化車牌）、儲存
          ── 唯一索引衝突 → 409 PLATE_EXISTS_ACTIVE / PLATE_EXISTS_IN_TRASH
      → 規則引擎診斷：行照日期與公式不一致 → 寫 LICENCE_DATE_MISMATCH log
  ← 201 新車輛（含 inspections、version）
```

**登入與每次請求的名單檢查**
```text
GET /api/auth/login?returnUrl=/ → OIDC Challenge → Google
Google 回呼 /signin-oidc → OnTokenValidated → LoginAdmission
    ├─ 不通過 → 導向「此帳號尚未開放使用」（不建立使用者）
    └─ 通過 → 建立或更新使用者（INSERT ... ON CONFLICT）、寫入登入 cookie（含 Email claim）
之後每個請求 → cookie 驗證事件 → LoginAdmission（比對 cookie 裡的 Email）
    └─ 已不在名單上 → 清除 cookie → 401
```

### 4. 資料模型（摘要，完整欄位在詳細實作計畫）

| 資料表 | 主要欄位 | 說明 |
| --- | --- | --- |
| `users` | 內部 ID、`google_sub`（唯一）、`email_encrypted`、`email_lookup_hash`（一般索引）、`calendar_preference`（預設 `ROC`）、建立和修改時間 | Email 只用來處理人工刪除資料的請求 |
| `vehicles` | 內部 ID、`owner_id`、`plate`、`normalized_plate`、廠牌、型號、暱稱、顏色、`vehicle_type`、`displacement_bracket`、`manufacture_date`、`original_issue_date`、`licence_inspection_date`、`deleted_at`、建立和修改時間、`xmin`（版本號） | `(owner_id, normalized_plate)` 唯一；check constraint 限制級距和行照日期只出現在對應的車種 |
| `data_protection_keys` | 框架定義的欄位 | 存在獨立的 DbContext，內容已加密 |

日曆日期（出廠、原發照、行照指定檢驗日期）用 `date`，時間點用 `timestamptz`。檢驗期間不存進資料庫。

### 5. API 一覽（完整的請求、回應和錯誤代碼在詳細實作計畫）

| 方法 | 路徑 | 用途 |
| --- | --- | --- |
| GET | `/api/auth/login?returnUrl=` | 開始 Google 登入 |
| POST | `/api/auth/logout` | 登出（只登出 cookie，回傳 204） |
| GET | `/api/me` | 目前使用者的 Email 和曆法設定；同時發出 XSRF token |
| PATCH | `/api/me/preferences` | 修改曆法設定 |
| GET | `/api/vehicles?sort=deadline\|manufactureDate` | 車輛列表（含檢驗項目） |
| GET | `/api/vehicles/{id}` | 單台車（含檢驗項目和版本號） |
| POST | `/api/vehicles` | 新增車輛 |
| PUT | `/api/vehicles/{id}` | 修改車輛（整筆取代，需要版本號） |
| POST | `/api/vehicles/{id}/trash` | 丟進垃圾桶 |
| POST | `/api/vehicles/{id}/restore` | 從垃圾桶還原 |
| DELETE | `/api/vehicles/{id}` | 永久刪除（只限垃圾桶裡的車） |
| GET | `/api/trash` | 垃圾桶列表（不含檢驗項目） |
| GET | `/api/reference/displacement-brackets` | 級距清單 |
| GET | `/health/live`、`/health/ready` | 健康檢查（不需登入） |

別人的車一律回傳 404。所有非 GET 的 `/api` 請求都要通過 CSRF 檢查。

### 6. 安全與設定

- 所有設定都從環境變數讀取，本機由 `.env` 提供：`ALLOWED_EMAILS`、Google Client ID 和 Secret、資料庫連線、`MASTER_KEY`。`.env` 不進 git，repo 裡只有 `.env.example`。
- Email 和登入金鑰都用 `MASTER_KEY` 推導出的金鑰，以 AES-GCM 加密。第一版不支援更換金鑰；金鑰遺失時清空資料庫重建。
- pre-commit hook 和 CI 都會執行 `check-secrets.sh` 和 gitleaks。
- log 不寫車牌，Email 只寫查詢雜湊的前 8 碼。

### 7. 部署（本機 Docker Compose）

| 服務 | 說明 |
| --- | --- |
| `db` | PostgreSQL，用 `pg_isready` 做健康檢查 |
| `api` | Web 專案（含 Vue 靜態檔），等 `db` 健康後才啟動，啟動時套用 migration，`restart: unless-stopped` |

部署腳本 `deploy.sh`：匯出 git SHA，執行 `docker compose up -d --build`。本機資料庫可以隨時清空重建，所以第一版不在部署時備份（審查第 5 階段 N4）；備份和 migration bundle 等部署 VPS 時再設計。

### 8. 測試對應

| 測試專案（作者決定 C4） | 內容 | 對應成功標準 |
| --- | --- | --- |
| `VehicleGo.UnitTests` | Domain 規則引擎、Application handler（用假的 Repository 和查詢服務）、Infrastructure 的加密、HKDF、車牌正規化；架構測試（檢查四個專案的依賴方向） | G9-1 |
| `VehicleGo.IntegrationTests` | Web + PostgreSQL（Testcontainers）：登入名單、CSRF、錯誤代碼、同時編輯、車輛上限、資料隔離 | G9-2、G9-3 |
| `web/`（Vitest） | 民國／西元換算、日期下拉選單 | G9-4 |

## Alternatives Considered

以下是在腦力激盪（`concepts_brief.md`）和本文件撰寫過程中考慮過、但沒有採用的做法。每一項列出優點、缺點和否決原因。標示「作者否決」的是作者的決定；「接受審查建議」表示作者採納了獨立審查者的意見。

### A. 產品範圍

#### A1：只寄 Email 提醒，不做網頁
*   **優點**：使用者不必主動上網站查。
*   **缺點**：需要背景排程和寄信服務，範圍變大。
*   **否決原因**：作者否決：範圍太大，第一版先驗證規則計算，通知留到之後。

#### A2：使用者自己輸入下次驗車日（取代公式計算）
*   **優點**：不用實作法規規則，不會算錯。
*   **缺點**：系統的價值和行事曆提醒差不多。
*   **否決原因**：作者否決：規則自動計算才是核心價值。後來加入的「行照指定檢驗日期」是選填、只用於定期檢驗，沒填仍用公式計算，不屬於這個做法。

#### A3：顯示「已逾期」，或加「上次驗車日」欄位
*   **優點**：可以提醒真正錯過驗車的人。
*   **缺點**：沒有驗車紀錄，就無法分辨「準時驗過」和「真的逾期」，會誤報。
*   **否決原因**：作者否決：沒有驗車紀錄就無法誠實判斷逾期，期限過後直接顯示下一個週期，並以免責聲明說明。

#### A3b：期限結束後 6 個月內顯示「上次檢驗期間已於 X 結束；如已完成請忽略」（`RECENTLY_ENDED`）
*   **優點**：在逾期罰款和註銷牌照前提供中性的提醒，不宣稱逾期、不變色。
*   **缺點**：多一種狀態、測試和畫面文字；已驗車的人也會看到最多 6 個月的提示。
*   **否決原因**：作者否決（審查第 5 階段 M8）：維持期限過後直接顯示下一個期間。

#### A4：「即將進入檢驗期間」的提前提示、到期前 2 週變色
*   **優點**：提早提醒。
*   **缺點**：多一種狀態；「到期前 2 週」會在使用者已經可以去驗車之後才變色，意義不大。
*   **否決原因**：作者否決：只有進入檢驗期間才變色。

#### A5：第一版不收排氣量、不支援大型重機
*   **優點**：範圍最小。
*   **缺點**：大型重機有兩種檢驗，排除後無法涵蓋常見的車種。
*   **否決原因**：作者否決：大型重型機車要納入第一版。

#### A6：開放任何 Google 帳號註冊
*   **優點**：任何人都能使用。
*   **缺點**：第一版沒有帳號刪除，個資進來後刪不掉。
*   **否決原因**：作者否決：改用環境變數的登入名單。

### B. 登入與帳號

#### B1：Email + 密碼，或 Magic link 登入
*   **優點**：不依賴 Google。
*   **缺點**：要處理密碼雜湊、重設密碼、登入信寄送和垃圾信問題。
*   **否決原因**：作者否決：改用 Google 登入。

#### B2：只限 `@gmail.com`
*   **優點**：規則簡單。
*   **缺點**：排除公司或學校的 Google Workspace 帳號。
*   **否決原因**：作者否決：任何 Google 帳號都可以，另外用登入名單限制。

#### B3：以 Email 識別使用者
*   **優點**：直覺。
*   **缺點**：Email 可能改變或被別人重新註冊，會造成資料遺失或外洩。
*   **否決原因**：作者否決：改用 Google 的 `sub` 識別。

#### B4：用 Google 同意畫面的「Testing」測試帳號當名單
*   **優點**：不用寫程式。
*   **缺點**：登入時顯示「未經驗證」警告；名單要到 Google Cloud Console 修改；沒辦法寫整合測試。
*   **否決原因**：作者否決：採用程式內的 Email 名單。

#### B5：用 `sub` 當名單
*   **優點**：不受 Email 改變影響。
*   **缺點**：事先不會知道別人的 `sub`。
*   **否決原因**：作者否決：採用 Email 名單，並要求 `email_verified = true`。

#### B6：`AddGoogle`
*   **優點**：設定較少。
*   **缺點**：預設不帶出 `email_verified`，issuer 不是標準值。
*   **否決原因**：作者否決（接受審查建議）：改用 `AddOpenIdConnect`。

#### B7：JWT 存在 localStorage、SPA 自己處理 PKCE
*   **優點**：API 不需要 cookie。
*   **缺點**：token 會暴露在 XSS 攻擊下。
*   **否決原因**：作者否決（接受審查建議）：改用同網域的 HttpOnly cookie。

#### B8：ASP.NET Identity、Keycloak、Auth0、Duende BFF
*   **優點**：功能完整。
*   **缺點**：只用 Google 登入、沒有角色時太重。
*   **否決原因**：作者否決（接受審查建議）。

### C. 規則與資料

#### C1：規則設定頁 + 權限管理、seed 管理者帳號
*   **優點**：不用改程式就能調整規則。
*   **缺點**：工作量多一倍；設定頁拿掉後，管理者帳號沒有事可做，還多了預設密碼的風險。
*   **否決原因**：作者否決：第一版規則寫在程式裡，改規則就是改程式並重新部署。

#### C2：規則存資料庫（seed + migration），或程式內一份規則表再產生 seed
*   **優點**：日後要做管理頁時比較容易。
*   **缺點**：沒有管理頁時，只是多一份要和程式同步的資料。
*   **否決原因**：作者否決。

#### C3：檢驗期間存進資料庫
*   **優點**：可以在資料庫裡排序和分頁。
*   **缺點**：修改車輛資料或規則後，容易留下過時的日期。
*   **否決原因**：作者否決：讀取時即時計算。

#### C4：出廠日期只存年月
*   **優點**：和行照上印的格式一致。
*   **缺點**：「剛好滿 5 年」沒有明確定義。
*   **否決原因**：作者否決：改存完整日期，行照只有年月時請使用者填該月 1 日。

#### C5：機車排氣量用牌照稅級距
*   **優點**：之後算稅可以直接用。
*   **缺點**：機車稅級（例如 501–600cc）可能跨過大型重機的門檻。
*   **否決原因**：作者否決：機車級距只作紀錄；汽車仍照牌照稅級距。

#### C6：排氣量改成「動力 + 整數 cc」（審查者建議）
*   **優點**：資料和行照一致，之後換算稅級最有彈性。
*   **缺點**：使用者可能輸入錯誤的數字。
*   **否決原因**：作者否決：偏好下拉選單；代價是之後若稅級和門檻對不上，要另外收精確的 cc 數，級距修改時也要做資料遷移。

#### C7：排氣量改成選填
*   **優點**：少一些驗證、check constraint 和資料遷移的工作。
*   **缺點**：資料不完整。
*   **否決原因**：作者否決：維持必填。審查第 5 階段 M1 再次提出「拿掉或改選填」，作者仍維持必填，接受驗證、check constraint、參考資料端點和日後資料遷移的成本。

#### C8：大型重機由燃油機車的排氣量級距推導（不設獨立車種）
*   **優點**：不會出現車種和級距互相矛盾的資料。
*   **缺點**：使用者在車種清單裡看不到「大型重型機車」。
*   **否決原因**：作者否決：設為獨立車種，車種和級距互相檢查（`BRACKET_TYPE_MISMATCH`）。

#### C9：拍行照用 OCR 辨識
*   **優點**：資料輸入最省事。
*   **缺點**：範圍太大，而且要多存行照照片這類個資。
*   **否決原因**：作者否決（接受審查建議）。

#### C11：把行照日期不一致的紀錄存進資料庫（`rule_mismatches` 表）
*   **優點**：每筆不一致都能保留下來，轉成規則的回歸測試，讓規則越用越準。
*   **缺點**：多一張表和一個資料存放處；仍需要有人判斷是規則錯還是使用者打錯。
*   **否決原因**：作者否決（審查第 5 階段 M7）：維持只寫 log。已知限制：Docker 輪替 log 後紀錄會遺失。

#### C10：推算的期間不變色、不參與排序
*   **優點**：維持「只會提早、不會晚」的原則。
*   **缺點**：滿 10 年的車少了第二次期間的提示。
*   **否決原因**：作者否決：照樣變色和排序，文件註明這個原則不適用於推算項目。

### D. 刪除、上限與同時編輯

#### D1：直接硬刪除
*   **優點**：範圍較小，個資刪除較乾淨。
*   **缺點**：使用者沒有反悔的機會。
*   **否決原因**：作者否決：採用垃圾桶。

#### D2：垃圾桶不自動清空
*   **優點**：不需要任何清除邏輯。
*   **缺點**：丟進垃圾桶的個資（車牌）永遠留著；垃圾桶算在 50 台上限內，使用者必須手動永久刪除才能新增。
*   **否決原因**：作者否決（審查第 5 階段 M3 後改變決定）：改成 30 天後自動清空。原本以為自動清空需要背景排程，審查者指出可以在寫入路徑和登入時順便清除，不需要排程。

#### D3：車牌唯一性不檢查垃圾桶（部分唯一索引）
*   **優點**：可以用同一個車牌重新新增。
*   **缺點**：還原垃圾桶裡的車時可能撞車牌。
*   **否決原因**：作者否決：垃圾桶也算，重複時提示去垃圾桶還原。

#### D4：不設每人車輛數上限
*   **優點**：少一個規則和錯誤代碼。
*   **缺點**：列表在記憶體排序、不分頁，數量沒有限制。
*   **否決原因**：作者否決：每人最多 50 台（含垃圾桶）。

#### D5：不做同時編輯檢查（最後一次儲存為準）
*   **優點**：少一個版本號和錯誤代碼。
*   **缺點**：較早儲存的修改會被默默覆蓋。
*   **否決原因**：作者否決：用 `xmin` 檢查，不一致時回傳 `VEHICLE_MODIFIED`。

### E. 日期顯示

#### E1：民國／西元在每個日期欄位旁切換，顯示跟著當初輸入的曆法
*   **優點**：輸入時最有彈性。
*   **缺點**：每個日期都要多存曆法；算出的日期還要決定跟哪個欄位；列表會民國、西元混在一起。
*   **否決原因**：作者否決：改成個人設定，全站套用。

#### E2：日期用文字輸入（例如 `114/03/15`）
*   **優點**：輸入快。
*   **缺點**：要自己寫解析和錯誤處理，也會發生在民國模式打成西元年的錯誤。
*   **否決原因**：作者否決：改用年、月、日三個下拉選單。

### F. 安全與金鑰

#### F1：三把獨立金鑰放在 `.env`
*   **優點**：每把金鑰可以單獨更換。
*   **缺點**：每個環境要管理和備份三把金鑰。
*   **否決原因**：作者否決：改用一把 `MASTER_KEY`，以 HKDF 推導。

#### F2：Data Protection 金鑰不加密，直接存在 PostgreSQL
*   **優點**：最簡單。
*   **缺點**：拿到資料庫備份的人就能偽造登入 cookie。
*   **否決原因**：作者否決。

#### F3：Data Protection 金鑰用 `.pfx` 憑證加密
*   **優點**：框架內建，不用自己寫加密。
*   **缺點**：要多管理一個憑證檔案。
*   **否決原因**：作者否決：改用從 `MASTER_KEY` 推導的 AES 金鑰。

#### F4：Email 和 Data Protection 金鑰的加密延到部署 VPS 時再做
*   **優點**：第一版少寫一些自己的加密程式。
*   **缺點**：之後有真實資料再補，需要資料遷移和重新測試。
*   **否決原因**：作者否決：第一版就做。審查第 5 階段 M2 建議簡化（只存 Email 雜湊、登入金鑰改用框架內建的檔案存放），作者仍維持現狀。已知限制：本機的資料庫備份和 `MASTER_KEY` 在同一台電腦上，加密在本機階段的保護效果有限；`MASTER_KEY` 要和資料庫備份分開保存，否則備份無法還原。

#### F5：自己寫 CSRF 檢查的 middleware
*   **優點**：完全掌握邏輯。
*   **缺點**：要自己寫和測試框架已經提供的功能。
*   **否決原因**：作者否決（接受審查建議）：改用框架內建的 `IAntiforgery`。

#### F6：第一版不加 gitleaks
*   **優點**：少一個工具要安裝。
*   **缺點**：`check-secrets.sh` 抓不到被直接寫進設定檔或程式的密鑰，而 repo 是公開的。
*   **否決原因**：作者否決：hook 和 CI 都加。

### G. 部署

#### G1：用 GitHub Actions 依分支自動部署（本機 self-hosted runner、VPS 用 SSH）
*   **優點**：push 後自動部署。
*   **缺點**：repo 是公開的，self-hosted runner 可能讓別人的 PR 在作者電腦上執行程式。
*   **否決原因**：延後（不是否決）：第一版手動部署。之後要加時的規則：公開 repo 不用 self-hosted runner，或只用 `push` 觸發；不用 `pull_request_target`；SSH 金鑰放在只限正式分支的 GitHub Environment；或改成主機自己從 GHCR 拉映像檔。

#### G2：本機 `deploy.sh` 先執行 `git pull`
*   **優點**：自動取得最新的程式。
*   **缺點**：本機就是開發機，可能蓋掉進行中的修改。
*   **否決原因**：作者否決（接受審查建議）。

#### G3：Caddy 反向代理（只在 VPS 啟用）
*   **優點**：自動申請 HTTPS 憑證。
*   **缺點**：VPS 實際是 k3s（內建 Traefik），而第一版只在本機部署。
*   **否決原因**：作者否決：第一版不需要反向代理。

#### G4：ASP.NET Core Razor Pages、SQLite
*   **優點**：單一專案、部署最簡單。
*   **缺點**：和作者指定的技術不同。
*   **否決原因**：作者否決：指定 Vue + PostgreSQL。

### H. 後端架構（本文件撰寫時決定）

#### H1：三個專案（Core = Domain + Application、Infrastructure、Web）
*   **優點**：少一個專案，改功能時跳的專案較少。
*   **缺點**：Core 內部「實體不能依賴使用案例」的分界沒有編譯器強制。
*   **否決原因**：作者否決：採用四個專案，讓編譯器強制每一層的依賴方向。

#### H2：兩個專案（Domain、Web）+ 架構測試
*   **優點**：專案最少。
*   **缺點**：Web 專案內部的分層只靠架構測試保護。
*   **否決原因**：作者否決：同上。

#### H3：Application 透過 `IAppDbContext` 直接使用 EF Core 的 `DbSet`
*   **優點**：程式最少，是常見的 .NET 範本做法。
*   **缺點**：Application 專案必須參考 EF Core，編譯器擋不住 Application 使用 EF Core 的功能，和四個專案的目的矛盾。
*   **否決原因**：作者否決：寫入用 Repository，讀取用查詢服務，Application 不參考 EF Core。

#### H4：只用 Repository（讀寫都走 Repository）
*   **優點**：只有一種資料存取概念。
*   **缺點**：讀取時要載入完整的實體。
*   **否決原因**：作者否決：讀取改用查詢服務。

#### H5：使用案例寫成一般的服務類別
*   **優點**：最直覺，不需要額外的介面。
*   **缺點**：服務類別容易越來越大，違反單一職責；共同處理要在每個方法重複寫。
*   **否決原因**：作者否決：每個使用案例一個 handler。

#### H6：MediatR
*   **優點**：有現成的 pipeline，是 .NET Clean Architecture 範本常見的做法。
*   **缺點**：多一個外部套件；2025 年起改為商業授權（有免費社群版），授權條件可能再變。
*   **否決原因**：作者否決：自己定義 `ICommandHandler`、`IQueryHandler` 介面。

#### H7：每一層一個測試專案，或分成單元、整合、架構三個測試專案
*   **優點**：架構測試可以在 CI 上單獨設成必要檢查。
*   **缺點**：測試專案較多，CI 設定較複雜。
*   **否決原因**：作者否決：只用 `UnitTests`（含架構測試）和 `IntegrationTests` 兩個。

#### H8：Minimal API
*   **優點**：程式最少，一個端點對一個 handler。
*   **缺點**：端點的檔案組織要自己規劃；部分 filter 的寫法和範例較少。
*   **否決原因**：作者否決：採用 Controller。

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **安全與隱私：** 登入名單每次請求重新檢查；Email 和 Data Protection 金鑰以 `MASTER_KEY` 推導的金鑰 AES-GCM 加密；CSRF 用 `IAntiforgery`；別人的資料一律回傳 404；log 不寫車牌，Email 只寫查詢雜湊前 8 碼；密鑰不進 git（`.gitignore` + `check-secrets.sh` + gitleaks，hook 和 CI 都跑）。
*   **效能：** 每人最多 50 台車，列表在記憶體排序、不分頁；名單比對在記憶體完成，不查資料庫。
*   **上線與回滾：** 第一版只在本機 Docker 執行，資料庫可以隨時清空重建（`docker compose down -v`），migration 可以自由修改。「migration 只增不改」從第一次部署到正式 VPS 開始適用。

--------------------------------------------------------------------------------

## Detailed Implementation Plan

### 0. 實作順序

1. Repo 基礎設定（版本固定、`.gitignore`、密鑰檢查、hook）
2. Domain（規則引擎 + 單元測試）——先讓成功標準 1 通過
3. Application（handler + 單元測試）
4. Infrastructure（EF Core、加密、Data Protection、migration）
5. Web（登入、CSRF、Controllers、錯誤處理）+ 整合測試
6. 前端（Vue）+ Vitest
7. Docker Compose、`deploy.sh`、CI

### 1. 共用合約

#### 1.1 固定代碼

| 類別 | 代碼 |
| --- | --- |
| 車種 `VehicleType` | `CAR`（自用小客車）、`FUEL_MC`（一般燃油機車）、`HEAVY_MC`（大型重型機車）、`ELECTRIC_MC`（電動機車） |
| 汽車級距 | `CAR_LE_500`、`CAR_501_600`、`CAR_601_1200`、`CAR_1201_1800`、`CAR_1801_2400`、`CAR_2401_3000`、`CAR_3001_4200`、`CAR_4201_5400`、`CAR_5401_6600`、`CAR_6601_7800`、`CAR_GT_7800`、`CAR_ELECTRIC` |
| 機車級距 | `FUEL_MC`：`MC_LE_50`、`MC_51_250`；`HEAVY_MC`：`MC_251_550`、`MC_GT_550`；`ELECTRIC_MC`：無（null） |
| 檢驗種類 `kind` | `PERIODIC`（監理定期檢驗）、`EXHAUST`（排氣定檢） |
| 狀態 `status` | `EXEMPT`、`UPCOMING`、`IN_WINDOW`、`UNCOMPUTABLE` |
| 日期來源 `basis` | `FORMULA`、`LICENCE` |
| 曆法 | `ROC`、`GREGORIAN` |
| 排序 `sort` | `deadline`（預設）、`manufactureDate` |

#### 1.2 規則 ID 與規則表

| 車種 | 種類 | 規則 ID | 內容 |
| --- | --- | --- | --- |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE0TO5` | 未滿 5 年：顯示第一次定檢期間，狀態 `UPCOMING` |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE5TO10` | 每年一次，指定日期前後一個月（以日計） |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE10UP` | 每年兩次：指定日期 + 6 個月推算（`estimated: true`） |
| `CAR` | `PERIODIC` | `RTS44-CAR-LICENCE` | 有填行照指定檢驗日期時使用 |
| `HEAVY_MC` | `PERIODIC` | `RTS44-HMC-AGE0TO5`、`RTS44-HMC-AGE5TO10`、`RTS44-HMC-AGE10UP`、`RTS44-HMC-LICENCE` | 同汽車 |
| `FUEL_MC`、`HEAVY_MC` | `EXHAUST` | `APC40-MC-AGE0TO5`、`APC40-MC-ANNUAL` | 未滿 5 年顯示第一次期間；滿 5 年起每年一次，原發照月份前一個月 1 日到後一個月最後一天（以月計） |
| `ELECTRIC_MC` | `EXHAUST` | `APC40-EMC-EXEMPT` | 免檢 |

每條規則附法規出處（法規名稱、條號、修正日期），並有 `EffectiveFrom` 欄位（填目前條文的修正日期）。規則引擎透過 `RuleTable.Current()` 取得現行規則，第一版這個函式永遠回傳全部規則，不寫任何過渡邏輯；日後法規修改時，只要新增規則並調整這個函式（審查第 5 階段 N7）。`RTS44` = 道路交通安全規則第 44 條；`APC40` = 空氣污染防制法第 40 條。

#### 1.3 規則引擎演算法（虛擬碼）

```text
Calculate(vehicle, today):
  kinds = { CAR: [PERIODIC], FUEL_MC: [EXHAUST], HEAVY_MC: [PERIODIC, EXHAUST], ELECTRIC_MC: [EXHAUST] }
  for kind in kinds[vehicle.type]:
    if vehicle.type == ELECTRIC_MC: yield EXEMPT item (dates null, rule APC40-EMC-EXEMPT); continue
    if kind == PERIODIC:
      anchors = vehicle.licenceDate != null
                ? Sequence(L, L+1y, L+2y, ...)                         basis = LICENCE
                : Sequence(D1, D1+1y, D1+2y, ...)                      basis = FORMULA
                  where D1 = issueDate + (5 - gapYears)y,
                        gapYears = 出廠到發照的完整年數（剛好 N 年算「未逾 N 年」），上限 4
      windows = for each anchor A:
                  [A-1m, A+1m]                                         estimated = false
                  if age(manufactureDate, A) >= 10y: [A+6m-1m, A+6m+1m] estimated = true
    if kind == EXHAUST:
      windows = for each year Y from manufacture year:
                  M = issueDate.month; window = [1st of (Y,M)-1m, last day of (Y,M)+1m]
                take windows whose deadline >= manufactureDate + 5y
    choose earliest window whose deadline >= today
    status = today in [start, deadline] (含兩端) ? IN_WINDOW : UPCOMING
    if no window or invalid input → UNCOMPUTABLE（reason: NO_RULE 或 INVALID_INPUT）
    diagnostics（只給伺服器）: 有填 L 時，比對同年度公式日期的月日（滿 10 年時也比對 +6 個月），不一致 → mismatch = true、formulaDate
```

月份加減一律用 `DateOnly.AddMonths` / `AddYears`（月底往前夾）。

#### 1.4 API 合約

所有回應的日期一律是西元 `YYYY-MM-DD`；錯誤一律是 ProblemDetails（`type`、`title`、`status`、`traceId`，擴充欄位 `code`，必要時加 `trashedVehicleId`、`activeCount`、`trashCount`）。

| 方法與路徑 | 請求 | 成功 | 錯誤 |
| --- | --- | --- | --- |
| `GET /api/auth/login?returnUrl=` | — | 302 到 Google | `returnUrl` 不合法時改用 `/` |
| `POST /api/auth/logout` | — | 204 | 401、400 `CSRF_INVALID` |
| `GET /api/me` | — | 200 `{ email, calendar }`，並發出 `XSRF-TOKEN` cookie | 401 |
| `PATCH /api/me/preferences` | `{ calendar }` | 200（同 `/api/me`） | 400 `INVALID_CALENDAR`、401、400 `CSRF_INVALID` |
| `GET /api/vehicles?sort=` | — | 200 `[VehicleDto]` | 401 |
| `GET /api/vehicles/{id}` | — | 200 `VehicleDto` | 404 `VEHICLE_NOT_FOUND`、401 |
| `POST /api/vehicles` | `VehicleInput` | 201 `VehicleDto`（`Location` header） | 400 驗證錯誤、409 `PLATE_EXISTS_ACTIVE`／`PLATE_EXISTS_IN_TRASH`／`VEHICLE_LIMIT_REACHED`、401、400 `CSRF_INVALID` |
| `PUT /api/vehicles/{id}` | `VehicleInput` + `version` | 200 `VehicleDto` | 400 驗證錯誤（沒帶 `version` 也屬於 `VALIDATION_FAILED`）、404、409 `VEHICLE_MODIFIED`／`VEHICLE_IN_TRASH`／`PLATE_EXISTS_*`、401、400 `CSRF_INVALID` |
| `POST /api/vehicles/{id}/trash` | — | 204 | 404、409 `VEHICLE_IN_TRASH`、401、400 `CSRF_INVALID` |
| `POST /api/vehicles/{id}/restore` | — | 204 | 404、409 `VEHICLE_NOT_IN_TRASH`、401、400 `CSRF_INVALID` |
| `DELETE /api/vehicles/{id}` | — | 204 | 404、409 `VEHICLE_NOT_IN_TRASH`、401、400 `CSRF_INVALID` |
| `GET /api/trash` | — | 200 `[TrashItemDto]` | 401 |
| `GET /api/reference/displacement-brackets` | — | 200 `[{ code, label, vehicleTypes }]` | 401 |
| `GET /health/live`、`GET /health/ready` | — | 200 `{ status, gitSha }` | 503（ready：資料庫連不上） |

*   `VehicleInput`：`plate`、`brand`、`model`、`nickname?`、`color?`、`vehicleType`、`displacementBracket?`、`manufactureDate`、`originalIssueDate`、`licenceInspectionDate?`。
*   `VehicleDto`：`id`、`version`、`VehicleInput` 的所有欄位、`inspections: [{ kind, status, earliestDate?, deadline?, ruleId, estimated, basis }]`。
*   `TrashItemDto`：`id`、`plate`、`brand`、`model`、`nickname?`、`vehicleType`、`deletedAt`、`purgeAt`（`deletedAt` + 30 天，畫面顯示「將於 X 永久刪除」）。

#### 1.5 錯誤代碼

| 代碼 | HTTP | 情況 |
| --- | --- | --- |
| `VALIDATION_FAILED` | 400 | 必填欄位缺漏（包括 PUT 沒帶 `version`）、格式錯誤（附欄位清單） |
| `ISSUE_BEFORE_MANUFACTURE` | 400 | 原發照日早於出廠日 |
| `DATE_IN_FUTURE` | 400 | 出廠日或原發照日是未來日期 |
| `BRACKET_TYPE_MISMATCH` | 400 | 級距不屬於該車種，或電動機車送了級距、其他車種沒送 |
| `LICENCE_DATE_NOT_ALLOWED` | 400 | 一般燃油機車或電動機車送了非 null 的行照日期 |
| `LICENCE_DATE_BEFORE_ISSUE` | 400 | 行照日期早於原發照日 |
| `LICENCE_DATE_TOO_FAR` | 400 | 行照日期晚於今年 + 2 年的 12/31 |
| `INVALID_CALENDAR` | 400 | 曆法值不是 `ROC` 或 `GREGORIAN` |
| `CSRF_INVALID` | 400 | CSRF token 缺少或錯誤 |
| `VEHICLE_NOT_FOUND` | 404 | 車輛不存在或不屬於目前使用者 |
| `PLATE_EXISTS_ACTIVE` | 409 | 車牌和使用中的車重複 |
| `PLATE_EXISTS_IN_TRASH` | 409 | 車牌和垃圾桶裡的車重複（附 `trashedVehicleId`） |
| `VEHICLE_LIMIT_REACHED` | 409 | 已達 50 台（附 `activeCount`、`trashCount`） |
| `VEHICLE_MODIFIED` | 409 | 版本號不一致 |
| `VEHICLE_IN_TRASH` | 409 | 修改或丟進垃圾桶的車已在垃圾桶裡 |
| `VEHICLE_NOT_IN_TRASH` | 409 | 還原或永久刪除的車不在垃圾桶裡 |

#### 1.6 資料庫 schema

`users`

| 欄位 | 型別 | 限制 |
| --- | --- | --- |
| `id` | `uuid` | PK（`pk_users`） |
| `google_sub` | `text` | NOT NULL，唯一（`ux_users_google_sub`） |
| `email_encrypted` | `bytea` | NOT NULL |
| `email_lookup_hash` | `bytea` | NOT NULL，一般索引（`ix_users_email_lookup_hash`） |
| `calendar_preference` | `text` | NOT NULL，預設 `'ROC'`，check（`ck_users_calendar`） |
| `created_at`、`updated_at` | `timestamptz` | NOT NULL |

`vehicles`

| 欄位 | 型別 | 限制 |
| --- | --- | --- |
| `id` | `uuid` | PK（`pk_vehicles`） |
| `owner_id` | `uuid` | NOT NULL，FK → `users.id`（`fk_vehicles_owner`） |
| `plate` | `text` | NOT NULL，長度 1–16 |
| `normalized_plate` | `text` | NOT NULL；`(owner_id, normalized_plate)` 唯一（`ux_vehicles_owner_plate`） |
| `brand`、`model` | `text` | NOT NULL，長度 1–50 |
| `nickname`、`color` | `text` | 可 null，長度 ≤ 50 |
| `vehicle_type` | `text` | NOT NULL，check（`ck_vehicles_type`） |
| `displacement_bracket` | `text` | 可 null；check：只有 `ELECTRIC_MC` 為 null，且代碼屬於該車種（`ck_vehicles_bracket`） |
| `manufacture_date`、`original_issue_date` | `date` | NOT NULL；check `original_issue_date >= manufacture_date`（`ck_vehicles_dates`） |
| `licence_inspection_date` | `date` | 可 null；check：只有 `CAR`、`HEAVY_MC` 可以有值（`ck_vehicles_licence_date`） |
| `deleted_at` | `timestamptz` | 可 null；`(owner_id, deleted_at)` 索引（`ix_vehicles_owner_deleted`），供 30 天清除使用 |
| `created_at`、`updated_at` | `timestamptz` | NOT NULL |
| `xmin` | 系統欄位 | 對應 `uint Version`，同時編輯檢查 |

`data_protection_keys`：框架定義的欄位，放在獨立的 DbContext 和 migration。

#### 1.7 命名慣例

| 種類 | 風格 | 例子 |
| --- | --- | --- |
| query 參數 | camelCase | `returnUrl`、`sort` |
| query 參數的值 | camelCase | `sort=deadline`、`sort=manufactureDate` |
| JSON 欄位（請求、回應、ProblemDetails 擴充欄位） | camelCase | `licenceInspectionDate`、`trashedVehicleId`、`earliestDate` |
| URL 路徑 | 全小寫，單字之間用連字號（kebab-case） | `/api/reference/displacement-brackets`、`/api/me/preferences` |
| 代碼值（車種、級距、檢驗種類、狀態、日期來源、曆法、錯誤代碼） | 全大寫，單字之間用底線 | `HEAVY_MC`、`MC_51_250`、`IN_WINDOW`、`PLATE_EXISTS_IN_TRASH`、`ROC` |
| 規則 ID | 全大寫，用連字號分段 | `RTS44-CAR-AGE5TO10` |
| 資料表、欄位、索引、constraint | 全小寫，單字之間用底線（snake_case）；索引和 constraint 用 `pk_`、`fk_`、`ux_`、`ix_`、`ck_` 前綴 | `licence_inspection_date`、`ux_vehicles_owner_plate` |
| C# 型別、方法、屬性 | PascalCase | `InspectionCalculator`、`LicenceInspectionDate` |
| 環境變數 | 全大寫，單字之間用底線 | `ALLOWED_EMAILS`、`MASTER_KEY` |
| 前端檔案 | Vue 元件 PascalCase；其他 TypeScript 檔案 camelCase | `VehicleListPage.vue`、`useCurrentUser.ts`、`calendar.ts` |

新增端點、欄位或代碼時照這張表命名。ASP.NET Core 預設把 JSON 序列化成 camelCase；EF Core 的欄位名稱要明確對應成 snake_case（例如用命名慣例套件或在設定檔裡指定）。

### 2. 檔案清單

#### 2.1 Repo 根目錄

| 檔案 | 新增／修改 | 內容與理由 |
| --- | --- | --- |
| `.gitignore` | 新增 | 第一個 commit 就包含 `.env`、`**/.env`、`bin/`、`obj/`、`node_modules/`、`web/dist/`；防止密鑰和建置產物進 git |
| `.editorconfig` | 新增 | 統一縮排和換行，讓 diff 乾淨 |
| `global.json` | 新增 | 固定 .NET 10 SDK 版本，Dockerfile 和 CI 用同一版本 |
| `Directory.Build.props` | 新增 | 所有專案共用：`Nullable=enable`、`TreatWarningsAsErrors=true`、`InvariantGlobalization=false`（NFKC 需要） |
| `Directory.Packages.props` | 新增 | 集中管理 NuGet 套件版本 |
| `VehicleGo.sln` | 新增 | 方案，包含 4 個 `src` 專案和 2 個 `tests` 專案 |
| `postgres-image.txt` | 新增 | PostgreSQL 映像檔標籤（例如 `postgres:17.x`），Compose 和 Testcontainers 都讀它，CI 不依賴 `.env` |
| `.env.example` | 新增 | 列出所有環境變數和範例值（Email 一律用 `example.com`），附 `openssl rand -base64 32` 產生 `MASTER_KEY` 的說明 |
| `docker-compose.yml` | 新增 | `db`、`api` 兩個服務；只引用 `${...}` 變數；`build.args: GIT_SHA`；log 輪替；`api` port 固定 |
| `Dockerfile` | 新增 | multi-stage：Node 建置 `web/` → .NET publish `VehicleGo.Web`（含 `wwwroot`）→ runtime（含 tzdata，非 chiseled／Alpine） |
| `deploy.sh` | 新增 | 匯出 `GIT_SHA`（有未 commit 修改時加 `-dirty`）；`docker compose up -d --build`；不執行 `git pull` |
| `scripts/check-secrets.sh` | 新增 | `.env`（含子資料夾）被追蹤就失敗；掃 `*.cs`、`*.ts`、`*.vue`、`*.md`、`*.yml` 中 RFC 2606 保留網域以外的 Email；可傳入「只檢查暫存檔」參數供 hook 使用 |
| `.githooks/pre-commit` | 新增（可執行） | 先跑 `check-secrets.sh`（暫存檔），再跑 `gitleaks git --staged`；gitleaks 沒安裝或版本和 `.gitleaks-version` 不同時失敗並提示安裝方式 |
| `.gitleaks.toml` | 新增 | `[extend] useDefault = true`；排除 HKDF 測試向量等已知的假值 |
| `.gitleaks-version`、`.gitleaks-version.sha256` | 新增 | gitleaks 版本號（不含 `v`）和下載檔的 SHA-256 |
| `.gitleaksignore` | 新增 | 空檔；日後處理過的誤判或已更換的密鑰 fingerprint |
| `.github/workflows/ci.yml` | 新增 | job：`backend`（建置、`UnitTests`、`IntegrationTests`）、`frontend`（`npm ci`、`typecheck`、`test:unit`、`build`）、`check-secrets`、`gitleaks`（`fetch-depth: 0` 掃完整歷史）；`permissions: contents: read`；觸發 `pull_request` 和 push 任何分支 |
| `README.md` | 新增 | 啟用 hook（`git config core.hooksPath .githooks`）、建立 `.env`、`deploy.sh`、清空重建指令（`docker compose down -v`）、Google OAuth redirect URI 設定 |

#### 2.2 `src/VehicleGo.Domain/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Domain.csproj` | 不參考任何其他專案或框架套件 |
| `Common/ErrorCode.cs` | 1.5 節所有錯誤代碼的常數 |
| `Common/Result.cs` | 成功或失敗（附錯誤代碼和欄位）的結果型別，取代拋出例外 |
| `Vehicles/VehicleType.cs`、`Vehicles/DisplacementBracket.cs` | 1.1 節的代碼 |
| `Vehicles/DisplacementBrackets.cs` | 級距清單（代碼、中文標籤、適用車種、法規出處）和「車種 → 可用級距」對照 |
| `Vehicles/PlateNormalizer.cs` | NFKC、轉大寫、去空白和連字號 |
| `Vehicles/Vehicle.cs` | 車輛實體；建立和修改時呼叫驗證；丟進垃圾桶、還原的狀態轉換 |
| `Vehicles/VehicleValidator.cs` | 日期先後、未來日期、級距與車種、行照日期規則（需要「今天」作為參數） |
| `Vehicles/VehicleLimits.cs` | 上限常數 50 |
| `Users/User.cs`、`Users/CalendarPreference.cs` | 使用者實體和曆法代碼 |
| `Inspections/InspectionKind.cs`、`InspectionStatus.cs`、`InspectionBasis.cs` | 1.1 節的代碼 |
| `Inspections/InspectionItem.cs` | 一個檢驗項目（給 API 用的欄位） |
| `Inspections/InspectionDiagnostics.cs` | 只給伺服器用：`UNCOMPUTABLE` 原因、行照日期不一致旗標、公式日期 |
| `Inspections/InspectionCalculator.cs` | 1.3 節的純函式 |
| `Inspections/Rules/InspectionRule.cs`、`PeriodCalculation.cs`、`LegalCitation.cs`、`RuleTable.cs` | 1.2 節的規則表，算法用 enum 表示；每條規則有 `EffectiveFrom`；`RuleTable.Current()` 回傳現行規則 |
| `Inspections/Rules/FirstInspectionDate.cs` | 第一次定檢日（第 14 節 2a） |

#### 2.3 `src/VehicleGo.Application/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Application.csproj` | 只參考 Domain |
| `Abstractions/ICommandHandler.cs`、`IQueryHandler.cs` | handler 介面（C3） |
| `Abstractions/IVehicleRepository.cs` | `GetForOwnerAsync(id, includeTrashed)`、`LockOwnerAsync(ownerId)`（在交易中對 `users` 該列 `SELECT ... FOR UPDATE`）、`CountForOwnerIncludingTrashAsync`、`PurgeExpiredTrashAsync(ownerId, cutoff)`、`AddAsync`、`SaveAsync(vehicle, expectedVersion?)`、`DeleteAsync` |
| `Abstractions/IUserRepository.cs` | `UpsertOnLoginAsync(sub, emailEncrypted, emailHash)`、`GetAsync`、`GetCalendarPreferenceAsync`、`SaveAsync` |
| `Abstractions/IVehicleQueries.cs` | 列出使用中的車、取單台、列出垃圾桶（都只限目前使用者） |
| `Abstractions/ICurrentUser.cs` | 目前使用者 ID 和 cookie 裡的 Email |
| `Abstractions/IEmailProtector.cs` | 加密 Email、計算查詢雜湊 |
| `Abstractions/IAllowlistProvider.cs` | 取得名單 |
| `Common/TaipeiToday.cs` | 由 `TimeProvider` 取得台北時間的今天 |
| `Auth/LoginAdmission.cs` | 名單比對 + `email_verified` |
| `Auth/EnsureUserCommand.cs`（含 handler） | 登入時建立或更新使用者，並清除該使用者滿 30 天的垃圾桶車輛 |
| `Vehicles/CreateVehicleCommand.cs`、`UpdateVehicleCommand.cs`、`TrashVehicleCommand.cs`、`RestoreVehicleCommand.cs`、`PurgeVehicleCommand.cs`（各含 handler） | 寫入使用案例；新增和修改依規則引擎診斷寫 `LICENCE_DATE_MISMATCH`、`UNCOMPUTABLE` log |
| `Vehicles/ListVehiclesQuery.cs`、`GetVehicleQuery.cs`、`ListTrashQuery.cs`（各含 handler） | 讀取使用案例；呼叫規則引擎；排序 |
| `Vehicles/VehicleDto.cs`、`InspectionDto.cs`、`TrashItemDto.cs` | 回應資料 |
| `Me/GetMeQuery.cs`、`UpdatePreferencesCommand.cs`（各含 handler） | `/api/me` |
| `Reference/GetDisplacementBracketsQuery.cs`（含 handler） | 級距清單 |
| `DependencyInjection.cs` | 掃描組件，註冊所有 handler |

#### 2.4 `src/VehicleGo.Infrastructure/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Infrastructure.csproj` | 參考 Application、Domain；EF Core、Npgsql |
| `Persistence/AppDbContext.cs` | `users`、`vehicles`；具名查詢過濾器「owner」「deleted」 |
| `Persistence/Configurations/UserConfiguration.cs`、`VehicleConfiguration.cs` | 1.6 節的欄位、索引、check constraint 和名稱；`xmin` |
| `Persistence/PostgresErrorMapper.cs` | 依 constraint 名稱把 23505 轉成 `PLATE_EXISTS_*`；`DbUpdateConcurrencyException` 轉成 `VEHICLE_MODIFIED` |
| `Persistence/Repositories/VehicleRepository.cs`、`UserRepository.cs` | 實作 Repository；PUT 時把用戶端版本設成 `OriginalValue`；計數只關閉「deleted」過濾器 |
| `Persistence/Queries/VehicleQueries.cs` | 實作查詢服務（`AsNoTracking`） |
| `Persistence/Migrations/` | `AppDbContext` 的 EF Core migration（產生的檔案） |
| `Crypto/MasterKey.cs` | 讀取並驗證 `MASTER_KEY`（Base64、32 bytes） |
| `Crypto/KeyDerivation.cs` | HKDF-SHA256，salt 為空，info 為標籤 |
| `Crypto/AesGcmProtector.cs` | 版本 byte + nonce + tag + 密文；associated data 為用途字串 |
| `Crypto/EmailProtector.cs` | 實作 `IEmailProtector` |
| `DataProtection/DataProtectionKeyContext.cs` | 獨立的 DbContext，只存登入金鑰 |
| `DataProtection/Migrations/` | 這個 DbContext 的 migration |
| `DataProtection/AesGcmXmlEncryptor.cs`、`AesGcmXmlDecryptor.cs` | 加密 Data Protection 金鑰；解密失敗寫 Error log |
| `DependencyInjection.cs` | 註冊 DbContext、Repository、查詢服務、加密、Data Protection |

#### 2.5 `src/VehicleGo.Web/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Web.csproj` | 參考 Application、Infrastructure |
| `Program.cs` | 組裝相依性；啟動時套用兩個 DbContext 的 migration；JSON console log；ProblemDetails；健康檢查；靜態檔與 SPA fallback（排除 `/api`、`/signin-oidc`）；`--healthcheck` 參數 |
| `appsettings.json` | 不含任何密鑰；log 等級 |
| `Startup/StartupValidation.cs` | 啟動時檢查 `MASTER_KEY`、Asia/Taipei、`ALLOWED_EMAILS`（空的時候警告並全部拒絕） |
| `Auth/AuthenticationSetup.cs` | cookie 為預設 scheme；OIDC（Scope 只有 `openid email`、`ResponseType=code`、`MapInboundClaims=false`、`SaveTokens=false`）；`OnTokenValidated`、`OnValidatePrincipal`、`OnRedirectToLogin`／`OnRedirectToAccessDenied`、`OnRemoteFailure` |
| `Auth/EnvironmentAllowlistProvider.cs` | 實作 `IAllowlistProvider`（讀 `ALLOWED_EMAILS`） |
| `Auth/HttpContextCurrentUser.cs` | 實作 `ICurrentUser` |
| `Security/AntiforgerySetup.cs` | `IAntiforgery`（`X-XSRF-TOKEN`）；先驗證登入再驗證 CSRF |
| `Errors/ErrorCodeMapping.cs` | 錯誤代碼 → HTTP 狀態碼與 ProblemDetails |
| `Contracts/VehicleInput.cs`、`UpdateVehicleRequest.cs`、`UpdatePreferencesRequest.cs` | 請求資料 |
| `Controllers/AuthController.cs`、`MeController.cs`、`VehiclesController.cs`、`TrashController.cs`、`ReferenceController.cs` | 1.4 節的端點；handler 用 `[FromServices]` 注入在 action 上 |

#### 2.6 `tests/VehicleGo.UnitTests/`（xUnit）

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.UnitTests.csproj` | 參考 Domain、Application、Infrastructure；xUnit、ArchUnitNET、`Microsoft.Extensions.TimeProvider.Testing` |
| `Domain/InspectionCalculatorCarTests.cs` | 汽車：未滿 5 年、剛好 5 年、10 年切換前後、2/29 發照、月底 + 6 個月、`estimated` |
| `Domain/InspectionCalculatorMotorcycleTests.cs` | 燃油機車和大型重機的排氣定檢（月份計算、1 月和 12 月跨年）；大型重機兩種檢驗；電動機車免檢 |
| `Domain/InspectionCalculatorLicenceDateTests.cs` | 行照日期為準、往後推、未滿 5 年也用行照日期、不一致的診斷 |
| `Domain/FirstInspectionDateTests.cs` | 出廠到發照相隔剛好 1、2、3、4 年和 5 年以上 |
| `Domain/RuleTableCoverageTests.cs` | 車種 × 檢驗種類 × 車齡區間，沒有重疊、沒有缺口；有填和沒填行照日期 |
| `Domain/PlateNormalizerTests.cs`、`VehicleValidatorTests.cs`、`DisplacementBracketsTests.cs` | 正規化、輸入驗證、級距對照 |
| `Application/*HandlerTests.cs` | 每個 handler 一個測試檔，用 `Fakes/` 的假 Repository 和查詢服務；列表排序；名單比對 |
| `Infrastructure/KeyDerivationTests.cs` | 用固定的假測試向量（已排除在 gitleaks 之外）固定推導結果 |
| `Infrastructure/AesGcmProtectorTests.cs`、`DataProtectionEncryptorTests.cs` | 加解密往返、用錯金鑰失敗、用途字串不同時失敗、存的不是明文 |
| `Architecture/LayerDependencyTests.cs` | ArchUnitNET：Domain 不依賴其他層和框架；Application 不依賴 Infrastructure、Web、EF Core、ASP.NET |
| `Fakes/` | 假的 Repository、查詢服務、`ICurrentUser`、`IEmailProtector` |

測試案例名稱附上條號和修正日期（例如 `RTS44_111-04-01_汽車滿5年_每年一次`）。

#### 2.7 `tests/VehicleGo.IntegrationTests/`（xUnit + Testcontainers）

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.IntegrationTests.csproj` | 參考 Web；xUnit、Testcontainers.PostgreSql、`Microsoft.AspNetCore.Mvc.Testing` |
| `Infrastructure/PostgresFixture.cs` | 讀 `postgres-image.txt` 啟動 PostgreSQL，套用兩個 DbContext 的 migration |
| `Infrastructure/TestLoginEndpoint.cs` | 測試用的登入端點：直接為指定的測試使用者發出真正的登入 cookie。只由 `VehicleGoFactory` 透過 `ConfigureTestServices` 加上的 startup filter 對應路由，正式的 Web 專案完全不含這段程式（審查第 5 階段 M5） |
| `Infrastructure/VehicleGoFactory.cs` | `IntegrationTest` 環境；註冊 `TestLoginEndpoint`；記憶體中產生的 `MASTER_KEY`；可替換的名單；`FakeTimeProvider` |
| `Infrastructure/TestClient.cs` | 透過測試登入端點取得**真正的** cookie，自動帶 XSRF token。所有整合測試都走真正的 cookie 驗證和每次請求的名單檢查，不使用假的登入處理器（作者決定，取代 brief 第 8 節「一般測試用假的登入處理器」的寫法） |
| `AuthTests.cs` | `/api` 未登入回 401（不轉址）；移出名單後下一個請求 401；`returnUrl` 拒絕外部網址；登出要 CSRF；同時第一次登入只建立一筆 |
| `CsrfTests.cs` | 沒帶 header 回 `CSRF_INVALID`；未登入時先回 401 |
| `VehicleTests.cs` | 1.5 節每個錯誤代碼至少一個測試；車牌重複（使用中、垃圾桶）；垃圾桶流程；滿 30 天的垃圾桶車輛在新增或登入時被刪除、讀取時看不到（用 `FakeTimeProvider` 控制時間） |
| `ConcurrencyTests.cs` | 舊版本回 `VEHICLE_MODIFIED`；沒帶版本回 `VALIDATION_FAILED`（欄位清單含 `version`） |
| `VehicleLimitTests.cs` | 第 51 台被擋下並附數量；有 49 台時同時送出兩筆新增，只有一筆成功、另一筆回 `VEHICLE_LIMIT_REACHED`（審查第 5 階段 N3） |
| `IsolationTests.cs` | 使用者 A 對 B 的每個車輛和垃圾桶端點都回 404；`/api/me` 只讀寫自己 |
| `LoggingTests.cs` | `LICENCE_DATE_MISMATCH` 只在新增或修改時寫 |
| `HealthTests.cs` | `/health/live`、`/health/ready` 回傳 SHA |

#### 2.8 `web/`（Vue 3 + TypeScript + Vite + Vue Router + Element Plus）

| 檔案 | 內容與理由 |
| --- | --- |
| `package.json` | scripts：`dev`、`build`、`typecheck`（`vue-tsc --noEmit`）、`test:unit`（Vitest） |
| `.nvmrc` | 固定 Node 版本，Dockerfile 和 CI 用同一版本 |
| `vite.config.ts` | dev proxy：`/api`、`/signin-oidc`；Vitest 設定 |
| `tsconfig.json`、`index.html`、`src/main.ts`、`src/App.vue` | 專案骨架；只註冊用到的 Element Plus 元件 |
| `src/router.ts` | 路由：`/login`、`/not-allowed`、`/vehicles`、`/vehicles/new`、`/vehicles/:id`、`/trash`、`/settings` |
| `src/api/client.ts` | `fetch` 包裝：帶 `X-XSRF-TOKEN`；401 導向登入頁；`CSRF_INVALID` 重新取得 token 後重試一次 |
| `src/api/types.ts` | 1.4 節的資料型別 |
| `src/composables/useCurrentUser.ts` | 模組層級的使用者與曆法狀態（不用 Pinia） |
| `src/dates/calendar.ts` | 民國／西元換算、每月天數、年份範圍；日期當成年月日三個數字處理，不使用 JavaScript `Date` |
| `src/dates/calendar.spec.ts` | 成功標準 4 的 Vitest 測試 |
| `src/components/DateSelect.vue` | 年、月、日三個下拉選單（Element Plus `el-select`） |
| `src/components/InspectionBadge.vue` | 依 `status`、`basis`、`estimated` 顯示文字、顏色和標示 |
| `src/components/Disclaimer.vue` | 免責聲明 |
| `src/pages/LoginPage.vue`、`NotAllowedPage.vue`、`VehicleListPage.vue`、`VehicleFormPage.vue`、`TrashPage.vue`、`SettingsPage.vue` | 各頁面；錯誤代碼對應繁體中文訊息 |

### Verification Plan

*   **建置：** `dotnet build VehicleGo.sln`；`npm ci && npm run build`（在 `web/`）；`docker compose build`。
*   **單元測試：** `dotnet test tests/VehicleGo.UnitTests`；`npm run test:unit`。
*   **整合測試：** `dotnet test tests/VehicleGo.IntegrationTests`（需要 Docker）。
*   **靜態檢查：** `TreatWarningsAsErrors`；`npm run typecheck`；`scripts/check-secrets.sh`；`gitleaks git`。
*   **測試覆蓋率：** 不設數字門檻；以成功標準列出的案例為準。規則引擎（Domain）的每條規則和每個邊界都要有案例。
*   **手動驗證：** 用 Chrome 或 Firefox 開 `http://localhost:<port>`，走過 Critical User Journeys 的 CUJ 1–11（把系統時間或 `TimeProvider` 固定在 2026-11-19，比對 CUJ 3–6 寫出的確切期間）。

--------------------------------------------------------------------------------

*Drafted with EGM*

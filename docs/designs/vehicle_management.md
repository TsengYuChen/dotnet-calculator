# 個人車輛管理系統（第一版：檢驗期間）

*   **Author:** tsengyuchen
*   **Status:** Proposed
*   **Date:** 2026-11-19

--------------------------------------------------------------------------------

## Overview

一個網頁版的個人車輛管理系統。使用者用 Google 帳號登入，登錄自己擁有的汽機車；系統依台灣法規，在每次讀取時算出每台車每種檢驗（定期檢驗、排氣定檢）的下一個檢驗期間，顯示在網頁上。

第一版只做「車輛資料 + 檢驗期間」：
- 刻意不做通知、管理頁、帳號刪除和逾期判斷，目的是先驗證規則的實作符合作者對法規的理解（法規本身的正確性見 [Risks & Mitigations](#risks--mitigations)）。
- 只開放給環境變數名單上的 Google 帳號。
- 只部署在作者本機的 Docker 當作測試環境，正式的 VPS 部署延後。

之後可能會加入保養紀錄、加油紀錄，但第一版不為之後的功能預先寫程式（不預建資料表、API 或畫面）。
- **例外：** 作者想先收集、之後會用到的資料，第一版可以先收集。
- **目前的例子：** 排氣量級距。第一版的檢驗規則用不到它，但作者決定必填並驗證：汽車級距照牌照稅級距，供之後計算牌照稅；機車級距依車種分級，只作紀錄（見 Technical Plan 第 2 節的對照表）。排氣量不是個資。
- **成本：** 先收集資料會有驗證、欄位和日後資料遷移的成本；若是個資，還要符合「只收集必要資料」的原則，所以每筆要先收集的資料都要個別評估。

技術：ASP.NET Core Web API（後端採用 Clean Architecture，遵守 SOLID；其中依賴方向由專案參考和架構測試強制）+ Vue SPA + PostgreSQL，用 Docker Compose 部署；介面只有繁體中文。

### 術語

| 用語 | 意思 |
| --- | --- |
| **定期檢驗** | 監理單位依道路交通安全規則第 44 條要求的檢驗（API 代碼 `PERIODIC`）。 |
| **排氣定檢** | 環保單位依空氣污染防制法第 40 條要求的機車排氣檢驗（API 代碼 `EXHAUST`）。 |
| **檢驗期間** | 可以去做某一次檢驗的日期範圍，由「期間起日」到「最後期限」，兩天都包含在內。 |
| **指定檢驗日期** | 行照上印的定期檢驗日期；定期檢驗的檢驗期間是它的前後一個月。使用者可以選填行照上的指定檢驗日期；沒填時由公式算出。 |
| **推算期間** | 滿 10 年後每年第二次的檢驗期間。法規沒有公開算法，系統用「指定檢驗日期 + 6 個月」推算。畫面上的推算標示在 G5 定義。 |
| **大型重型機車** | 排氣量逾 250cc 的燃油機車，簡稱「大型重機」。 |
| **Data Protection 金鑰** | ASP.NET Core 用來簽署和加密登入 cookie 的金鑰。 |
| **查詢服務** | Application 定義、只負責讀取資料的介面（例如 `IVehicleQueries`），和負責寫入的 Repository 相對。 |

## The Problem

台灣的汽機車有兩種強制檢驗，主管機關不同、規則不同，車主要自己記：

- **定期檢驗**（交通部，道路交通安全規則第 44 條）：自用小客車和大型重型機車（逾 250cc）出廠滿 5 年後每年一次，滿 10 年後每年兩次，要在行照上的指定檢驗日期前後一個月內完成。逾期會被處 900–1,800 元罰鍰，逾期 6 個月以上註銷牌照。
- **排氣定檢**（環境部，空氣污染防制法第 40 條）：燃油機車出廠滿 5 年後每年一次，要在原發照月份的前後一個月內完成（例如 3 月發照，期限是 2 月 1 日到 4 月 30 日）。

一個人有多台車時（例如一台汽車、一台機車、一台大型重機），每台車要做的檢驗種類、頻率和檢驗期間都不同，而且會隨車齡改變。監理單位和環保單位雖然會通知，但各自只通知自己的項目，車主看不到「我所有的車接下來各要驗什麼」。

**解法：** 使用者輸入行照上的資料（車種、排氣量、出廠日期、原發照日期，以及選填的指定檢驗日期），系統依法規算出每台車每種檢驗的下一個檢驗期間，並在進入檢驗期間時變色提示。

**這個解法的限制，使用者必須知道：**
- 系統沒有「已驗車」的紀錄，所以不判斷逾期，也不知道使用者是否已經驗過。
- 滿 10 年後每年第二次的檢驗期間，法規沒有公開算法，系統用推算期間表示。
- **「只會提早、不會晚」的原則：** 除了推算期間以外，系統的簡化做法只會讓顯示的最後期限比實際早：(1) 不處理假日順延（顯示的期限可能比實際早幾天）；(2) 行照只有出廠年月時填該月 1 日（車齡算得偏大）；(3) 月份加減落在不存在的日期時取該月最後一天（見[日期計算規則](#3-日期計算規則)）。推算期間可能比實際早，也可能比實際晚。
- 第一次指定檢驗日期的公式（D-R2）只依網路資料整理，作者決定不以真實行照比對。
- 系統只支援 4 種車種（見 G2）。電動大型重型機車、營業用車、貨車等不支援的車種，如果被登錄成支援的車種，算出的檢驗會不正確。

以上都會寫進畫面上的免責聲明，最終文字：「檢驗期間依您登錄的資料計算，僅供參考，請以行照、監理站及環保單位的正式通知為準。期限遇假日可能順延；標示「推算日期」的期間可能比實際早或晚。本系統不會提醒已過期或已完成的檢驗。本系統不支援電動大型重型機車、營業用車及貨車。」

## Goals

*   **G1 登入與名單：** 用 Google OIDC 登入，只申請 `openid email` 權限。只有環境變數 `ALLOWED_EMAILS` 名單上、而且 `email_verified = true` 的帳號能登入。每次請求都重新比對名單：作者從 `ALLOWED_EMAILS` 移除某人並重建 `api` 容器後，該使用者的下一個請求會收到 401。使用者以 Google 的 `sub` 識別。登入 cookie 有效 14 天，有使用就自動延長（ASP.NET Core 預設值）。
*   **G2 車輛管理：** 使用者可以新增、修改自己的車輛，每人最多 50 台（含垃圾桶）。
    *   車種（選單上的標籤）：自用小客車（非營業用）、一般燃油機車、大型重型機車、電動機車（40 馬力以下）。
    *   欄位：車牌、廠牌、型號、暱稱（選填）、顏色（選填）、車種、排氣量級距（電動機車以外必填）、出廠日期、原發照日期、行照上的指定檢驗日期（選填，只限自用小客車和大型重機）。指定檢驗日期欄位的說明文字：「每次驗完車，請改成新行照上的指定檢驗日期。」
    *   同一台車在兩個分頁或裝置被同時修改時，後儲存的一方會被擋下，並提示「資料已被修改，請重新整理」。
*   **G3 垃圾桶：** 刪除時先丟進垃圾桶，可以還原或永久刪除。丟進垃圾桶滿 30 天後就看不到；實際刪除發生在該使用者下一次新增或修改車輛時（不需要背景排程，見 Technical Plan 第 2 節）；不再使用的帳號，資料由作者人工處理（同帳號刪除）。車牌在同一使用者底下唯一（含垃圾桶），重複時提示去垃圾桶還原。
*   **G4 檢驗期間計算：** 檢驗規則寫在程式裡，在讀取時算出每台車每種適用檢驗的下一個檢驗期間（期間起日、最後期限、狀態、規則 ID、日期來源、是否為推算），不存進資料庫。
    *   有填指定檢驗日期時，定期檢驗以它為準；沒填時，用公式算出第一次指定檢驗日期（D-R2）。之後每年同一天一次，滿 10 年後加上推算期間；填過去的日期會自動往後推。完整規則見 [Technical Plan 第 3 節](#3-日期計算規則)。
*   **G5 顯示：** 列表可依最後期限（預設）或出廠日期排序。進入檢驗期間時日期變色。每個狀態都有文字標籤，不只靠顏色：

    | 狀態 | 何時出現 | 畫面 |
    | --- | --- | --- |
    | `EXEMPT` | 這台車不需要這種檢驗（目前只有電動機車的排氣定檢） | 「免檢」，不變色，沒有日期 |
    | `UPCOMING` | 今天在下一個檢驗期間之前 | 「下次檢驗期間」加日期，不變色 |
    | `IN_WINDOW` | 今天在檢驗期間內（含期間起日和最後期限當天） | 「檢驗期間內」加日期，變色 |
    | `UNCOMPUTABLE` | 規則表沒有對應的規則，或資料庫裡的資料不符合現行驗證 | 「無法計算，請確認車種與日期是否正確；若仍有問題請回報車牌」 |

    推算期間另外顯示**推算標示**，文字固定為「推算日期，請以行照為準」；使用者有填指定檢驗日期（`basis = LICENCE`），而且不是推算期間時，顯示「依行照指定檢驗日期」。依最後期限排序時，依該車所有檢驗中最早的最後期限排序，早的在前，免檢和無法計算排最後，同日依車牌排序；依出廠日期排序時，舊的在前，同日依車牌排序。列表上方和車輛詳細頁各顯示一次免責聲明。
*   **G6 日期：** 日期的輸入和顯示可用民國年或西元年，由使用者在個人設定選一次，預設民國；輸入用年、月、日三個下拉選單。時間點以 UTC 存放；日曆日期不轉時區；「今天」用台北時間。
*   **G7 資料保護：** 名單、Google Client Secret、資料庫密碼和 `MASTER_KEY` 都放在不進 git 的 `.env`。使用者的 Email 和 Data Protection 金鑰都以 `MASTER_KEY` 推導出的金鑰加密存放。pre-commit hook 和 CI 都會檢查有沒有密鑰被 commit 進 repo。
*   **G8 部署：** 在本機用 Docker Compose 執行一支部署腳本完成部署。
*   **G9 成功標準：** GitHub Actions CI 中以下四類測試全部通過：
    1. **G9-1 規則計算的單元測試。** 每個案例的名稱附上法規條號和規則版本日期，涵蓋 Technical Plan 第 3 節列出的每個邊界、每個 CUJ 的確切結果，以及各車種的組合。
    2. **G9-2 API + 真實 PostgreSQL 的整合測試。** 涵蓋登入名單、CSRF、錯誤代碼、同時編輯、車輛上限、垃圾桶 30 天清除。
    3. **G9-3 使用者資料隔離測試。** 使用者 A 不能讀寫使用者 B 的任何資料，包括車輛、垃圾桶和個人設定。
    4. **G9-4 民國／西元換算和日期下拉選單的前端單元測試。**

## Non-Goals

*   Email、LINE、推播通知，以及任何應用程式內的背景排程。
*   管理頁面、角色與權限、在畫面上編輯規則；規則不存資料庫。
*   帳號刪除。有人要求刪除資料時，由作者人工處理。
*   「已逾期」狀態、登記已驗車、驗車紀錄、「即將進入檢驗期間」的提前提示。
*   手動覆寫檢驗日期。例外：自用小客車和大型重機可以選填行照上的指定檢驗日期。
*   期限遇假日的順延計算。
*   牌照稅、燃料稅、保險、保養紀錄、加油紀錄。
*   電動大型重型機車（逾 40 馬力的電動機車）、營業用車、貨車等其他車種。車種選單的標籤和免責聲明會註明（見 G2）。
*   開放任何人註冊。
*   正式 VPS（k3s）部署、自動部署（CD）、正式的備份排程、更換 `MASTER_KEY`。
*   多語系。
*   前端元件測試和 E2E 測試（民國／西元換算的單元測試除外）。

## Decisions

以下是撰寫本文件前後已經決定的事項。法規類（`D-R*`）是作者對法規的理解，由作者在 2026-11-19 確認，只做過網路查證；技術類（`D-T*`）是作者的技術選擇。

### 法規規則

*   **D-R1 定期檢驗：** 自用小客車和大型重型機車出廠未滿 5 年免驗；滿 5 年、未滿 10 年每年一次；滿 10 年起每年兩次；期限為指定檢驗日期前後一個月內。測試案例名稱要附上條號（道路交通安全規則第 44 條）和規則版本日期（作者指定為 2026-01-01，不是條文實際修正日期，見詳細實作計畫 1.2 節）。
*   **D-R2 第一次指定檢驗日期：** 第一次指定檢驗日期 = 原發照日期 +（依[間隔年數表](#3-日期計算規則)）年；車齡用完整出廠日期計算。例：CUJ 3 的車出廠 103/05/01、原發照 103/08/20，相隔未逾 1 年，+ 5 年，第一次指定檢驗日期為 108/08/20。作者決定直接採用，不另外用真實行照比對；如果日後發現和行照不一致，使用者可以填指定檢驗日期覆蓋，系統也會記 `LICENCE_DATE_MISMATCH` log 供修正規則。
*   **D-R3 排氣定檢：** 燃油機車（白牌、黃牌、紅牌，二行程和四行程）出廠未滿 5 年免排氣定檢，滿 5 年起每年一次，檢驗期間為原發照月份前一個月 1 日到後一個月最後一天（以月計）；電動機車終身免排氣定檢。大型重機另外還有監理單位的定期檢驗，規則和自用小客車相同（D-R1）。
*   **D-R4 汽車排氣量級距：** 照牌照稅級距：500cc 以下、501–600、601–1200、1201–1800、1801–2400、2401–3000、3001–4200、4201–5400、5401–6600、6601–7800、7801cc 以上，加上「電動」選項。機車級距見 Technical Plan 第 2 節的對照表。
*   **D-R5 檢驗期間邊界：** 期間起日和最後期限當天都算在檢驗期間內（例如 2/10–4/10，兩天都顯示「檢驗期間內」）。第一版只放現行條文；目前沒有已公布、尚未生效的修正，所以沒有過渡條款要處理（見 OQ-1）。

### 技術選擇

*   **D-T1 四個專案：** 後端分成四個專案：Domain、Application、Infrastructure、Web，依賴方向由專案參考強制（每層放什麼在 Technical Plan 寫明）。Clean Architecture 本身只規定「依賴由外往內」，沒有規定要拆專案；拆成四個專案是為了讓編譯器強制這條規則。
*   **D-T2 寫入用 Repository、讀取用查詢服務：**
    *   **寫入：** 新增、修改、丟進垃圾桶、還原、永久刪除，透過 Application 定義的 Repository 介面（`IVehicleRepository`、`IUserRepository`）。
    *   **讀取：** 車輛列表、單台車、垃圾桶列表，透過 Application 定義的查詢服務（`IVehicleQueries`），直接回傳畫面需要的資料。
    *   **例外：** 使用者只有曆法偏好一個可讀的設定，`/api/me` 的讀取併入 `IUserRepository`，不另設查詢服務。
    *   **實作：** 兩者都由 Infrastructure 用 EF Core 實作。Application 專案不參考 EF Core。
*   **D-T3 handler 介面：** 使用案例用自己定義的 handler 介面組織：Application 定義 `ICommandHandler<TCommand>`、`IQueryHandler<TQuery, TResult>`，每個使用案例一個實作類別，用 ASP.NET Core 內建 DI 註冊，API 端點注入對應的 handler。不使用 MediatR 等外部套件。第一版沒有 handler pipeline，也不預留 decorator 等擴充點。
*   **D-T4 測試專案：** 兩個。`UnitTests` 放不需要資料庫的測試（Domain 規則引擎與車牌正規化、Application handler、Infrastructure 的加密）和架構測試；`IntegrationTests` 放 API + PostgreSQL（Testcontainers）的整合測試和資料隔離測試。已知取捨：架構測試沒辦法在 CI 上設成獨立的必要檢查。
*   **D-T5 命名（不影響設計的命名選擇，作者可否決）：** 方案和根命名空間 `VehicleGo`（取自 repo 名稱）；專案 `VehicleGo.Domain`、`VehicleGo.Application`、`VehicleGo.Infrastructure`、`VehicleGo.Web`、`VehicleGo.UnitTests`、`VehicleGo.IntegrationTests`；資料夾 `src/`（後端）、`tests/`（後端測試）、`web/`（Vue 前端）。
*   **D-T6 Controller：** API 用 Controller，不用 Minimal API。
*   **D-T7 其他技術選擇：** .NET 10（EF Core 10 具名查詢過濾器需要）；前端 TypeScript + Vue Router（CI 要求 `typecheck`）。以下由作者決定：前端狀態管理用 Vue 內建 composable（不用 Pinia）；UI 元件庫用 Element Plus；後端測試框架用 xUnit；架構測試用 ArchUnitNET；結構化 log 用 .NET 內建的 JSON console logger（不用 Serilog）。

## Open Questions

以下是延後決定的事項，各有觸發條件。

*   **OQ-1 規則生效日與版本選擇：** 第一版只放現行規則。觸發：法規修正時，依當時的過渡條款決定用哪個日期判斷新舊規則（今天、原發照日期或出廠日期）。每條規則都記錄 `EffectiveFrom`（規則版本日期，作者指定為 2026-01-01），測試案例命名本來就需要這個日期，所以不算為之後的功能預先寫程式；`RuleTable.Current()` 只是取得全部規則的單一入口，不含過渡邏輯。
*   **OQ-2 正式 VPS 部署（作者決定維持延後）：** 觸發：作者決定部署到正式 VPS 時。作者還沒確定會部署到正式機，這個系統可能只會是實驗專案。VPS 是 k3s，前面有 Cloudflare 代理和原點憑證；分支安排、反向代理與 forwarded headers、自動部署、正式備份排程、獨立的 migration 步驟（migration bundle）、更換金鑰的流程，都等真的決定部署時再處理，也可能永遠不做。

## Critical User Journeys (CUJs)

以下 CUJ 3、4、4b、4c、5、6、12 的「今天」都是 2026-11-19（民國 115/11/19），使用者已登入，曆法設定為民國。

### CUJ 1：第一次登入

*   **Given**：使用者的 Google 帳號 Email 在 `ALLOWED_EMAILS` 名單上，且已經過 Google 驗證，從來沒有登入過。
*   **When**：使用者打開網站，按「用 Google 登入」，在 Google 完成驗證。
*   **Then**：系統建立使用者資料（Google `sub`、加密的 Email，曆法預設民國），導回車輛列表。列表是空的，顯示「新增車輛」按鈕。

### CUJ 2：不在名單上的人嘗試登入

*   **Given**：使用者的 Google 帳號不在名單上。
*   **When**：使用者完成 Google 登入。
*   **Then**：系統顯示「此帳號尚未開放使用」，不建立使用者資料，也不自動重新導向 Google。

### CUJ 3：新增一台車齡超過 10 年、沒有填指定檢驗日期的汽車

*   **When**：新增一台自用小客車，排氣量選「1201–1800cc」，出廠日期 103/05/01，原發照日期 103/08/20，指定檢驗日期不填。
*   **Then**：列表上這台車的「定期檢驗」顯示 **116/01/20 – 116/03/20**，狀態「下次檢驗期間」，旁邊顯示推算標示，日期不變色。
    *   計算過程：出廠到發照相隔未逾 1 年，第一次指定檢驗日期 = 108/08/20；113/05/01 滿 10 年，之後每年兩次；今年的指定檢驗日期 115/08/20 的檢驗期間（115/07/20–115/09/20）已經結束；下一個是推算的 115/08/20 + 6 個月 = 116/02/20，檢驗期間 116/01/20–116/03/20。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2027-01-20`、`deadline = 2027-03-20`、`basis = FORMULA`、`estimated = true`、`ruleId = RTS44-CAR-AGE10UP`。

### CUJ 4：填了指定檢驗日期的汽車

*   **Given**：使用者有一台自用小客車，出廠日期 108/06/01，原發照日期 108/07/15（車齡約 7 年），指定檢驗日期原本沒填。
*   **When**：使用者在編輯頁填入行照上的指定檢驗日期 116/03/10 並儲存。
*   **Then**：定期檢驗顯示 **116/02/10 – 116/04/10**，狀態「下次檢驗期間」，旁邊標示「依行照指定檢驗日期」。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2027-02-10`、`deadline = 2027-04-10`、`basis = LICENCE`、`estimated = false`、`ruleId = RTS44-CAR-LICENCE`。
    *   公式算出的指定檢驗日期是每年 7/15（第一次 113/07/15），和填入的 3/10 不同，依[第 3 節](#3-日期計算規則)判定不一致：儲存時伺服器寫一筆 `LICENCE_DATE_MISMATCH` log，畫面仍以填入的指定檢驗日期為準。

### CUJ 4b：指定檢驗日期已經過了

*   **Given**：和 CUJ 4 同一台車。
*   **When**：使用者填入的指定檢驗日期是 115/03/10（上一次驗車時行照上的指定檢驗日期，檢驗期間 115/02/10–115/04/10 已經結束）。
*   **Then**：系統自動往後推一年，定期檢驗顯示 **116/02/10 – 116/04/10**，狀態「下次檢驗期間」，標示「依行照指定檢驗日期」。API 回應和 CUJ 4 相同（`basis = LICENCE`、`ruleId = RTS44-CAR-LICENCE`）。

### CUJ 4c：滿 10 年的車填了下一次的指定檢驗日期

*   **Given**：使用者有 CUJ 3 那台車（出廠 103/05/01、原發照 103/08/20，已滿 10 年），剛驗完車。
*   **When**：使用者照欄位說明，填入新行照上的指定檢驗日期 116/08/20。
*   **Then**：定期檢驗顯示 **116/01/20 – 116/03/20**，狀態「下次檢驗期間」，旁邊顯示推算標示。
    *   計算過程：L = 116/08/20；L − 1 年（115/08/20）當天車齡已滿 10 年，所以補上 L − 6 個月 = 116/02/20 的推算期間（116/01/20–116/03/20），它比 L 的檢驗期間（116/07/20–116/09/20）早，所以先顯示。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2027-01-20`、`deadline = 2027-03-20`、`basis = LICENCE`、`estimated = true`、`ruleId = RTS44-CAR-LICENCE`。
    *   填入的 8/20 和公式算出的指定檢驗日期月日相同，判定一致，不寫 log。

### CUJ 5：新增一台大型重機

*   **When**：新增一台大型重型機車，排氣量選「551cc 以上」，出廠日期 108/09/01，原發照日期 108/11/25。
*   **Then**：列表上這台車有兩行，兩行都在檢驗期間內，日期都變色：
    *   「定期檢驗」：**115/10/25 – 115/12/25**，狀態「檢驗期間內」。第一次指定檢驗日期 113/11/25（相隔未逾 1 年），今年的指定檢驗日期是 115/11/25；車齡未滿 10 年，沒有推算期間。API：`kind = PERIODIC`、`status = IN_WINDOW`、`earliestDate = 2026-10-25`、`deadline = 2026-12-25`、`basis = FORMULA`、`estimated = false`、`ruleId = RTS44-HMC-AGE5TO10`。
    *   「排氣定檢」：**115/10/01 – 115/12/31**，狀態「檢驗期間內」。原發照月份 11 月，檢驗期間是前一個月 1 日到後一個月最後一天；出廠已滿 5 年。API：`kind = EXHAUST`、`status = IN_WINDOW`、`earliestDate = 2026-10-01`、`deadline = 2026-12-31`、`basis = FORMULA`、`estimated = false`、`ruleId = APC40-MC-ANNUAL`。

### CUJ 6：新增一台電動機車

*   **When**：新增一台電動機車，出廠日期 110/03/01，原發照日期 110/03/15。表單上不顯示排氣量和指定檢驗日期欄位。
*   **Then**：列表顯示「排氣定檢：免檢」，不變色。API：`kind = EXHAUST`、`status = EXEMPT`、`earliestDate` 和 `deadline` 為 null、`ruleId = APC40-EMC-EXEMPT`。依最後期限排序時，這台車排在有日期的車後面。

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
*   **When**：作者從 `.env` 的 `ALLOWED_EMAILS` 移除這位使用者的 Email，並執行 `./deploy.sh` 重建容器。
*   **Then**：使用者的下一個請求收到 401，SPA 導向登入頁。再次登入時，看到「此帳號尚未開放使用」。

### CUJ 11：切換曆法

*   **Given**：使用者已登入，曆法設定為民國。
*   **When**：在設定頁改成西元。
*   **Then**：所有頁面的日期顯示和輸入都改用西元。換一台裝置登入時，仍然是西元。

### CUJ 12：新增一台未滿 5 年的汽車

*   **When**：新增一台自用小客車，出廠日期 112/03/01，原發照日期 112/04/10，指定檢驗日期不填。
*   **Then**：定期檢驗顯示 **117/03/10 – 117/05/10**，狀態「下次檢驗期間」，不變色。
    *   計算過程：出廠到發照相隔未逾 1 年，第一次指定檢驗日期 = 112/04/10 + 5 年 = 117/04/10，檢驗期間是前後一個月。
    *   API 回應：`kind = PERIODIC`、`status = UPCOMING`、`earliestDate = 2028-03-10`、`deadline = 2028-05-10`、`basis = FORMULA`、`estimated = false`、`ruleId = RTS44-CAR-AGE0TO5`。

## Technical Plan (Architecture)

### 1. 整體架構

後端採用 Clean Architecture，分成四個 .NET 專案，依賴只能由外往內，並由專案參考強制（D-T1）。前端是獨立的 Vue SPA，建置後的靜態檔由 Web 專案提供，所以前後端在同一個網域。

```mermaid
flowchart TD
    Browser["瀏覽器（Vue SPA）"]
    Web["VehicleGo.Web<br/>Controllers、OIDC + Cookie 登入、名單檢查掛勾、CSRF、<br/>ProblemDetails、健康檢查、靜態檔與 SPA fallback、相依性組裝"]
    App["VehicleGo.Application<br/>命令／查詢 handler、Repository 與查詢服務介面、<br/>名單檢查、錯誤代碼"]
    Infra["VehicleGo.Infrastructure<br/>EF Core 實作 Repository 與查詢服務、加密、<br/>Data Protection 金鑰存放"]
    Domain["VehicleGo.Domain<br/>實體、車種與級距、車牌正規化、<br/>檢驗規則引擎（純函式）"]
    DB[("PostgreSQL<br/>（Docker Compose 的 db 服務）")]

    Browser -- "http://localhost:8080<br/>同網域 cookie + XSRF header" --> Web
    Web -- "呼叫 handler" --> App
    Web -. "只在組裝相依性時參考" .-> Infra
    Infra -- "實作介面" --> App
    App --> Domain
    Infra --> Domain
    Infra -- "Npgsql" --> DB
```

### 2. 各層內容

**VehicleGo.Domain（不參考任何其他專案或框架套件）**
- **實體與值物件：** `Vehicle`（車牌、正規化車牌、廠牌、型號、暱稱、顏色、車種、排氣量級距、出廠日期、原發照日期、指定檢驗日期、刪除時間、擁有者 ID）、`User`。
- **固定代碼：** 車種（`CAR`、`FUEL_MC`、`HEAVY_MC`、`ELECTRIC_MC`）、排氣量級距、曆法（`ROC`、`GREGORIAN`）。級距清單附法規出處，並定義下列「車種 → 可用級距 → 必填」對照表：

    | 車種 | 可用級距 | 級距必填 | 可填指定檢驗日期 |
    | --- | --- | --- | --- |
    | `CAR` 自用小客車（非營業用） | 汽車 11 級（D-R4）加上 `CAR_ELECTRIC` | 是 | 是 |
    | `FUEL_MC` 一般燃油機車 | `MC_LE_50`（50cc 以下）、`MC_51_250`（51–250cc） | 是 | 否 |
    | `HEAVY_MC` 大型重型機車 | `MC_251_550`（251–550cc，黃牌）、`MC_GT_550`（551cc 以上，紅牌） | 是 | 是 |
    | `ELECTRIC_MC` 電動機車（40 馬力以下） | 無 | 否（必須是 null） | 否 |

- **車牌正規化：** NFKC 全形轉半形、轉大寫、去掉空白和連字號。
- **檢驗規則引擎：** 純函式，輸入（車輛資料、今天的日期），輸出檢驗項目清單。每一項包含：種類、狀態、期間起日、最後期限、規則 ID、日期來源、是否推算，以及只給伺服器使用的診斷資訊。規則表和期間算法（以發照月日、以發照月份、以指定檢驗日期、每年兩次 + 6 個月推算、免檢）都在這裡，用型別或 enum 表示。日期怎麼算見第 3 節。
- **領域規則：** 車輛上限 50、輸入驗證（日期先後、級距與車種相符、指定檢驗日期只限自用小客車和大型重機），回傳固定的錯誤代碼，不拋出框架例外。

**VehicleGo.Application（只參考 Domain）**
- **handler 介面（D-T3）：** `ICommandHandler<TCommand>`、`IQueryHandler<TQuery, TResult>`，每個使用案例一個實作類別。結果用統一的型別表示成功，或失敗並附錯誤代碼。
- **命令（寫入，走 Repository，D-T2）：** 新增車輛、修改車輛（帶版本號）、丟進垃圾桶、還原、永久刪除、修改曆法偏好、登入時建立或更新使用者。
- **查詢（讀取）：** 車輛列表（含排序）、單台車、垃圾桶列表走查詢服務；目前使用者（`/api/me`）走 `IUserRepository`（D-T2 的例外）；級距清單直接讀 Domain 的固定資料。車輛列表和單台車的查詢服務只負責取出資料；檢驗期間由 handler 呼叫 Domain 的規則引擎計算後再組成回應，所以查詢服務本身不需要知道規則。
- **對外介面（由 Infrastructure 或 Web 實作）：** `IVehicleRepository`、`IUserRepository`、`IVehicleQueries`、`IUnitOfWork`（在同一個資料庫交易裡執行多個步驟）、`IEmailProtector`（加密 Email、算查詢雜湊）、`ICurrentUser`（目前使用者 ID，Web 從 cookie 讀取）。時間一律透過 .NET 的 `TimeProvider` 取得，再換算成台北時間的日期。
- **名單檢查：** `LoginAdmission`（比對 `ALLOWED_EMAILS`、要求 `email_verified = true`），由 Web 的 OIDC 事件和 cookie 驗證事件共同呼叫。
- **指定檢驗日期不一致的判斷：** 新增和修改車輛的 handler 讀取規則引擎回傳的診斷資訊，決定是否寫 `LICENCE_DATE_MISMATCH` log；讀取時不寫。

**VehicleGo.Infrastructure（參考 Application、Domain）**
- **資料庫：** EF Core + Npgsql 的 `AppDbContext`，兩個具名查詢過濾器（擁有者、已刪除）；`xmin` 當作同時編輯檢查的版本號；`(owner_id, normalized_plate)` 唯一索引。車牌重複在寫入前先查詢判斷（使用中或垃圾桶），唯一索引只當同時寫入時的最後防線。
- **垃圾桶自動清除（不需要背景排程）：** 新增和修改車輛時，先永久刪除目前使用者丟進垃圾桶滿 30 天的車，再檢查車牌唯一和 50 台上限；只有這兩件事需要真正刪除。其他查詢和操作都把滿 30 天的車視為不存在。
- **Repository 與查詢服務：** 實作 Application 的三個介面（`IVehicleRepository`、`IUserRepository`、`IVehicleQueries`）。垃圾桶相關查詢和車輛數量只關閉「已刪除」過濾器，不能關閉擁有者過濾器。
- **加密：** 從 `MASTER_KEY` 用 HKDF 推導三把金鑰：Email 加密（標籤 `email-enc/v1`）、Email 查詢雜湊（`email-hmac/v1`，`email_lookup_hash` = HMAC-SHA256(這把金鑰, 去空白並轉小寫的 Email)，用途是收到人工刪除請求時，用 Email 找出使用者）、Data Protection 金鑰加密（`dataprotection/v1`）；共用的 AES-GCM 工具；`IEmailProtector` 的實作。
- **Data Protection：** 獨立的小 DbContext 存放 Data Protection 金鑰，加上自行實作的 XML 加密器和解密器。
- **migration：** EF Core migration（`AppDbContext` 和 Data Protection 的 DbContext 各一組），第一版在 `api` 啟動時自動套用，因為本機只有一個執行個體，不需要另一個部署步驟。部署到 VPS 時，要改成獨立的 migration 步驟（多個執行個體同時在啟動時套用 migration 會互相衝突），列在 OQ-2。

**VehicleGo.Web（參考 Application；組裝相依性時參考 Infrastructure）**
- **Controllers（D-T6）：** `AuthController`（登入、登出）、`MeController`（讀取使用者、修改偏好）、`VehiclesController`（列表、單台、新增、修改、丟進垃圾桶、還原、永久刪除）、`TrashController`（垃圾桶列表）、`ReferenceController`（級距清單）。每個 action 只呼叫一個 handler。
- **登入：** 預設的驗證方式是 cookie，Google 透過 OIDC 登入；`/api` 未登入時回傳 401，不轉址；cookie 驗證事件每次請求都重新檢查名單。
- **CSRF：** 用框架內建的 `IAntiforgery`。先檢查登入，再檢查 CSRF。
- **錯誤處理：** Application 回傳的錯誤代碼，在這一層統一轉成 ProblemDetails 和 HTTP 狀態碼。
- **其他：** 健康檢查（`/health/live`、`/health/ready`）；提供 Vue 建置後的靜態檔，並把非 `/api` 的路徑導向 `index.html`；啟動時檢查 `MASTER_KEY`、Asia/Taipei 時區、`ALLOWED_EMAILS`。正式程式碼裡**沒有**任何測試用的登入端點，避免設定錯誤時被拿來登入；整合測試需要的登入端點只存在於 `IntegrationTests` 專案。

**前端（`web/`，Vue SPA）**
- **頁面：** 登入、此帳號尚未開放使用、車輛列表、新增與編輯車輛、垃圾桶、設定（曆法）。
- **共用模組：**
  - API 呼叫：自動帶 `X-XSRF-TOKEN`；收到 401 時導向登入頁；收到 `CSRF_INVALID` 時重新取得 token 後重試一次。
  - 民國／西元換算和日期下拉選單的邏輯，獨立成一個模組，用 Vitest 測試。
- 前端只依 API 回傳的 `status`、`basis`、`estimated` 決定顏色和標示，**不做任何檢驗日期計算**。
- 前端技術：TypeScript、Vue Router、Vue 內建 composable（不用 Pinia）、Element Plus（D-T7）。

### 3. 日期計算規則

- **月份加減落在不存在的日期：** 取該月最後一天，例如 3/31 − 1 個月 = 2/28（閏年 2/29）；8/31 + 6 個月 = 2/28（閏年 2/29）。最後期限因此一律較早，符合「只會提早」。期間起日也取較早的日期，可能讓使用者提早一兩天前往；接受這個風險（監理站現場會說明）。
- **週年日：** 一律從原發照日期（或指定檢驗日期 L）直接加 N 年計算，不從前一年累加。2/29 發照的車，非閏年的週年日取 2/28，閏年仍是 2/29。
- **排氣定檢從哪一年開始：** 最後期限在「出廠日期 + 5 年」當天或之後的第一個檢驗期間就要驗。例：出廠 2019-03-10、3 月發照，「出廠日期 + 5 年」是 2024-03-10，2024 年 2/1–4/30 這個檢驗期間的最後期限在它之後，所以要驗。
- **推算期間：** 某個指定檢驗日期（填入的 L 系列，或公式算出的日期）當天車齡已滿 10 年時，才產生推算期間：日期為該日 + 6 個月，檢驗期間是它的前後一個月。有填指定檢驗日期 L 時，也檢查 L − 1 年，所以會補上 L − 6 個月的推算期間（見 CUJ 4c）。
- **車齡和間隔的比較：** 某日 X 車齡「滿 N 年」，就是 X 在出廠日期 + N 年當天或之後；間隔年數表的「未逾 N 年」，就是原發照日期在出廠日期 + N 年當天或之前。
- **不一致的判斷：** 填入的指定檢驗日期，月日和同年度公式算出的指定檢驗日期不同時，判定不一致；某個指定檢驗日期當天車齡已滿 10 年時，和公式日期 + 6 個月的月日相同，也算一致。填入的日期早於第一次公式日期時，改和第一次公式日期比對月日。
- **間隔年數表（第一次指定檢驗日期，D-R2）：** 依出廠日期到原發照日期的間隔，原發照日期加上：

    | 出廠到原發照的間隔 | 加幾年 |
    | --- | --- |
    | 未逾 1 年（含剛好 1 年） | + 5 |
    | 逾 1 年、未逾 2 年（含剛好 2 年） | + 4 |
    | 逾 2 年、未逾 3 年（含剛好 3 年） | + 3 |
    | 逾 3 年、未逾 4 年（含剛好 4 年） | + 2 |
    | 逾 4 年 | + 1 |

### 4. 主要資料流

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
      → Domain 驗證（日期、級距與車種、指定檢驗日期）── 失敗 → 400 + 錯誤代碼
      → IUnitOfWork：以下在同一個交易裡執行
          → 鎖住使用者（同一使用者同時送出的寫入會排隊執行）
          → 永久刪除自己滿 30 天的垃圾桶車輛
          → 查詢同一車牌 ── 使用中 → 409 PLATE_EXISTS_ACTIVE；垃圾桶 → 409 PLATE_EXISTS_IN_TRASH
          → 計算車輛數（含垃圾桶）≥ 50 → 409 VEHICLE_LIMIT_REACHED（上限精確，不會變成 51 台）
          → 建立 Vehicle（正規化車牌）、儲存
      → 規則引擎診斷：指定檢驗日期與公式不一致 → 寫 LICENCE_DATE_MISMATCH log
  ← 201 新車輛（含 inspections、version）
```

**修改車輛**
```text
SPA ── PUT /api/vehicles/{id}（帶 X-XSRF-TOKEN、version）──► 登入檢查 → CSRF 檢查 → VehiclesController
  → UpdateVehicleCommandHandler
      → Domain 驗證 ── 失敗 → 400 + 錯誤代碼
      → IUnitOfWork：以下在同一個交易裡執行
          → 鎖住使用者、永久刪除自己滿 30 天的垃圾桶車輛
          → 取出車輛 ── 不存在 → 404 VEHICLE_NOT_FOUND；在垃圾桶 → 409 VEHICLE_IN_TRASH
          → 比對版本號 ── 不一致 → 409 VEHICLE_MODIFIED
          → 查詢同一車牌（排除自己）── 409 PLATE_EXISTS_ACTIVE / PLATE_EXISTS_IN_TRASH
          → 儲存（同時寫入時，版本號檢查是後備）
      → 規則引擎診斷 → 寫 log
  ← 200 車輛（含 inspections、新的 version）
```

**登入與每次請求的名單檢查**
```text
GET /api/auth/login?returnUrl=/ → 檢查 returnUrl → OIDC Challenge → Google
Google 回呼 /signin-oidc → OnTokenValidated → LoginAdmission
    ├─ 不通過 → 導向「此帳號尚未開放使用」（不建立使用者）
    └─ 通過 → 建立或更新使用者（同時第一次登入只會建立一筆）
             → 寫入登入 cookie，含三個 claim：內部使用者 ID（users.id）、Google sub、Email
之後每個請求 → cookie 驗證事件 → LoginAdmission（比對 cookie 裡的 Email，不查資料庫）
    ├─ 已不在名單上 → 清除 cookie → 401
    └─ 通過 → ICurrentUser 直接從 cookie 讀取使用者 ID（不查資料庫）
```

### 5. 資料模型（摘要，完整欄位在詳細實作計畫）

| 資料表 | 主要欄位 | 說明 |
| --- | --- | --- |
| `users` | 內部 ID、`google_sub`（唯一）、`email_encrypted`、`email_lookup_hash`（一般索引）、`calendar_preference`（預設 `ROC`）、建立和修改時間 | Email 有兩份：cookie 裡的 Email claim（名單比對、`/api/me` 顯示）；資料庫裡加密的 Email（只用來處理人工刪除請求） |
| `vehicles` | 內部 ID、`owner_id`、`plate`、`normalized_plate`、廠牌、型號、暱稱、顏色、`vehicle_type`、`displacement_bracket`、`manufacture_date`、`original_issue_date`、`licence_inspection_date`、`deleted_at`、建立和修改時間、`xmin`（版本號） | `(owner_id, normalized_plate)` 唯一；check constraint 限制級距和指定檢驗日期只出現在對應的車種 |
| `data_protection_keys` | 框架定義的欄位 | 存在獨立的 DbContext，內容已加密 |

日曆日期（出廠、原發照、指定檢驗日期）用 `date`，時間點用 `timestamptz`。檢驗期間不存進資料庫。

### 6. API 一覽（完整的請求、回應和錯誤代碼在詳細實作計畫）

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

### 7. 安全與設定

- 所有設定都從環境變數讀取，本機由 `.env` 提供：`ALLOWED_EMAILS`、Google Client ID 和 Client Secret、資料庫連線、`MASTER_KEY`。`.env` 不進 git，repo 裡只有 `.env.example`。
- Email 和 Data Protection 金鑰都用 `MASTER_KEY` 推導出的金鑰，以 AES-GCM 加密。第一版不支援更換金鑰；金鑰遺失時清空資料庫重建。
- `returnUrl` 只接受站內相對路徑，拒絕 `//`、`/\` 開頭，不合法時改導回 `/`，避免登入流程被用來導向外部網站。
- pre-commit hook 和 CI 都會執行 `check-secrets.sh` 和 gitleaks。
- log 不寫車牌，Email 只寫查詢雜湊的前 8 碼。

### 8. 部署（本機 Docker Compose）

| 服務 | 說明 |
| --- | --- |
| `db` | PostgreSQL，用 `pg_isready` 做健康檢查 |
| `api` | Web 專案（含 Vue 靜態檔），等 `db` 健康後才啟動，啟動時套用 migration，`restart: unless-stopped` |

部署腳本 `deploy.sh`：匯出 git SHA，執行 `docker compose up -d --build`。本機資料庫可以隨時清空重建，所以第一版不在部署時備份；備份和 migration bundle 等部署 VPS 時再設計（OQ-2）。

### 9. 測試對應

| 測試專案（D-T4） | 內容 | 對應成功標準 |
| --- | --- | --- |
| `VehicleGo.UnitTests` | Domain 規則引擎與車牌正規化（`Domain/*` 測試滿足 G9-1）；Application handler（用假的 Repository 和查詢服務）、Infrastructure 的加密與 HKDF、架構測試（檢查四個專案的依賴方向）是額外的保護 | G9-1 |
| `VehicleGo.IntegrationTests` | Web + PostgreSQL（Testcontainers）：登入名單、CSRF、錯誤代碼、同時編輯、車輛上限、垃圾桶 30 天清除、資料隔離；log、健康檢查測試是額外的保護 | G9-2、G9-3 |
| `web/`（Vitest） | 民國／西元換算、日期下拉選單 | G9-4 |

## Risks & Mitigations

| 風險 | 類別 | 影響 | 緩解方式 | 狀態 |
| --- | --- | --- | --- | --- |
| 規則和法規不一致（作者對法規的理解有誤） | 正確性 | 顯示錯誤的檢驗期間；測試的預期值和規則出自同一人的理解，測試抓不到 | 法規條號和規則版本日期寫進每個測試名稱，方便對照原文；免責聲明請使用者以正式通知為準；使用者可以填指定檢驗日期覆蓋 | 接受 |
| 規則實作算錯日期 | 正確性 | 顯示錯誤的檢驗期間 | G9-1 的單元測試涵蓋每條規則和第 3 節的每個邊界；CUJ 3、4、4b、4c、5、6、12 寫出確切結果 | 已緩解 |
| 第一次指定檢驗日期公式（D-R2）和真實行照不一致 | 正確性 | 沒填指定檢驗日期的車，顯示的日期可能不對 | 使用者填指定檢驗日期覆蓋；新增、修改時寫 `LICENCE_DATE_MISMATCH` log 供修正規則 | 接受 |
| 推算期間比實際晚 | 正確性 | 使用者可能錯過第二次檢驗 | 推算標示；免責聲明說明可能比實際早或晚；考慮過必填指定檢驗日期（ALT-C12），作者否決 | 接受 |
| 不支援的車種（電動大型重型機車、營業用車、貨車）被登錄成支援的車種 | 正確性 | 少算檢驗 | 車種選單標籤註明「非營業用」「40 馬力以下」；免責聲明列出不支援的車種；第一版只有名單上的使用者 | 接受 |
| 重建容器會清掉 log（包括 `LICENCE_DATE_MISMATCH`） | 維運 | `docker compose up -d --build` 或 Docker 輪替 log 後，不一致的紀錄遺失，無法用來修正規則 | 作者在部署前自行查看 log；考慮過存進資料庫（ALT-C11），作者否決 | 接受 |
| 法規修改沒有過渡邏輯 | 正確性 | 修法後舊車可能套錯規則 | 每條規則有 `EffectiveFrom`；觸發時依 OQ-1 處理 | 延後（OQ-1） |
| 自寫的 AES-GCM／HKDF 出錯 | 安全 | Email 或 Data Protection 金鑰的保護失效，或資料無法解密 | 使用 .NET 內建的 `AesGcm`、`HKDF` 類別，只自己組合格式；單元測試涵蓋往返、用錯金鑰失敗、用途字串不同時失敗、存的不是明文、固定測試向量 | 已緩解 |
| `MASTER_KEY` 遺失 | 維運 | 加密的 Email 和 Data Protection 金鑰無法解密，所有人要重新登入 | 第一版：清空資料庫重建（`docker compose down -v`）；本機資料可以重建 | 接受 |
| `MASTER_KEY` 和資料庫在同一台主機 | 安全 | 拿到這台電腦的人可以解密所有 Email，也可以偽造任何人的登入 cookie | 本機只是測試環境；正式部署時金鑰和資料庫、備份分開保存（OQ-2） | 接受 |
| 列表在記憶體排序、不分頁 | 規模 | 車輛很多時列表變慢 | 每人上限 50 台（G2） | 已緩解 |
| `api` 啟動時套用 migration 失敗 | 維運 | `api` 無法啟動 | 第一版：清空資料庫重建後重新部署；VPS 改用獨立的 migration 步驟（OQ-2） | 接受 |
| Google OIDC 故障，或 redirect URI 設錯 | 可用性 | 沒有人能登入 | 沒有替代登入方式；README 寫明 redirect URI 設定 | 接受 |
| 用區網 IP 的 http 連線 | 可用性 | OIDC 的 cookie 需要 `localhost` 或 HTTPS，瀏覽器會拒絕，無法登入 | README 註明只能用 `localhost` 連線；正式部署時改用 HTTPS（OQ-2） | 接受 |
| 密鑰被 commit 進公開 repo | 安全 | 名單、Google Client Secret 或 `MASTER_KEY` 外洩 | `.gitignore` 第一個 commit 就排除 `.env`；pre-commit hook 和 CI 都執行 `check-secrets.sh` 和 gitleaks（CI 掃完整歷史） | 已緩解 |

## Alternatives Considered

以下是在腦力激盪（[concepts_brief.md](../../concepts_brief.md)）和本文件撰寫過程中考慮過、但沒有採用的做法。每一項列出優點、缺點和否決原因。標示「作者否決」的是作者的決定；「接受審查建議」表示作者採納了獨立審查者的意見；「設計審查時再次確認」表示審查者再次提出、作者仍維持原決定；「設計審查後改變決定」表示作者因審查意見改變了原本的決定（審查意見見 [zoom_out_report.md](../../zoom_out_report.md)、[critic_report.md](../../critic_report.md)，作者的決定見 [goldfish_review_report.md](../../goldfish_review_report.md)）。

### A. 產品範圍

#### ALT-A1：只寄 Email 提醒，不做網頁
*   **優點**：使用者不必主動上網站查。
*   **缺點**：需要背景排程和寄信服務，範圍變大。
*   **否決原因**：作者否決：範圍太大，第一版先驗證規則計算，通知留到之後。

#### ALT-A2：使用者自己輸入下次驗車日（取代公式計算）
*   **優點**：不用實作法規規則，不會算錯。
*   **缺點**：系統的價值和行事曆提醒差不多。
*   **否決原因**：作者否決：規則自動計算才是核心價值。後來加入的指定檢驗日期是選填、只用於定期檢驗，沒填仍用公式計算，不屬於這個做法。

#### ALT-A3：顯示「已逾期」，或加「上次驗車日」欄位
*   **優點**：可以提醒真正錯過驗車的人。
*   **缺點**：沒有驗車紀錄，就無法分辨「準時驗過」和「真的逾期」，會誤報。
*   **否決原因**：作者否決：沒有驗車紀錄就無法誠實判斷逾期，期限過後直接顯示下一個檢驗期間，並以免責聲明說明。

#### ALT-A4：期限結束後 6 個月內顯示「上次檢驗期間已於 X 結束；如已完成請忽略」（`RECENTLY_ENDED`）
*   **優點**：在逾期罰款和註銷牌照前提供中性的提醒，不宣稱逾期、不變色。
*   **缺點**：多一種狀態、測試和畫面文字；已驗車的人也會看到最多 6 個月的提示。
*   **否決原因**：作者否決（設計審查時再次確認）：系統沒有已驗車紀錄，大多數已準時驗車的使用者也會連續 6 個月看到這個提示；作者選擇讓免責聲明承擔這個提醒。

#### ALT-A5：「即將進入檢驗期間」的提前提示、到期前 2 週變色
*   **優點**：提早提醒。
*   **缺點**：多一種狀態；「到期前 2 週」會在使用者已經可以去驗車之後才變色，意義不大。
*   **否決原因**：作者否決：只有進入檢驗期間才變色。

#### ALT-A6：第一版不收排氣量、不支援大型重機
*   **優點**：範圍最小。
*   **缺點**：大型重機有兩種檢驗，排除後無法涵蓋常見的車種。
*   **否決原因**：作者否決：大型重型機車要納入第一版。

#### ALT-A7：開放任何 Google 帳號註冊
*   **優點**：任何人都能使用。
*   **缺點**：第一版沒有帳號刪除，個資進來後刪不掉。
*   **否決原因**：作者否決：改用環境變數的登入名單。

### B. 登入與帳號

#### ALT-B1：Email + 密碼，或 Magic link 登入
*   **優點**：不依賴 Google。
*   **缺點**：要處理密碼雜湊、重設密碼、登入信寄送和垃圾信問題。
*   **否決原因**：作者否決：改用 Google 登入。

#### ALT-B2：只限 `@gmail.com`
*   **優點**：規則簡單。
*   **缺點**：排除公司或學校的 Google Workspace 帳號。
*   **否決原因**：作者否決：任何 Google 帳號都可以，另外用登入名單限制。

#### ALT-B3：以 Email 識別使用者
*   **優點**：直覺。
*   **缺點**：Email 可能改變或被別人重新註冊，會造成資料遺失或外洩。
*   **否決原因**：作者否決：改用 Google 的 `sub` 識別。

#### ALT-B4：用 Google 同意畫面的「Testing」測試帳號當名單
*   **優點**：不用寫程式。
*   **缺點**：登入時顯示「未經驗證」警告；名單要到 Google Cloud Console 修改；沒辦法寫整合測試。
*   **否決原因**：作者否決：採用程式內的 Email 名單。

#### ALT-B5：用 `sub` 當名單
*   **優點**：不受 Email 改變影響。
*   **缺點**：事先不會知道別人的 `sub`。
*   **否決原因**：作者否決：採用 Email 名單，並要求 `email_verified = true`。

#### ALT-B6：`AddGoogle`
*   **優點**：設定較少。
*   **缺點**：預設不帶出 `email_verified`，issuer 不是標準值。
*   **否決原因**：作者否決（接受審查建議）：改用 `AddOpenIdConnect`。

#### ALT-B7：JWT 存在 localStorage、SPA 自己處理 PKCE
*   **優點**：API 不需要 cookie。
*   **缺點**：token 會暴露在 XSS 攻擊下。
*   **否決原因**：作者否決（接受審查建議）：改用同網域的 HttpOnly cookie。

#### ALT-B8：ASP.NET Identity、Keycloak、Auth0、Duende BFF
*   **優點**：功能完整。
*   **缺點**：只用 Google 登入、沒有角色時太重。
*   **否決原因**：作者否決（接受審查建議）：只用 Google 登入、沒有角色，這些方案要多架一個服務或多一組資料表，帶來的功能第一版用不到。

### C. 規則與資料

#### ALT-C1：規則設定頁 + 權限管理、seed 管理者帳號
*   **優點**：不用改程式就能調整規則。
*   **缺點**：工作量多一倍；設定頁拿掉後，管理者帳號沒有事可做，還多了預設密碼的風險。
*   **否決原因**：作者否決：第一版規則寫在程式裡，改規則就是改程式並重新部署。

#### ALT-C2：規則存資料庫（seed + migration），或程式內一份規則表再產生 seed
*   **優點**：日後要做管理頁時比較容易。
*   **缺點**：沒有管理頁時，只是多一份要和程式同步的資料。
*   **否決原因**：作者否決：第一版沒有任何程式需要從資料庫讀規則，規則的唯一來源是程式碼。

#### ALT-C3：檢驗期間存進資料庫
*   **優點**：可以在資料庫裡排序和分頁。
*   **缺點**：修改車輛資料或規則後，容易留下過時的日期。
*   **否決原因**：作者否決：讀取時即時計算。

#### ALT-C4：出廠日期只存年月
*   **優點**：和行照上印的格式一致。
*   **缺點**：「剛好滿 5 年」沒有明確定義。
*   **否決原因**：作者否決：改存完整日期，行照只有年月時請使用者填該月 1 日。

#### ALT-C5：機車排氣量用牌照稅級距
*   **優點**：之後算稅可以直接用。
*   **缺點**：機車的牌照稅級距（例如 151–250、251–500、501–600cc）和驗車門檻（250cc）、黃紅牌分界（550cc）都對不上。
*   **否決原因**：作者否決：機車級距改依車種分級，只作紀錄；汽車仍照牌照稅級距。

#### ALT-C6：排氣量改成「動力 + 整數 cc」（審查者建議）
*   **優點**：資料和行照一致，之後換算稅級最有彈性。
*   **缺點**：使用者可能輸入錯誤的數字。
*   **否決原因**：作者否決：偏好下拉選單；代價是之後若稅級和門檻對不上，要另外收精確的 cc 數，級距修改時也要做資料遷移。

#### ALT-C7：排氣量改成選填
*   **優點**：少一些驗證、check constraint 和資料遷移的工作。
*   **缺點**：資料不完整。
*   **否決原因**：作者否決（設計審查時再次確認）：維持必填，接受驗證、check constraint、參考資料端點和日後資料遷移的成本。

#### ALT-C8：大型重機由燃油機車的排氣量級距推導（不設獨立車種）
*   **優點**：不會出現車種和級距互相矛盾的資料。
*   **缺點**：使用者在車種清單裡看不到「大型重型機車」。
*   **否決原因**：作者否決：設為獨立車種，車種和級距互相檢查（`BRACKET_TYPE_MISMATCH`）。

#### ALT-C9：拍行照用 OCR 辨識
*   **優點**：資料輸入最省事。
*   **缺點**：範圍太大，而且要多存行照照片這類個資。
*   **否決原因**：作者否決（接受審查建議）：需要影像辨識服務並保存行照照片，範圍和個資都大幅增加。

#### ALT-C10：推算期間不變色、不參與排序
*   **優點**：維持「只會提早、不會晚」的原則。
*   **缺點**：滿 10 年的車少了第二次檢驗期間的提示。
*   **否決原因**：作者否決：照樣變色和排序，文件註明這個原則不適用於推算期間。

#### ALT-C11：把指定檢驗日期不一致的紀錄存進資料庫（`rule_mismatches` 表）
*   **優點**：每筆不一致都能保留下來，轉成規則的回歸測試，讓規則越用越準。
*   **缺點**：多一張表和一個資料存放處；仍需要有人判斷是規則錯還是使用者打錯。
*   **否決原因**：作者否決（設計審查時再次確認）：維持只寫 log。log 會遺失的風險見 [Risks & Mitigations](#risks--mitigations)。

#### ALT-C12：滿 10 年的車必填指定檢驗日期，取代 + 6 個月推算
*   **優點**：不違反「只會提早、不會晚」的原則。
*   **缺點**：輸入門檻提高；行照一次只印一個指定檢驗日期，仍然不知道每年第二次的日期。
*   **否決原因**：作者否決：改以推算標示和免責聲明處理。

### D. 刪除、上限與同時編輯

#### ALT-D1：直接硬刪除
*   **優點**：範圍較小，個資刪除較乾淨。
*   **缺點**：使用者沒有反悔的機會。
*   **否決原因**：作者否決：採用垃圾桶。

#### ALT-D2：垃圾桶不自動清除
*   **優點**：不需要任何清除邏輯。
*   **缺點**：丟進垃圾桶的個資（車牌）永遠留著；垃圾桶算在 50 台上限內，使用者必須手動永久刪除才能新增。
*   **否決原因**：作者否決（設計審查後改變決定）：改成 30 天後自動清除。原本以為自動清除需要背景排程，審查者指出可以在新增、修改車輛時順便清除，不需要排程；不再使用的帳號由作者人工處理。

#### ALT-D3：車牌唯一性不檢查垃圾桶（部分唯一索引）
*   **優點**：可以用同一個車牌重新新增。
*   **缺點**：還原垃圾桶裡的車時可能撞車牌。
*   **否決原因**：作者否決：垃圾桶也算，重複時提示去垃圾桶還原。

#### ALT-D4：不設每人車輛數上限
*   **優點**：少一個規則和錯誤代碼。
*   **缺點**：列表在記憶體排序、不分頁，數量沒有限制。
*   **否決原因**：作者否決：每人最多 50 台（含垃圾桶）。

#### ALT-D5：不做同時編輯檢查（最後一次儲存為準）
*   **優點**：少一個版本號和錯誤代碼。
*   **缺點**：較早儲存的修改會被默默覆蓋。
*   **否決原因**：作者否決：用 `xmin` 檢查，不一致時回傳 `VEHICLE_MODIFIED`。

### E. 日期顯示

#### ALT-E1：民國／西元在每個日期欄位旁切換，顯示跟著當初輸入的曆法
*   **優點**：輸入時最有彈性。
*   **缺點**：每個日期都要多存曆法；算出的日期還要決定跟哪個欄位；列表會民國、西元混在一起。
*   **否決原因**：作者否決：改成個人設定，全站套用。

#### ALT-E2：日期用文字輸入（例如 `114/03/15`）
*   **優點**：輸入快。
*   **缺點**：要自己寫解析和錯誤處理，也會發生在民國模式打成西元年的錯誤。
*   **否決原因**：作者否決：改用年、月、日三個下拉選單。

### F. 安全與金鑰

#### ALT-F1：三把獨立金鑰放在 `.env`
*   **優點**：每把金鑰可以單獨更換。
*   **缺點**：每個環境要管理和備份三把金鑰。
*   **否決原因**：作者否決：改用一把 `MASTER_KEY`，以 HKDF 推導。

#### ALT-F2：Data Protection 金鑰不加密，直接存在 PostgreSQL
*   **優點**：最簡單。
*   **缺點**：拿到資料庫備份的人就能偽造登入 cookie。
*   **否決原因**：作者否決：第一版不備份，加密在本機擋不住拿到這台電腦的人；但作者選擇在第一版就把加密做好、測試好，正式部署（OQ-2）有備份時不必再做資料遷移。

#### ALT-F3：Data Protection 金鑰用 `.pfx` 憑證加密
*   **優點**：框架內建，不用自己寫加密。
*   **缺點**：要多管理一個憑證檔案。
*   **否決原因**：作者否決：改用從 `MASTER_KEY` 推導的 AES 金鑰。

#### ALT-F4：Email 和 Data Protection 金鑰的加密延到部署 VPS 時再做
*   **優點**：第一版少寫一些自己的加密程式。
*   **缺點**：之後有真實資料再補，需要資料遷移和重新測試。
*   **否決原因**：作者否決（設計審查時再次確認）：第一版就做。審查者建議簡化（只存 Email 雜湊、Data Protection 金鑰改用框架內建的檔案存放），作者仍維持現狀：第一版不備份，加密在本機擋不住拿到這台電腦的人；但作者選擇在第一版就把加密做好、測試好，正式部署（OQ-2）時不必再做資料遷移。相關風險（`MASTER_KEY` 和資料庫在同一台主機、`MASTER_KEY` 遺失）見 [Risks & Mitigations](#risks--mitigations)。

#### ALT-F5：自己寫 CSRF 檢查的 middleware
*   **優點**：完全掌握邏輯。
*   **缺點**：要自己寫和測試框架已經提供的功能。
*   **否決原因**：作者否決（接受審查建議）：改用框架內建的 `IAntiforgery`。

#### ALT-F6：第一版不加 gitleaks
*   **優點**：少一個工具要安裝。
*   **缺點**：`check-secrets.sh` 抓不到被直接寫進設定檔或程式的密鑰，而 repo 是公開的。
*   **否決原因**：作者否決：hook 和 CI 都加。gitleaks 的版本固定在 `.gitleaks-version`，並附下載檔的 SHA-256；本機 hook 和 CI 用同一版本，版本不同時 hook 失敗，避免兩邊的掃描結果不一致。作者在設計審查時決定保留（審查者建議只在 CI 固定版本）。

### G. 部署

#### ALT-G1：本機 `deploy.sh` 先執行 `git pull`
*   **優點**：自動取得最新的程式。
*   **缺點**：本機就是開發機，可能蓋掉進行中的修改。
*   **否決原因**：作者否決（接受審查建議）：本機就是開發機，`git pull` 可能蓋掉或合併到進行中的修改；部署腳本只建置目前工作目錄的程式。

#### ALT-G2：Caddy 反向代理（只在 VPS 啟用）
*   **優點**：自動申請 HTTPS 憑證。
*   **缺點**：VPS 實際是 k3s（內建 Traefik），而第一版只在本機部署。
*   **否決原因**：作者否決：第一版不需要反向代理。

#### ALT-G3：ASP.NET Core Razor Pages、SQLite
*   **優點**：單一專案、部署最簡單。
*   **缺點**：和需求指定的技術不同；而且 SQLite 不支援第一版用到的 `xmin` 版本號和 `SELECT ... FOR UPDATE`。
*   **否決原因**：作者否決：這是需求限制（作者指定 Vue + PostgreSQL），不是設計上的取捨。

### H. 後端架構（本文件撰寫時決定）

#### ALT-H1：三個專案（Core = Domain + Application、Infrastructure、Web）
*   **優點**：少一個專案，改功能時跳的專案較少。
*   **缺點**：Core 內部「實體不能依賴使用案例」的分界沒有編譯器強制。
*   **否決原因**：作者否決：採用四個專案，讓編譯器強制每一層的依賴方向。

#### ALT-H2：兩個專案（Domain、Web）+ 架構測試
*   **優點**：專案最少。
*   **缺點**：Web 專案內部的分層只靠架構測試保護。
*   **否決原因**：作者否決：架構測試只在測試執行時才會發現違規，而且規則要自己寫；拆成四個專案後，違反依賴方向的程式直接編譯失敗，不必依賴測試規則寫得完整。

#### ALT-H3：Application 透過 `IAppDbContext` 直接使用 EF Core 的 `DbSet`
*   **優點**：程式最少，是常見的 .NET 範本做法。
*   **缺點**：Application 專案必須參考 EF Core，編譯器擋不住 Application 使用 EF Core 的功能，和四個專案的目的矛盾。
*   **否決原因**：作者否決：寫入用 Repository，讀取用查詢服務，Application 不參考 EF Core。

#### ALT-H4：只用 Repository（讀寫都走 Repository）
*   **優點**：只有一種資料存取概念。
*   **缺點**：讀取時要載入完整的實體。
*   **否決原因**：作者否決：讀取改用查詢服務。

#### ALT-H5：使用案例寫成一般的服務類別
*   **優點**：最直覺，不需要額外的介面。
*   **缺點**：服務類別容易越來越大，違反單一職責；共同處理要在每個方法重複寫。
*   **否決原因**：作者否決：每個使用案例一個 handler。

#### ALT-H6：MediatR
*   **優點**：有現成的 pipeline，是 .NET Clean Architecture 範本常見的做法。
*   **缺點**：多一個外部套件；2025 年起改為商業授權（有免費社群版）。
*   **否決原因**：作者否決：自己定義 `ICommandHandler`、`IQueryHandler` 介面。

#### ALT-H7：每一層一個測試專案，或分成單元、整合、架構三個測試專案
*   **優點**：架構測試可以在 CI 上單獨設成必要檢查。
*   **缺點**：測試專案較多，CI 設定較複雜。
*   **否決原因**：作者否決：只用 `UnitTests`（含架構測試）和 `IntegrationTests` 兩個。

#### ALT-H8：Minimal API
*   **優點**：程式最少，一個端點對一個 handler。
*   **缺點**：端點的檔案組織要自己規劃；部分 filter 的寫法和範例較少。
*   **否決原因**：作者否決：採用 Controller。

### I. 延後的做法

#### ALT-I1：用 GitHub Actions 依分支自動部署（本機 self-hosted runner、VPS 用 SSH）
*   **優點**：push 後自動部署。
*   **缺點**：repo 是公開的，self-hosted runner 可能讓別人的 PR 在作者電腦上執行程式。
*   **否決原因**：延後（不是否決）：第一版手動部署。之後要加時的規則：公開 repo 不用 self-hosted runner，或只用 `push` 觸發；不用 `pull_request_target`；SSH 金鑰放在只限正式分支的 GitHub Environment；或改成主機自己從 GHCR 拉映像檔。

--------------------------------------------------------------------------------

## Non-Functional Requirements

*   **安全與隱私：** 登入名單每次請求重新檢查；Email 和 Data Protection 金鑰以 `MASTER_KEY` 推導的金鑰 AES-GCM 加密；CSRF 用 `IAntiforgery`；別人的資料一律回傳 404；log 不寫車牌，Email 只寫查詢雜湊前 8 碼；密鑰不進 git（`.gitignore` + `check-secrets.sh` + gitleaks，hook 和 CI 都跑）。
*   **效能：** 每人最多 50 台車，列表在記憶體排序、不分頁；名單比對在記憶體完成，不查資料庫。
*   **上線與回滾：** 第一版只在本機 Docker 執行，資料庫可以隨時清空重建（`docker compose down -v`），migration 可以自由修改。「migration 只增不改」從第一次部署到正式 VPS 開始適用。

--------------------------------------------------------------------------------

## Detailed Implementation Plan

### 0. 實作順序

每一步結束時，方案都要能建置，CI 都要通過。

1. **Repo 基礎設定：** 版本固定（`global.json`、`.nvmrc`、`.config/dotnet-tools.json`）、`.gitignore`、`.editorconfig`、`Directory.*.props`、密鑰檢查與 hook、`VehicleGo.slnx`（只含 `VehicleGo.Domain` 和 `VehicleGo.UnitTests`）、最小的 CI（`backend` 只建置和跑 `UnitTests`；`check-secrets`；`gitleaks`）。
2. **Domain：** 規則引擎和單元測試，先讓 G9-1 通過。
3. **Application：** 新增 `VehicleGo.Application` 專案，`UnitTests` 加參考；handler 和單元測試。
4. **Infrastructure：** 新增 `VehicleGo.Infrastructure` 專案，`UnitTests` 加參考；EF Core、加密、Data Protection、design-time factory、migration。
5. **Web：** 新增 `VehicleGo.Web` 和 `VehicleGo.IntegrationTests` 專案；登入、CSRF、Controllers、錯誤處理和整合測試；CI 的 `backend` 加跑 `IntegrationTests`。
6. **前端：** Vue 和 Vitest；CI 加 `frontend` job。
7. **部署：** `Dockerfile`、`docker-compose.yml`、`deploy.sh`、README。

### 1. 共用合約

#### 1.1 固定代碼

| 類別 | 代碼 |
| --- | --- |
| 車種 `VehicleType` | `CAR`（自用小客車，本文簡稱「汽車」）、`FUEL_MC`（一般燃油機車）、`HEAVY_MC`（大型重型機車）、`ELECTRIC_MC`（電動機車） |
| 汽車級距 | `CAR_LE_500`、`CAR_501_600`、`CAR_601_1200`、`CAR_1201_1800`、`CAR_1801_2400`、`CAR_2401_3000`、`CAR_3001_4200`、`CAR_4201_5400`、`CAR_5401_6600`、`CAR_6601_7800`、`CAR_GT_7800`、`CAR_ELECTRIC` |
| 機車級距 | `FUEL_MC`：`MC_LE_50`、`MC_51_250`；`HEAVY_MC`：`MC_251_550`、`MC_GT_550`；`ELECTRIC_MC`：無（null） |
| 檢驗種類 `kind` | `PERIODIC`（定期檢驗）、`EXHAUST`（排氣定檢） |
| 狀態 `status` | `EXEMPT`（免檢）、`UPCOMING`（下次檢驗期間）、`IN_WINDOW`（檢驗期間內）、`UNCOMPUTABLE`（無法計算）；畫面文字見 G5 |
| 日期來源 `basis` | `FORMULA`（公式推算）、`LICENCE`（行照指定檢驗日期） |
| 曆法 | `ROC`、`GREGORIAN` |
| 排序 `sort` | `deadline`（預設）、`manufactureDate` |

**三邊的表示方式（只有一份代碼對照）：**
*   **C#：** enum，每個成員用 `[JsonStringEnumMemberName("CAR_LE_500")]` 直接寫出代碼（.NET 9 起內建）。不用 `JsonNamingPolicy.SnakeCaseUpper`，因為它不會在數字前加底線（`CarLe500` 會變成 `CAR_LE500`）。
*   **JSON：** `new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)`，不接受數字。不認得的代碼值會讓 JSON 解析失敗，回 `VALIDATION_FAILED`（見 1.4 節）。
*   **資料庫：** 一個共用的 `ValueConverter<TEnum, string>`，讀同一個 attribute 雙向轉換，在 `ConfigureConventions` 統一套用到所有代碼 enum，存成 `HEAVY_MC` 這樣的字串。不用 `HasConversion<string>()`，因為它存的是 enum 名稱（`HeavyMc`），和 check constraint 不符。

#### 1.2 規則 ID 與規則表

| 車種 | 種類 | 規則 ID | 內容 |
| --- | --- | --- | --- |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE0TO5` | 今天距出廠未滿 5 年：顯示第一次定期檢驗的檢驗期間。這個檢驗期間可能在滿 5 年前一個月就開始，這時狀態為 `IN_WINDOW` |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE5TO10` | 今天滿 5 年、未滿 10 年：每年一次，檢驗期間為指定檢驗日期前後一個月（以日計） |
| `CAR` | `PERIODIC` | `RTS44-CAR-AGE10UP` | 今天滿 10 年：每年兩次，其中指定檢驗日期 + 6 個月的那一次為推算期間（`estimated: true`） |
| `CAR` | `PERIODIC` | `RTS44-CAR-LICENCE` | 有填指定檢驗日期時使用（選規則 ID 時不看車齡）；推算期間見 1.3 節 |
| `HEAVY_MC` | `PERIODIC` | `RTS44-HMC-AGE0TO5`、`RTS44-HMC-AGE5TO10`、`RTS44-HMC-AGE10UP`、`RTS44-HMC-LICENCE` | 同汽車 |
| `FUEL_MC`、`HEAVY_MC` | `EXHAUST` | `APC40-MC-AGE0TO5`、`APC40-MC-ANNUAL` | 今天距出廠未滿 5 年用 `AGE0TO5`，顯示第一個檢驗期間；滿 5 年起用 `ANNUAL`，每年一次，原發照月份前一個月 1 日到後一個月最後一天（以月計） |
| `ELECTRIC_MC` | `EXHAUST` | `APC40-EMC-EXEMPT` | 免檢 |

**法規出處與規則版本日期：**

| 法規 | 條號／範圍 | 規則版本日期 | 對應規則 ID／代碼 |
| --- | --- | --- | --- |
| 道路交通安全規則 | 第 44 條 | 2026-01-01（民國 115/01/01） | `RTS44-*` |
| 空氣污染防制法 | 第 40 條 | 2026-01-01（民國 115/01/01） | `APC40-*` |
| 使用牌照稅法 | 汽車排氣量級距 | 2026-01-01（民國 115/01/01） | 汽車級距（`CAR_*`） |

規則版本日期由作者指定為 2026-01-01，**不是條文實際的修正日期**；每條規則的 `EffectiveFrom` 和測試案例名稱都用這個日期。

**規則表的其他說明：**
*   **`RuleTable.Current()`：** 第一版回傳全部規則，不含過渡邏輯；日後法規修改時，新增規則並調整這個函式（OQ-1）。
*   **規則 ID 的選法（作者決定）：** 有填指定檢驗日期時用 `*-LICENCE`；否則依**今天**距出廠日期的車齡選 `AGE0TO5`／`AGE5TO10`／`AGE10UP`（排氣定檢為 `AGE0TO5`／`ANNUAL`）。
*   **狀態：** 只依「今天是否在顯示的檢驗期間內」判斷，和規則 ID 無關。
*   **已知限制：** 車齡接近 10 年時，顯示的檢驗期間可能已經是滿 10 年後的推算期間，但規則 ID 仍是 `AGE5TO10`；這只影響規則 ID 的標示，日期本身不受影響。
*   **縮寫：** `RTS44` = 道路交通安全規則第 44 條；`APC40` = 空氣污染防制法第 40 條。

#### 1.3 規則引擎演算法（虛擬碼）

```text
Calculate(vehicle, today, rules):                     # rules 由 IRuleTableProvider 提供
  kinds = { CAR: [PERIODIC], FUEL_MC: [EXHAUST], HEAVY_MC: [PERIODIC, EXHAUST], ELECTRIC_MC: [EXHAUST] }
  if InvalidInput(vehicle):                           # 影響整台車的所有檢驗
    for kind in kinds[vehicle.type]: yield Uncomputable(kind, INVALID_INPUT)
    return
  for kind in kinds[vehicle.type]:
    rule = rules.Find(vehicle.type, kind)
    if rule == null: yield Uncomputable(kind, NO_RULE); continue   # 只影響這一種檢驗

    if vehicle.type == ELECTRIC_MC:
      yield { kind, status: EXEMPT, earliestDate: null, deadline: null,
              ruleId: APC40-EMC-EXEMPT, basis: FORMULA, estimated: false }
      continue

    if kind == PERIODIC:
      if L != null:                                   # L = 指定檢驗日期
        basis = LICENCE
        anchors = L.AddYears(n), n = -1, 0, 1, 2, ...   # n = -1 只用來產生前一年的推算期間
      else:
        basis = FORMULA
        gapYears = GapYears(manufactureDate, issueDate)
        anchors = issueDate.AddYears(gapYears + n), n = 0, 1, 2, ...
      # 每個 anchor 都從起點直接加 N 年，不從前一個 anchor 累加
      windows = for each anchor A (第 n 個):
        if n >= 0:
          { earliestDate: A.AddMonths(-1), deadline: A.AddMonths(1), estimated: false }
        if Age(manufactureDate, A) >= 10:
          E = A.AddMonths(6)
          { earliestDate: E.AddMonths(-1), deadline: E.AddMonths(1), estimated: true }

    if kind == EXHAUST:
      basis = FORMULA
      M = issueDate.Month
      windows = for Y = manufactureDate.Year, Y + 1, ...:
        first = new DateOnly(Y, M, 1)
        { earliestDate: first.AddMonths(-1), deadline: LastDayOfMonth(first.AddMonths(1)), estimated: false }
      windows = windows.where(deadline >= manufactureDate.AddYears(5))

    w = windows 依 deadline 由早到晚產生，取第一個 deadline >= today 的檢驗期間（找到就停止）
    status = w.earliestDate <= today <= w.deadline ? IN_WINDOW : UPCOMING
    ruleId = basis == LICENCE ? rule.LicenceRuleId : rule.RuleIdFor(Age(manufactureDate, today))
    yield { kind, status, w.earliestDate, w.deadline, ruleId, basis, w.estimated }

  diagnostics（只給伺服器）:
    有填 L 時，判斷是否不一致（Technical Plan 第 3 節「不一致的判斷」），不一致 → mismatch = true、formulaDate

Uncomputable(kind, reason) = { kind, status: UNCOMPUTABLE, earliestDate: null, deadline: null,
                               ruleId: null, basis: FORMULA, estimated: false }，診斷記錄 reason
```

**輔助函式：**
*   **`Age(m, X)`：** 回傳滿幾年，也就是最大的 N 使 `X >= m.AddYears(N)`。
*   **`GapYears(m, issue)`：** 回傳要加的年數。找出最小的 N（1–4）使 `issue <= m.AddYears(N)`（「未逾 N 年」包含剛好 N 年），回傳 `6 − N`；都不符合時（逾 4 年）回傳 1。對照 Technical Plan 第 3 節的間隔年數表。
*   **`InvalidInput(vehicle)`：** 引擎只檢查「沒有這些資料就算不出來」的條件：原發照日期早於出廠日期；一般燃油機車或電動機車有指定檢驗日期；級距和車種不符。和「今天」有關的驗證（未來日期、`LICENCE_DATE_TOO_FAR`）不在引擎裡做；資料庫裡的舊資料就算不符合現行驗證，只要算得出來就照常顯示。
*   **規則表從參數傳入：** Application 透過 `IRuleTableProvider` 取得規則（預設回傳 `RuleTable.Current()`），單元測試和整合測試可以換成缺少某條規則的規則表。
*   **`UNCOMPUTABLE` 的範圍：** `NO_RULE` 以單一檢驗為單位，大型重機的一種檢驗缺規則時，另一種照常計算；`INVALID_INPUT` 影響整台車的所有檢驗。正常情況下輸入驗證會擋掉錯誤資料，所以 `UNCOMPUTABLE` 只會在規則表有漏洞，或資料庫裡有舊資料不符合現行驗證時出現。

**月份加減：** 定期檢驗用 `DateOnly.AddMonths`／`AddYears`，落在不存在的日期時取該月最後一天（例如 2/29 在非閏年變 2/28），規則見 Technical Plan 第 3 節。排氣定檢以月計，最後期限一律是後一個月的最後一天，不套用這個規則。

**依據的優先順序：** 檢驗期間怎麼算以本節的虛擬碼為準；規則 ID 和法規出處以 1.2 節的規則表為準。

#### 1.4 API 合約

**格式：**
*   日曆日期（出廠日期、原發照日期、指定檢驗日期、`earliestDate`、`deadline`）是西元 `YYYY-MM-DD`；時間點（`deletedAt`、`purgeAt`）是 ISO 8601 UTC，前端換算成台北時間的日期顯示。
*   錯誤一律是 ProblemDetails：`type`、`title`、`status`、`traceId`，擴充欄位 `code`；必要時加 `trashedVehicleId`、`activeCount`、`trashCount`。`VALIDATION_FAILED` 另附 `errors: { 欄位名稱: [錯誤訊息] }`，欄位名稱和請求的 JSON 欄位相同（camelCase）。
*   框架自動產生的 400（模型驗證失敗、JSON 解析失敗，例如日期格式錯誤或不認得的代碼）由 `InvalidModelStateResponseFactory` 統一轉成 `VALIDATION_FAILED`。Content-Type 不對時框架回 415，第一版不另外處理（SPA 一律送 `application/json`）。
*   請求資料裡的必填值型別（`version`、`vehicleType`、`manufactureDate`、`originalIssueDate`、`calendar`）一律宣告成 nullable 加 `[Required]`，沒帶時才會進入模型驗證錯誤，不會默默變成預設值。
*   下表的「400 驗證錯誤」= `VALIDATION_FAILED` 或 1.5 節的 Domain 驗證代碼。錯誤欄只依 HTTP 狀態碼排列，不代表檢查順序（CSRF 的 400 在登入檢查的 401 之後）；PUT 的檢查順序見表格下方。

| 方法與路徑 | 請求 | 成功 | 錯誤 |
| --- | --- | --- | --- |
| `GET /api/auth/login?returnUrl=` | — | 302 到 Google | `returnUrl` 不合法時改用 `/` |
| `POST /api/auth/logout` | — | 204 | 400 `CSRF_INVALID`、401 |
| `GET /api/me` | — | 200 `{ email, calendar }`，並發出 `XSRF-TOKEN` cookie | 401 |
| `PATCH /api/me/preferences` | `{ calendar }` | 200（同 `/api/me`） | 400 `VALIDATION_FAILED`（不合法的曆法值）、400 `CSRF_INVALID`、401 |
| `GET /api/vehicles?sort=` | — | 200 `[VehicleDto]` | 400 `VALIDATION_FAILED`（不合法的 `sort`）、401 |
| `GET /api/vehicles/{id}` | — | 200 `VehicleDto` | 401、404 `VEHICLE_NOT_FOUND`（包括垃圾桶裡的車） |
| `POST /api/vehicles` | `VehicleInput` | 201 `VehicleDto`（`Location` header） | 400 驗證錯誤、400 `CSRF_INVALID`、401、409 `PLATE_EXISTS_ACTIVE`、409 `PLATE_EXISTS_IN_TRASH`、409 `VEHICLE_LIMIT_REACHED` |
| `PUT /api/vehicles/{id}` | `UpdateVehicleRequest` | 200 `VehicleDto` | 400 驗證錯誤、400 `CSRF_INVALID`、401、404 `VEHICLE_NOT_FOUND`、409 `VEHICLE_IN_TRASH`、409 `VEHICLE_MODIFIED`、409 `PLATE_EXISTS_ACTIVE`、409 `PLATE_EXISTS_IN_TRASH` |
| `POST /api/vehicles/{id}/trash` | — | 204 | 400 `CSRF_INVALID`、401、404 `VEHICLE_NOT_FOUND`、409 `VEHICLE_IN_TRASH` |
| `POST /api/vehicles/{id}/restore` | — | 204 | 400 `CSRF_INVALID`、401、404 `VEHICLE_NOT_FOUND`、409 `VEHICLE_NOT_IN_TRASH` |
| `DELETE /api/vehicles/{id}` | — | 204 | 400 `CSRF_INVALID`、401、404 `VEHICLE_NOT_FOUND`、409 `VEHICLE_NOT_IN_TRASH` |
| `GET /api/trash` | — | 200 `[TrashItemDto]` | 401 |
| `GET /api/reference/displacement-brackets` | — | 200 `[{ code, label, vehicleTypes }]` | 401 |
| `GET /health/live`、`GET /health/ready` | — | 200 `{ status, gitSha }` | 503（ready：資料庫連不上） |

*   `VehicleInput`：`plate`、`brand`、`model`、`nickname?`、`color?`、`vehicleType`、`displacementBracket?`、`manufactureDate`、`originalIssueDate`、`licenceInspectionDate?`。
*   `UpdateVehicleRequest` = `VehicleInput` + `version`（`uint?` 加 `[Required]`，沒帶時進入模型驗證錯誤）。
*   `VehicleDto`：`id`、`version`、`VehicleInput` 的所有欄位、`inspections: [{ kind, status, earliestDate?, deadline?, ruleId?, estimated, basis }]`。`earliestDate` 就是期間起日。
    *   `EXEMPT`：日期為 null，`ruleId = APC40-EMC-EXEMPT`，`basis = FORMULA`、`estimated = false`。
    *   `UNCOMPUTABLE`：日期和 `ruleId` 都是 null，`basis = FORMULA`、`estimated = false`。
*   `TrashItemDto`：`id`、`plate`、`brand`、`model`、`nickname?`、`vehicleType`、`deletedAt`、`purgeAt`（`deletedAt` + 30 天，畫面顯示「X 後自動移除」）。

**修改車輛（PUT）的檢查順序：** 400 驗證（含 Domain 驗證）→ 404 不存在或不屬於目前使用者 → 409 `VEHICLE_IN_TRASH` → 409 `VEHICLE_MODIFIED`（取出車輛後先比對版本號）→ 409 車牌重複（見 2.3 節）。

#### 1.5 錯誤代碼

| 代碼 | HTTP | 情況 |
| --- | --- | --- |
| `VALIDATION_FAILED` | 400 | 必填欄位缺漏（包括 PUT 沒帶 `version`）、格式錯誤、不認得的代碼值、正規化後的車牌是空的（附 `errors`） |
| `ISSUE_BEFORE_MANUFACTURE` | 400 | 原發照日期早於出廠日期 |
| `DATE_IN_FUTURE` | 400 | 出廠日期或原發照日期是未來日期（台北時間） |
| `BRACKET_TYPE_MISMATCH` | 400 | 級距不屬於該車種；電動機車送了級距；或其他車種沒有送級距 |
| `LICENCE_DATE_NOT_ALLOWED` | 400 | 一般燃油機車或電動機車送了非 null 的指定檢驗日期 |
| `LICENCE_DATE_BEFORE_ISSUE` | 400 | 指定檢驗日期早於原發照日期 |
| `LICENCE_DATE_TOO_FAR` | 400 | 指定檢驗日期晚於（台北時間）今年 + 2 年的 12/31 |
| `CSRF_INVALID` | 400 | CSRF token 缺少或錯誤 |
| `VEHICLE_NOT_FOUND` | 404 | 車輛不存在、不屬於目前使用者，或已在垃圾桶滿 30 天；`GET /api/vehicles/{id}` 遇到垃圾桶裡的車也回這個代碼 |
| `PLATE_EXISTS_ACTIVE` | 409 | 車牌和使用中的車重複 |
| `PLATE_EXISTS_IN_TRASH` | 409 | 車牌和垃圾桶裡的車重複（附 `trashedVehicleId`） |
| `VEHICLE_LIMIT_REACHED` | 409 | 已達 50 台（附 `activeCount`、`trashCount`） |
| `VEHICLE_MODIFIED` | 409 | 版本號不一致 |
| `VEHICLE_IN_TRASH` | 409 | 要修改或要丟進垃圾桶的車已經在垃圾桶裡 |
| `VEHICLE_NOT_IN_TRASH` | 409 | 要還原或要永久刪除的車不在垃圾桶裡 |

*   `LICENCE_DATE_TOO_FAR` 不是法規規定，是防止輸入錯誤的合理範圍：指定檢驗日期最多約一年後，留一年緩衝。
*   **驗證順序（遇到第一個失敗就停止）：**
    1. 框架的模型驗證：必填欄位和格式（`VALIDATION_FAILED`，`errors` 列出所有欄位）
    2. 未來日期（`DATE_IN_FUTURE`）
    3. 原發照日期早於出廠日期（`ISSUE_BEFORE_MANUFACTURE`）
    4. 級距和車種（`BRACKET_TYPE_MISMATCH`）
    5. 指定檢驗日期：`LICENCE_DATE_NOT_ALLOWED` → `LICENCE_DATE_BEFORE_ISSUE` → `LICENCE_DATE_TOO_FAR`

    第 1 項由框架處理；第 2–5 項是 Domain 驗證，只回傳第一個錯誤。

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
| `plate` | `text` | NOT NULL，使用者輸入的原始車牌，長度 1–16 |
| `normalized_plate` | `text` | NOT NULL，至少 1 個字元；`(owner_id, normalized_plate)` 唯一（`ux_vehicles_owner_plate`） |
| `brand`、`model` | `text` | NOT NULL，長度 1–50 |
| `nickname`、`color` | `text` | 可 null，長度 ≤ 50 |
| `vehicle_type` | `text` | NOT NULL，check（`ck_vehicles_type`） |
| `displacement_bracket` | `text` | 可 null；check：只有 `ELECTRIC_MC` 為 null，且代碼屬於該車種（`ck_vehicles_bracket`） |
| `manufacture_date`、`original_issue_date` | `date` | NOT NULL；check `original_issue_date >= manufacture_date`（`ck_vehicles_dates`） |
| `licence_inspection_date` | `date` | 可 null；check：只有 `CAR`、`HEAVY_MC` 可以有值（`ck_vehicles_licence_date`） |
| `deleted_at` | `timestamptz` | 可 null；`(owner_id, deleted_at)` 索引（`ix_vehicles_owner_deleted`），供 30 天清除使用 |
| `created_at`、`updated_at` | `timestamptz` | NOT NULL |
| `xmin` | 系統欄位 | 對應實體的 `uint Version`，用於同時編輯檢查 |

`data_protection_keys`：框架定義的欄位（`id`、`friendly_name`、`xml`），放在獨立的 DbContext 和 migration。

**時間：** 所有時間戳記（`created_at`、`updated_at`、`deleted_at`）都由程式從 `TimeProvider` 取得後寫入，不用資料庫的 `now()`。「滿 30 天」的截止時間 = `TimeProvider.GetUtcNow()` − 30 天，`deleted_at <= 截止時間` 就算滿 30 天。

**migration 歷史表：** `AppDbContext` 用 `__ef_migrations_history`，`DataProtectionKeyContext` 用 `__ef_dp_migrations_history`。`EFCore.NamingConventions` 不會改這個名稱，要在 `UseNpgsql(..., o => o.MigrationsHistoryTable("__ef_migrations_history"))` 明確指定。

#### 1.7 命名慣例

| 種類 | 風格 | 例子 |
| --- | --- | --- |
| query 參數 | camelCase | `returnUrl`、`sort` |
| query 參數的值 | camelCase | `sort=deadline`、`sort=manufactureDate`（`sort` 綁成字串自己比對，不綁成 enum，避免接受數字） |
| JSON 欄位（請求、回應、ProblemDetails 擴充欄位） | camelCase | `licenceInspectionDate`、`trashedVehicleId`、`earliestDate` |
| URL 路徑 | 全小寫，單字之間用連字號（kebab-case） | `/api/reference/displacement-brackets`、`/api/me/preferences` |
| 代碼值（車種、級距、檢驗種類、狀態、日期來源、曆法、錯誤代碼） | 全大寫，單字之間用底線 | `HEAVY_MC`、`MC_51_250`、`IN_WINDOW`、`PLATE_EXISTS_IN_TRASH`、`ROC` |
| 規則 ID | 全大寫，用連字號分段 | `RTS44-CAR-AGE5TO10` |
| 資料表、欄位、索引、constraint | 全小寫，單字之間用底線（snake_case）；索引和 constraint 用 `pk_`、`fk_`、`ux_`、`ix_`、`ck_` 前綴 | `licence_inspection_date`、`ux_vehicles_owner_plate` |
| C# 型別、方法、屬性 | PascalCase | `InspectionCalculator`、`LicenceInspectionDate` |
| 環境變數 | 全大寫，單字之間用底線 | `ALLOWED_EMAILS`、`MASTER_KEY` |
| log 事件 | 全大寫，單字之間用底線 | `LICENCE_DATE_MISMATCH`、`INSPECTION_UNCOMPUTABLE` |
| 前端檔案 | Vue 元件 PascalCase；其他 TypeScript 檔案 camelCase | `VehicleListPage.vue`、`useCurrentUser.ts`、`calendar.ts` |

新增端點、欄位或代碼時照這張表命名。ASP.NET Core 預設把 JSON 屬性序列化成 camelCase。資料庫的 snake_case 名稱由 `EFCore.NamingConventions` 的 `UseSnakeCaseNamingConvention()` 產生，兩個 DbContext 都使用；實作時先確認這個套件有支援 EF Core 10 的穩定版，沒有的話改用 `ToTable`、`HasColumnName` 手動指定。

### 2. 檔案清單

repo 目前沒有程式碼，以下檔案都是新增。

#### 2.1 Repo 根目錄

| 檔案 | 內容與理由 |
| --- | --- |
| `.gitignore` | 第一個 commit 就包含 `.env`、`**/.env`、`bin/`、`obj/`、`node_modules/`、`web/dist/`；防止密鑰和建置產物進 git |
| `.editorconfig` | 統一縮排、換行字元和檔尾換行 |
| `global.json` | 固定 .NET 10 SDK 版本，Dockerfile 和 CI 用同一版本 |
| `.config/dotnet-tools.json` | 固定 `dotnet-ef` 版本；`dotnet tool restore` 安裝 |
| `Directory.Build.props` | 所有專案共用的建置設定（見下方） |
| `Directory.Packages.props` | 集中管理 NuGet 套件版本，包括 `EFCore.NamingConventions`、`Microsoft.EntityFrameworkCore.Design`、`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`、`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`、`xunit.v3`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk` |
| `VehicleGo.slnx` | 方案（.NET 10 SDK 預設格式）；依實作順序逐步加入 4 個 `src` 專案和 2 個 `tests` 專案 |
| `postgres-image.txt` | PostgreSQL 映像檔標籤（例如 `postgres:17.x`）；Testcontainers 直接讀，Compose 透過 `deploy.sh` 匯出的 `POSTGRES_IMAGE` 取得 |
| `.env.example` | 依下方環境變數表列出所有要手動填的變數和範例值（Email 一律用 `example.com`），附 `openssl rand -base64 32` 產生 `MASTER_KEY` 的說明 |
| `docker-compose.yml` | `db`、`api` 兩個服務（見下方） |
| `Dockerfile` | multi-stage：Node 建置 `web/` → .NET publish `VehicleGo.Web`（含 `wwwroot`）→ runtime `mcr.microsoft.com/dotnet/aspnet:10.0-noble`（Ubuntu，內含 tzdata 和 ICU）；runtime 階段加 `USER $APP_UID`，以非 root 的 `app` 使用者執行（映像檔預設仍是 root） |
| `deploy.sh` | 匯出 `GIT_SHA`（有未 commit 修改時加 `-dirty`）和 `POSTGRES_IMAGE=$(cat postgres-image.txt)`；`docker compose up -d --build`；不執行 `git pull` |
| `scripts/check-secrets.sh` | `.env`（含子資料夾）被追蹤就失敗；掃 `*.cs`、`*.ts`、`*.vue`、`*.md`、`*.yml` 中 RFC 2606 保留網域以外的 Email；`--staged` 參數只檢查暫存檔，供 hook 使用 |
| `.githooks/pre-commit` | 先跑 `check-secrets.sh --staged`，再跑 `gitleaks git --pre-commit --staged --redact`；gitleaks 沒安裝或版本和 `.gitleaks-version` 不同時失敗並提示安裝方式 |
| `.gitleaks.toml` | `[extend] useDefault = true`；用 `[[allowlists]]` 和 `paths` 排除 `tests/VehicleGo.UnitTests/Infrastructure/KeyDerivationTests.cs` 的假測試向量 |
| `.gitleaks-version`、`.gitleaks-version.sha256` | gitleaks 版本號（不含 `v`）和下載檔的 SHA-256 |
| `.gitleaksignore` | 初始為空檔；日後記錄已確認的誤判，或已撤銷並更換的外洩密鑰的 fingerprint |
| `.github/workflows/ci.yml` | 見下方 CI 說明 |
| `README.md` | 開發與部署說明（見下方） |

**`Directory.Build.props`：**
*   `Nullable=enable`、`TreatWarningsAsErrors=true`。
*   `<WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>`：套件弱點警告不擋建置，避免新的 CVE 公布後 CI 突然失敗。
*   `InvariantGlobalization=false`：NFKC 正規化需要。

**`docker-compose.yml`：**
*   只引用 `${...}` 變數；`image: ${POSTGRES_IMAGE:?請用 deploy.sh 執行}`；`build.args: GIT_SHA: ${GIT_SHA:-unknown}`。
*   用 `POSTGRES_*` 組成 `ConnectionStrings__Default`。
*   log 輪替；`api` 對外 port 8080。

**`README.md`：**
*   啟用 hook：`git config core.hooksPath .githooks`。
*   建立 `.env`（照 `.env.example`）。
*   所有 Compose 操作都透過 `deploy.sh`，清空重建（`docker compose down -v`）除外。
*   Google OAuth redirect URI 要註冊 `http://localhost:8080/signin-oidc` 和 `http://localhost:5173/signin-oidc`。
*   只能用 `localhost` 連線：用區網 IP 的 http 連線，OIDC 的 cookie 會被瀏覽器拒絕，無法登入。
*   產生 migration 的步驟（見 2.4 節）。

**環境變數：**

| 變數 | 用途 | 範例值 | 誰讀取 |
| --- | --- | --- | --- |
| `ALLOWED_EMAILS` | 登入名單；逗號分隔，每個 Email 去空白、轉小寫後比對 | `alice@example.com,bob@example.com` | `api` |
| `GOOGLE_CLIENT_ID`、`GOOGLE_CLIENT_SECRET` | Google OIDC | （Google Cloud Console 取得） | `api` |
| `POSTGRES_USER`、`POSTGRES_PASSWORD`、`POSTGRES_DB` | 資料庫帳號 | `vehiclego`、（隨機產生）、`vehiclego` | `db`；compose 用它們組成連線字串 |
| `ConnectionStrings__Default` | 資料庫連線字串 | 由 compose 組成，不寫在 `.env` | `api` |
| `MASTER_KEY` | 推導加密金鑰；Base64、32 bytes | `openssl rand -base64 32` 產生 | `api` |
| `GIT_SHA` | 健康檢查顯示的版本 | 由 `deploy.sh` 匯出 | `api` 映像檔建置（Docker build arg） |
| `POSTGRES_IMAGE` | PostgreSQL 映像檔標籤 | 由 `deploy.sh` 從 `postgres-image.txt` 匯出 | compose |

**CI（`.github/workflows/ci.yml`）：**
*   **觸發：** `pull_request` 和 push 任何分支；`permissions: contents: read`。
*   **`backend`：** `dotnet build VehicleGo.slnx`、`dotnet test` `UnitTests`；第 5 步起加 `IntegrationTests`。
*   **`frontend`：** `npm ci`、`typecheck`、`test:unit`、`build`（實作順序第 6 步起）。
*   **`check-secrets`：** 執行 `scripts/check-secrets.sh`。
*   **`gitleaks`：** `fetch-depth: 0`；依 `.gitleaks-version` 下載 release tarball，用 `.gitleaks-version.sha256` 執行 `sha256sum -c` 驗證後，執行 `gitleaks git --redact` 掃完整歷史。

#### 2.2 `src/VehicleGo.Domain/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Domain.csproj` | 不參考任何其他專案或框架套件 |
| `Common/ErrorCode.cs` | 1.5 節所有錯誤代碼 |
| `Common/Result.cs` | 成功或失敗（附錯誤代碼和欄位）的結果型別，取代拋出例外 |
| `Vehicles/VehicleType.cs`、`Vehicles/DisplacementBracket.cs` | 1.1 節的代碼（enum）；車種的中文標籤為「自用小客車（非營業用）」「一般燃油機車」「大型重型機車」「電動機車（40 馬力以下）」 |
| `Vehicles/DisplacementBrackets.cs` | 級距清單（代碼、中文標籤、適用車種、法規出處）和 Technical Plan 第 2 節的「車種 → 可用級距 → 必填」對照 |
| `Vehicles/PlateNormalizer.cs` | NFKC（`string.Normalize(NormalizationForm.FormKC)`）、轉大寫、去空白和連字號 |
| `Vehicles/Vehicle.cs` | 車輛實體；建立和修改時呼叫驗證；丟進垃圾桶、還原的狀態轉換 |
| `Vehicles/VehicleValidator.cs` | 1.5 節的檢查順序；需要「今天」作為參數 |
| `Vehicles/VehicleLimits.cs` | 上限常數 50 |
| `Users/User.cs`、`Users/CalendarPreference.cs` | 使用者實體和曆法代碼 |
| `Inspections/InspectionKind.cs`、`InspectionStatus.cs`、`InspectionBasis.cs` | 1.1 節的代碼 |
| `Inspections/InspectionItem.cs` | 一個檢驗項目（給 API 用的欄位） |
| `Inspections/InspectionDiagnostics.cs` | 只給伺服器用：`UNCOMPUTABLE` 原因（`NO_RULE`、`INVALID_INPUT`）、指定檢驗日期不一致旗標、公式日期 |
| `Inspections/InspectionCalculator.cs` | 1.3 節的純函式 |
| `Inspections/Rules/InspectionRule.cs`、`PeriodCalculation.cs`、`LegalCitation.cs`、`RuleTable.cs` | 1.2 節的規則表，算法用 enum 表示；每條規則有 `EffectiveFrom`（規則版本日期 2026-01-01）；`RuleTable.Current()` 回傳現行規則 |
| `Inspections/Rules/FirstInspectionDate.cs` | `GapYears` 和第一次指定檢驗日期（D-R2、Technical Plan 第 3 節的間隔年數表） |

#### 2.3 `src/VehicleGo.Application/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Application.csproj` | 只參考 Domain |
| `Abstractions/ICommandHandler.cs`、`IQueryHandler.cs` | handler 介面（D-T3） |
| `Abstractions/IUnitOfWork.cs` | `Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct)`：只負責開交易和 commit／rollback；回傳失敗時 rollback |
| `Abstractions/IRuleTableProvider.cs` | 取得規則表；預設實作回傳 `RuleTable.Current()`，測試可替換 |
| `Abstractions/IVehicleRepository.cs` | 見下方方法清單 |
| `Abstractions/IUserRepository.cs` | `Task<Guid> UpsertOnLoginAsync(sub, emailEncrypted, emailHash)`（回傳 `users.id`，寫進 cookie）、`Task<User?> GetAsync(id)`、`Task<CalendarPreference> GetCalendarPreferenceAsync(id)`（`/api/me` 用，D-T2 的例外）、`Task SaveAsync(user)` |
| `Abstractions/IVehicleQueries.cs` | 列出使用中的車、取單台、列出垃圾桶（都只限目前使用者；列出垃圾桶帶截止時間參數） |
| `Abstractions/ICurrentUser.cs` | 目前使用者 ID 和 cookie 裡的 Email（都從 cookie 的 claim 讀取，不查資料庫） |
| `Abstractions/IEmailProtector.cs` | 加密 Email、計算查詢雜湊 |
| `Abstractions/IAllowlistProvider.cs` | 取得名單 |
| `Common/TaipeiToday.cs` | 由 `TimeProvider` 取得台北時間的今天 |
| `Auth/LoginAdmission.cs` | 名單比對（去空白、轉小寫）+ `email_verified` |
| `Auth/EnsureUserCommand.cs`（含 handler） | 登入時建立或更新使用者；同一帳號第一次登入的多個請求同時到達時，只建立一筆使用者 |
| `Vehicles/CreateVehicleCommand.cs`、`UpdateVehicleCommand.cs`、`TrashVehicleCommand.cs`、`RestoreVehicleCommand.cs`、`DeleteVehicleCommand.cs`（各含 handler） | 寫入使用案例；新增和修改見下方流程 |
| `Vehicles/ListVehiclesQuery.cs`、`GetVehicleQuery.cs`、`ListTrashQuery.cs`（各含 handler） | 讀取使用案例；呼叫規則引擎；排序（G5） |
| `Vehicles/VehicleDto.cs`、`InspectionDto.cs`、`TrashItemDto.cs` | 回應資料 |
| `Me/GetMeQuery.cs`、`UpdatePreferencesCommand.cs`（各含 handler） | `/api/me`；讀取走 `IUserRepository`（D-T2 的例外） |
| `Reference/GetDisplacementBracketsQuery.cs`（含 handler） | 級距清單 |
| `DependencyInjection.cs` | 掃描組件，註冊所有 handler |

**`IVehicleRepository` 的方法（都只限目前使用者，都帶 `CancellationToken`）：**
*   `Task<Vehicle?> GetForOwnerAsync(Guid id, bool includeTrashed, DateTimeOffset cutoff)`：已在垃圾桶滿 30 天的車回傳 null。
*   `Task<Vehicle?> FindByNormalizedPlateAsync(string normalizedPlate, DateTimeOffset cutoff)`：包括垃圾桶裡未滿 30 天的車。
*   `Task LockOwnerAsync()`：在交易中鎖住目前使用者那一列。
*   `Task<(int Active, int Trash)> CountForOwnerAsync()`：包括垃圾桶。
*   `Task PurgeExpiredTrashAsync(DateTimeOffset cutoff)`：清除 `deleted_at <= cutoff` 的垃圾桶車輛。
*   `Task<Result> AddAsync(Vehicle vehicle)`、`Task<Result> SaveAsync(Vehicle vehicle, uint? expectedVersion)`（`null` 表示不檢查版本號）、`Task<Result> DeleteAsync(Vehicle vehicle)`。

**說明：**
*   查詢類的方法靠 `Owner` 過濾器限定目前使用者，不帶 `ownerId` 參數；`LockOwnerAsync`（原生 SQL）和 `PurgeExpiredTrashAsync`（`ExecuteDeleteAsync`）從 `ICurrentUser` 取得使用者 ID。
*   每個寫入方法自己呼叫 `SaveChangesAsync`，並把資料庫錯誤轉成 `Result`。
*   截止時間由 handler 用 `TimeProvider` 算出（1.6 節）。

**新增和修改車輛的流程：**
1. Domain 驗證（1.5 節的順序），失敗就回錯誤。這一步在交易之前。
2. 以下步驟 2–7 在 `IUnitOfWork.ExecuteInTransactionAsync` 裡執行。`LockOwnerAsync`：同一使用者的寫入排隊執行。
3. `PurgeExpiredTrashAsync`：先清除滿 30 天的垃圾桶車輛。
4. 修改時：`GetForOwnerAsync`（不存在 → `VEHICLE_NOT_FOUND`；在垃圾桶 → `VEHICLE_IN_TRASH`），再比對版本號（不一致 → `VEHICLE_MODIFIED`）。
5. `FindByNormalizedPlateAsync`：查到使用中的其他車 → `PLATE_EXISTS_ACTIVE`；查到垃圾桶裡的車 → `PLATE_EXISTS_IN_TRASH`（附 `trashedVehicleId`）。
6. 新增時：`CountForOwnerAsync` ≥ 50 → `VEHICLE_LIMIT_REACHED`。
7. `AddAsync` 或 `SaveAsync`。同時寫入時的後備：版本號不一致 → `VEHICLE_MODIFIED`；撞到唯一索引 → `PLATE_EXISTS_ACTIVE`。
8. 交易提交後，依規則引擎的診斷寫 log：`LICENCE_DATE_MISMATCH`（欄位：車輛 ID、車種、出廠日期、原發照日期、填入的指定檢驗日期、公式日期；不含車牌）；`INSPECTION_UNCOMPUTABLE`（欄位：車輛 ID、檢驗種類、原因）。

**丟進垃圾桶、還原、永久刪除的流程**（都不使用 `IUnitOfWork`，因為只改一台車，不影響車牌或上限）：
*   **丟進垃圾桶：** `GetForOwnerAsync`（不存在 → `VEHICLE_NOT_FOUND`；已在垃圾桶 → `VEHICLE_IN_TRASH`）→ `vehicle.Trash(現在時間)` → `SaveAsync(vehicle, null)`。
*   **還原：** `GetForOwnerAsync(includeTrashed: true)`（不存在或已滿 30 天 → `VEHICLE_NOT_FOUND`；不在垃圾桶 → `VEHICLE_NOT_IN_TRASH`）→ `vehicle.Restore()` → `SaveAsync(vehicle, null)`。車牌唯一性包含垃圾桶，所以還原不會撞到車牌。
*   **永久刪除：** 同還原的檢查 → `DeleteAsync(vehicle)`。

#### 2.4 `src/VehicleGo.Infrastructure/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Infrastructure.csproj` | 參考 Application、Domain；EF Core、Npgsql、`EFCore.NamingConventions`、`Microsoft.EntityFrameworkCore.Design`（`PrivateAssets="all"`）、`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |
| `Persistence/AppDbContext.cs` | `users`、`vehicles`；`UseSnakeCaseNamingConvention()`；具名查詢過濾器 `Owner`（擁有者）和 `Deleted`（已刪除）；注入 `ICurrentUser`，`Owner` 過濾器參照 DbContext 的實例屬性，沒有目前使用者時用 `Guid.Empty`（查不到任何車）；不使用 DbContext pooling |
| `Persistence/AppDbContextFactory.cs` | `IDesignTimeDbContextFactory<AppDbContext>`：產生 migration 時直接建立 DbContext，不走 Web 的啟動驗證；用固定的假連線字串（`Host=localhost;Database=design_time`，不會真的連線）和沒有使用者的假 `ICurrentUser` |
| `Persistence/Configurations/UserConfiguration.cs`、`VehicleConfiguration.cs` | 1.6 節的欄位、索引、check constraint 和名稱；代碼 enum 用 1.1 節的共用 `ValueConverter`；`Property(v => v.Version).IsRowVersion()`（對應 `xmin`） |
| `Persistence/UnitOfWork.cs` | 實作 `IUnitOfWork`：`Database.BeginTransactionAsync()`；不啟用 Npgsql 的 `EnableRetryOnFailure`（自己開的交易不能直接和自動重試一起用） |
| `Persistence/PostgresErrorMapper.cs` | 23505（`ux_vehicles_owner_plate`）只當同時寫入的後備，固定轉成 `PLATE_EXISTS_ACTIVE`；`DbUpdateConcurrencyException` 轉成 `VEHICLE_MODIFIED`；回傳 Domain 的 `ErrorCode` |
| `Persistence/Repositories/VehicleRepository.cs`、`UserRepository.cs` | 見下方實作說明 |
| `Persistence/RuleTableProvider.cs` | 實作 `IRuleTableProvider`，回傳 `RuleTable.Current()` |
| `Persistence/Queries/VehicleQueries.cs` | 實作查詢服務（`AsNoTracking`） |
| `Persistence/Migrations/` | `AppDbContext` 的 migration（產生的檔案） |
| `Crypto/MasterKey.cs` | 讀取並驗證 `MASTER_KEY`（Base64、32 bytes） |
| `Crypto/KeyDerivation.cs` | `HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, salt: [], info: UTF-8(標籤))` |
| `Crypto/AesGcmProtector.cs` | `new AesGcm(key, tagSizeInBytes: 16)`；格式：版本 byte `0x01` + nonce 12 bytes + tag 16 bytes + 密文；每次加密用 `RandomNumberGenerator.Fill` 產生新的 nonce；associated data 為用途字串（`email:v1`、`dpkey:v1`） |
| `Crypto/EmailProtector.cs` | 實作 `IEmailProtector` |
| `DataProtection/DataProtectionKeyContext.cs` | 獨立的 DbContext，實作 `IDataProtectionKeyContext`（`DbSet<DataProtectionKey> DataProtectionKeys`）；`UseSnakeCaseNamingConvention()` |
| `DataProtection/DataProtectionKeyContextFactory.cs` | `IDesignTimeDbContextFactory<DataProtectionKeyContext>`；用同樣的假連線字串 |
| `DataProtection/Migrations/` | 這個 DbContext 的 migration |
| `DataProtection/AesGcmXmlEncryptor.cs` | `Encrypt` 回傳 `new EncryptedXmlInfo(element, typeof(AesGcmXmlDecryptor))`；透過 `KeyManagementOptions.XmlEncryptor` 註冊 |
| `DataProtection/AesGcmXmlDecryptor.cs` | 提供 `AesGcmXmlDecryptor(IServiceProvider services)` 建構式（框架用它建立解密器，不是從 DI 注入），從 `services` 取得 `MasterKey` 和 `ILogger`；解密失敗寫 Error log |
| `DependencyInjection.cs` | 註冊 DbContext、Repository、查詢服務、`IUnitOfWork`、加密；Data Protection：`PersistKeysToDbContext<DataProtectionKeyContext>()`、`SetApplicationName("VehicleGo")` |

**Repository 實作說明：**
*   **鎖定：** `LockOwnerAsync` 用原生 SQL `SELECT 1 FROM users WHERE id = @ownerId FOR UPDATE`（EF Core 沒有 `FOR UPDATE` 的 LINQ 寫法）。
*   **計數和垃圾桶查詢：** 只關閉 `Deleted` 過濾器（`IgnoreQueryFilters(["Deleted"])`），不能關閉 `Owner` 過濾器。
*   **清除：** `PurgeExpiredTrashAsync` 用 `ExecuteDeleteAsync`，同樣要 `IgnoreQueryFilters(["Deleted"])`，否則刪不到垃圾桶裡的車。
*   **修改：** 先把車輛查出來（tracked），再設定 `Entry(v).Property(x => x.Version).OriginalValue = expectedVersion`；不要用 `Attach` 一個新物件。
*   **登入時建立或更新使用者：** 用原生 SQL `INSERT ... ON CONFLICT (google_sub) DO UPDATE ... RETURNING id AS "Value"`（EF Core 沒有 upsert），以 `Database.SqlQuery<Guid>(...).ToListAsync()` 取結果；不能用 `SingleAsync`，否則 EF 會包成子查詢，PostgreSQL 回語法錯誤。

**產生 migration：** 先安裝工具，再依 DbContext 各執行一次。

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <名稱> --project src/VehicleGo.Infrastructure --startup-project src/VehicleGo.Infrastructure --context AppDbContext --output-dir Persistence/Migrations
```

```bash
dotnet ef migrations add <名稱> --project src/VehicleGo.Infrastructure --startup-project src/VehicleGo.Infrastructure --context DataProtectionKeyContext --output-dir DataProtection/Migrations
```

#### 2.5 `src/VehicleGo.Web/`

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.Web.csproj` | 參考 Application、Infrastructure |
| `Program.cs` | 見下方說明 |
| `appsettings.json` | 不含任何密鑰；log 等級 |
| `Startup/StartupValidation.cs` | 啟動時檢查 `MASTER_KEY`、Asia/Taipei 時區、`ALLOWED_EMAILS`（空的時候警告並全部拒絕）；從 `IConfiguration` 讀取，不直接讀環境變數，整合測試才能換成記憶體中的值 |
| `Auth/AuthenticationSetup.cs` | 見下方說明 |
| `Auth/OidcSignInHandler.cs` | Google 登入回來時的處理：輸入 `sub`、`email`、`email_verified`，呼叫 `LoginAdmission` 和 `EnsureUserCommand`，輸出「通過，附使用者 ID」或「拒絕」。`OnTokenValidated` 和測試登入端點都呼叫這個類別 |
| `Auth/EnvironmentAllowlistProvider.cs` | 實作 `IAllowlistProvider`（讀 `ALLOWED_EMAILS`） |
| `Auth/HttpContextCurrentUser.cs` | 實作 `ICurrentUser`：從 cookie 的 claim 讀取使用者 ID 和 Email |
| `Auth/ReturnUrlValidator.cs` | `returnUrl` 只接受站內相對路徑，拒絕 `//`、`/\` 開頭，不合法時改用 `/` |
| `Security/AntiforgeryFilter.cs` | 全域的 `IAsyncAuthorizationFilter`，`Order` 排在登入檢查之後：GET 等不改資料的請求略過；其他請求呼叫 `IAntiforgery.ValidateRequestAsync`；失敗時回傳 `code = CSRF_INVALID` 的 ProblemDetails。header 名稱用 `services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN")` 設定 |
| `Errors/ErrorCodeMapping.cs` | 錯誤代碼 → HTTP 狀態碼與 ProblemDetails；`InvalidModelStateResponseFactory` 把框架自動產生的 400 轉成 `VALIDATION_FAILED`（附 `errors`）：去掉 JSON 路徑的 `$.` 前綴，丟掉以參數名稱當欄位名稱的那一筆 |
| `Contracts/VehicleInput.cs`、`UpdateVehicleRequest.cs`、`UpdatePreferencesRequest.cs` | 請求資料（1.4 節） |
| `Controllers/AuthController.cs`、`MeController.cs`、`VehiclesController.cs`、`TrashController.cs`、`ReferenceController.cs` | 1.4 節的端點；handler 用 `[FromServices]` 注入在 action 上，避免建構子變長；`MeController` 的 `GET /api/me` 用 `GetAndStoreTokens` 產生 token，寫進 `XSRF-TOKEN` cookie（不設 HttpOnly） |

**`Program.cs`：**
*   組裝相依性；`TryAddSingleton(TimeProvider.System)`；JSON console log；ProblemDetails；靜態檔與 SPA fallback（排除 `/api`、`/signin-oidc`）。
*   `AddControllers(o => o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))`：模型驗證錯誤改用 JSON 的欄位名稱（camelCase）。
*   FallbackPolicy 要求所有端點都要登入；`/api/auth/login` 和健康檢查加 `[AllowAnonymous]`。
*   啟動時套用兩個 DbContext 的 migration。
*   健康檢查：`AddDbContextCheck<AppDbContext>()`（`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`），加上 `ready` tag；`/health/live` 設 `Predicate = _ => false`（不檢查資料庫），`/health/ready` 只跑 `ready` tag 的檢查；自訂 `ResponseWriter` 輸出 `{ status, gitSha }`。
*   `--healthcheck` 參數：runtime 映像檔沒有 curl，Compose 的容器健康檢查改用 `dotnet VehicleGo.Web.dll --healthcheck`。這個參數在建立 `WebApplication` 之前處理：從 `ASPNETCORE_HTTP_PORTS`（預設 8080；用 `;` 列出多個時取第一個）取得 port，呼叫 `/health/ready`，成功回傳 0。

**`Auth/AuthenticationSetup.cs`：**
*   `DefaultScheme` 和 `DefaultChallengeScheme` 都是 Cookie，所以 `/api` 未登入時回 401，不會被轉到 Google；`/api/auth/login` 明確呼叫 `Challenge(OpenIdConnectDefaults.AuthenticationScheme)`。
*   OIDC：`Scope.Clear()` 後只加 `openid`、`email`；`ResponseType = Code`；`MapInboundClaims = false`；`SaveTokens = false`。
*   cookie 事件（`CookieAuthenticationEvents`）：`OnValidatePrincipal`（每次請求比對名單）、`OnRedirectToLogin`／`OnRedirectToAccessDenied`（`/api` 回 401／403）。
*   OIDC 事件（`OpenIdConnectEvents`）：`OnTokenValidated`（呼叫 `OidcSignInHandler`，拒絕時導向 `/not-allowed`）、`OnRemoteFailure`。
*   cookie 有效 14 天、sliding expiration；登入時寫入 `users.id`、`sub`、Email 三個 claim。
*   SPA 登入後要先呼叫 `/api/me` 取得新的 CSRF token，因為 token 綁定登入的使用者。

#### 2.6 `tests/VehicleGo.UnitTests/`（xUnit v3）

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.UnitTests.csproj` | 依實作順序參考 Domain、Application、Infrastructure；`xunit.v3`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk`（透過 VSTest 執行）、`TngTech.ArchUnitNET.xUnitV3`、`Microsoft.Extensions.TimeProvider.Testing` |
| `Domain/InspectionCalculatorCarTests.cs` | 汽車定期檢驗的案例（見表格下方） |
| `Domain/InspectionCalculatorMotorcycleTests.cs` | CUJ 5、6 的確切結果；燃油機車和大型重機的排氣定檢（月份計算、1 月和 12 月跨年、2 月和 11 月發照的最後期限、「出廠日期 + 5 年」落在檢驗期間內、最後期限剛好等於「出廠日期 + 5 年」）；大型重機兩種檢驗（有填指定檢驗日期時，排氣定檢仍是 `basis = FORMULA`）；電動機車免檢的欄位值 |
| `Domain/InspectionCalculatorLicenceDateTests.cs` | CUJ 4、4b、4c；指定檢驗日期為準、填過去的日期會往後推、未滿 5 年也用指定檢驗日期、滿 10 年時加推算期間（`basis = LICENCE`、`estimated = true`）、2/29 週年日在閏年仍是 2/29、不一致的判斷（含指定檢驗日期早於第一次公式日期） |
| `Domain/InspectionCalculatorUncomputableTests.cs` | 用缺少某條規則的規則表測 `NO_RULE`（大型重機缺排氣定檢規則時，定期檢驗照常計算）；`INVALID_INPUT` 讓整台車的檢驗都是 `UNCOMPUTABLE`；兩者的欄位值 |
| `Domain/FirstInspectionDateTests.cs` | 間隔年數表的每一列和邊界：相隔剛好 1 年（+5）、剛好 2 年（+4）、剛好 3 年（+3）、剛好 4 年（+2）、逾 4 年（+1）；出廠日期 2/29 |
| `Domain/RuleTableCoverageTests.cs` | 車種 × 檢驗種類 × 車齡區間，沒有重疊、沒有缺口；有填和沒填指定檢驗日期 |
| `Domain/PlateNormalizerTests.cs`、`VehicleValidatorTests.cs`、`DisplacementBracketsTests.cs` | 正規化（含正規化後為空）、輸入驗證和檢查順序、級距對照 |
| `Domain/CodeMappingTests.cs` | 每個代碼 enum 成員在 JSON 和資料庫兩邊都轉成 1.1 節的代碼，並能轉回來；JSON 不接受數字 |
| `Application/*HandlerTests.cs` | 每個 handler 一個測試檔，用 `Fakes/` 的假 Repository、查詢服務和 `IUnitOfWork`；新增和修改的流程順序；列表排序；名單比對 |
| `Infrastructure/KeyDerivationTests.cs` | 用固定的假測試向量（已排除在 gitleaks 之外）固定推導結果 |
| `Infrastructure/AesGcmProtectorTests.cs`、`DataProtectionEncryptorTests.cs` | 加解密往返、用錯金鑰失敗、用途字串不同時失敗、存的不是明文；解密器可以用 `(IServiceProvider)` 建構式建立 |
| `Architecture/LayerDependencyTests.cs` | ArchUnitNET：Domain 不依賴其他層和框架；Application 不依賴 Infrastructure、Web、EF Core、ASP.NET |
| `Fakes/` | 假的 Repository、查詢服務、`IUnitOfWork`、`ICurrentUser`、`IEmailProtector` |

**`InspectionCalculatorCarTests.cs` 的案例：**
*   CUJ 3、12 的確切檢驗期間。
*   未滿 5 年：含滿 5 年前一個月內已進入檢驗期間（狀態 `IN_WINDOW`、規則 `AGE0TO5`）。
*   剛好 5 年；10 年切換前後；`estimated`。
*   2/29 發照遇到非閏年。
*   3/31 − 1 個月 → 2/28（閏年 2/29）。
*   8/31 + 6 個月：先加 6 個月得 2/28，檢驗期間 1/28–3/28；閏年得 2/29，檢驗期間 1/29–3/29。

測試案例名稱附上條號和規則版本日期，日期格式是西元 `yyyyMMdd`，例如 `RTS44_20260101_汽車滿5年_每年一次`。規則測試的「今天」固定為 2026-11-19。

#### 2.7 `tests/VehicleGo.IntegrationTests/`（xUnit v3 + Testcontainers）

| 檔案 | 內容與理由 |
| --- | --- |
| `VehicleGo.IntegrationTests.csproj` | 參考 Web；`xunit.v3`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk`、Testcontainers.PostgreSql、`Microsoft.AspNetCore.Mvc.Testing` |
| `Infrastructure/PostgresFixture.cs` | 讀 `postgres-image.txt` 啟動 PostgreSQL，套用兩個 DbContext 的 migration |
| `Infrastructure/TestLoginEndpoint.cs` | 測試用的登入端點：用模擬的 `sub`、`email`、`email_verified` 呼叫 `OidcSignInHandler`，通過時發出真正的登入 cookie。由 `VehicleGoFactory` 透過 `ConfigureTestServices` 加上的 startup filter 對應路由（`app.Map("/test/login", ...)`），正式的 Web 專案完全不含這段程式 |
| `Infrastructure/VehicleGoFactory.cs` | `WebApplicationFactory<Program>`（.NET 10 自動產生 `public partial class Program`）；`IntegrationTest` 環境；註冊 `TestLoginEndpoint`；記憶體中產生的 `MASTER_KEY`；可替換的名單；`FakeTimeProvider`（只用來固定「今天」；cookie 的到期時間也跟著它）；可替換的 `IRuleTableProvider`；測試用的 logger provider，用來擷取 log |
| `Infrastructure/TestClient.cs` | 透過測試登入端點取得**真正的** cookie，自動帶 XSRF token。所有整合測試都走真正的 cookie 驗證和每次請求的名單檢查，不使用假的登入處理器（作者決定） |
| `AuthTests.cs` | `/api` 未登入回 401（不轉址）；不在名單上時被拒絕，`users` 表沒有新增資料；`email_verified = false` 時被拒絕；移出名單後下一個請求 401；`returnUrl` 拒絕外部網址、`//` 和 `/\` 開頭；登出要 CSRF；同一帳號第一次登入的多個請求同時到達時，只建立一筆使用者 |
| `CsrfTests.cs` | 沒帶 header 回 `CSRF_INVALID` ProblemDetails；未登入時先回 401 |
| `VehicleTests.cs` | 1.5 節每個錯誤代碼至少一個測試；車牌重複（使用中、垃圾桶，附 `trashedVehicleId`）；垃圾桶流程；少了 `manufactureDate` 回 `VALIDATION_FAILED`；不合法的曆法值回 `VALIDATION_FAILED`；30 天清除：直接把資料庫裡的 `deleted_at` 改成 `FakeTimeProvider` 現在時間的 31 天前，確認**成功的**新增或修改後它已被刪除（新增失敗時交易 rollback，清除也會一起復原）、其他查詢和操作時看不到、PUT 把車牌改成和它相同時成功 |
| `ConcurrencyTests.cs` | 舊版本回 `VEHICLE_MODIFIED`；舊版本號加重複車牌也回 `VEHICLE_MODIFIED`；沒帶版本回 `VALIDATION_FAILED`（`errors` 含 `version`） |
| `VehicleLimitTests.cs` | 第 51 台被擋下並附數量；有 49 台時同時送出兩筆新增，只有一筆成功、另一筆回 `VEHICLE_LIMIT_REACHED` |
| `IsolationTests.cs` | 使用者 A 對 B 的每個車輛和垃圾桶端點都回 404；`/api/vehicles`、`/api/trash` 不包含 B 的資料；A 可以登錄和 B 相同的車牌；`/api/me` 只讀寫自己 |
| `LoggingTests.cs` | `LICENCE_DATE_MISMATCH`、`INSPECTION_UNCOMPUTABLE` 只在新增或修改時寫，欄位不含車牌；`INSPECTION_UNCOMPUTABLE` 用替換過的規則表觸發 |
| `HealthTests.cs` | `/health/live`、`/health/ready` 回傳 `gitSha` |

#### 2.8 `web/`（Vue 3 + TypeScript + Vite + Vue Router + Element Plus）

| 檔案 | 內容與理由 |
| --- | --- |
| `package.json`、`package-lock.json` | scripts：`dev`、`build`、`typecheck`（`vue-tsc --noEmit`）、`test:unit`（Vitest）；lock 檔供 `npm ci` 使用 |
| `.nvmrc` | 固定 Node 版本，Dockerfile 和 CI 用同一版本 |
| `vite.config.ts` | 從 `vitest/config` import `defineConfig`；dev proxy：`/api`、`/signin-oidc`（不設 `changeOrigin`，否則從 5173 登入時 Google 的 redirect URI 會對不上）；`unplugin-vue-components` 加 `ElementPlusResolver`，只註冊用到的 Element Plus 元件，`dts: 'src/components.d.ts'`（`vue-tsc` 才看得到元件型別）；`ElMessage`、`ElMessageBox` 等函式 API 要另外 import 並載入樣式 |
| `tsconfig.json` | 只用一個檔案，`include` `src/**/*.ts`、`src/**/*.vue`（不要用 `"files": []` 加 references，否則 `vue-tsc --noEmit` 不會檢查任何檔案） |
| `index.html`、`src/main.ts`、`src/App.vue` | 專案骨架 |
| `src/router.ts` | 路由：`/login`、`/not-allowed`、`/vehicles`、`/vehicles/new`、`/vehicles/:id`、`/trash`、`/settings` |
| `src/api/client.ts` | `fetch` 包裝：帶 `X-XSRF-TOKEN`；401 導向登入頁；`CSRF_INVALID` 重新呼叫 `/api/me` 取得 token 後重試一次 |
| `src/api/types.ts` | 1.4 節的資料型別 |
| `src/composables/useCurrentUser.ts` | 模組層級的使用者與曆法狀態（不用 Pinia） |
| `src/dates/calendar.ts` | 見下方日期規格；日期當成年月日三個數字處理，不使用 JavaScript `Date` |
| `src/dates/calendar.spec.ts` | G9-4 的 Vitest 測試（見下方最少案例） |
| `src/components/DateSelect.vue` | 年、月、日三個下拉選單（Element Plus `el-select`） |
| `src/components/InspectionBadge.vue` | 依 `status`、`basis`、`estimated` 顯示 G5 的文字、顏色和標示（`estimated` 為 true 時一律顯示推算標示） |
| `src/components/Disclaimer.vue` | 免責聲明（文字見 The Problem 一節），列表上方和車輛詳細頁各顯示一次 |
| `src/pages/LoginPage.vue`、`NotAllowedPage.vue`、`VehicleListPage.vue`、`VehicleFormPage.vue`、`TrashPage.vue`、`SettingsPage.vue` | 各頁面；錯誤代碼對應繁體中文訊息。`VehicleFormPage`（`/vehicles/:id`）就是車輛詳細頁：同時顯示檢驗項目和免責聲明 |

**日期規格（`calendar.ts`、`DateSelect.vue`）：**
*   **今年：** 用 `Intl.DateTimeFormat` 加 `timeZone: 'Asia/Taipei'` 取得台北時間的今年。
*   **年份範圍：** 出廠日期、原發照日期：1950 年到今年；指定檢驗日期：1950 年到今年 + 2 年（配合 `LICENCE_DATE_TOO_FAR`）。
*   **日期不存在時：** 改了年或月之後，原本的日不存在時，自動改成該月最後一天（例如 3/31 改成 2 月變成 2/28 或 2/29）。
*   **顯示格式：** 月、日補零。民國 `115/11/19`（年不補零，例如 `99/03/05`）；西元 `2026/11/19`。
*   **`calendar.spec.ts` 最少案例：** 民國 1 年 = 西元 1912 年；民國 113 年 2 月有 29 天、民國 114 年 2 月有 28 天；民國和西元互相換算後回到原值；3/31 改成 2 月時變成月底；兩種顯示格式；三種日期欄位的年份範圍。

### Verification Plan

*   **建置：** `dotnet build VehicleGo.slnx`；`npm ci && npm run build`（在 `web/`）；`./deploy.sh`（含 `docker compose build`）。
*   **單元測試：** `dotnet test tests/VehicleGo.UnitTests`；`npm run test:unit`。
*   **整合測試：** `dotnet test tests/VehicleGo.IntegrationTests`（需要 Docker）。
*   **靜態檢查：** `TreatWarningsAsErrors`；`npm run typecheck`；`scripts/check-secrets.sh`；`gitleaks git`。
*   **測試覆蓋率：** 不設數字門檻；以成功標準列出的案例為準。規則引擎（Domain）的每條規則和每個邊界都要有案例。
*   **CUJ 的確切日期：** 由 G9-1 的單元測試比對（「今天」固定為 2026-11-19），不靠手動驗證。
*   **手動驗證：** 用 Chrome 或 Firefox 開 `http://localhost:8080`，只檢查畫面：狀態文字標籤和顏色；推算標示、「依行照指定檢驗日期」標示；免責聲明的位置；兩種排序；各錯誤代碼的中文訊息；登入、垃圾桶、曆法切換流程（CUJ 1、2、7–11）。

--------------------------------------------------------------------------------

*Drafted with EGM*

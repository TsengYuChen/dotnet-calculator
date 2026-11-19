# Goldfish Annotated Review Report

**Status:** resolved（2026-11-19）

**作者決定：** 22 項接受並已套用到設計文件（X-B1、X-B2、X-M1 選 (b)、X-M2、X-M3、X-M4 選擇在車種選單加標籤並在免責聲明列出不支援的車種、X-M5–X-M14、X-N1、X-N5、X-N12、X-N13、X-N15、X-N16、X-N17）；1 項略過（X-N4：不加法規連結）；7 項 DECLINE 維持不修改（X-N2、X-N3、X-N6–X-N11、X-N14）。

**Target:** `docs/designs/vehicle_management.md`（第 1–621 行，功能規格）
**來源：** `critic_report.md`（第 7 階段第 1 輪，egm-design、egm-structural、egm-style）

分類說明：**PROPOSE FIX** = 意見成立，提出修改；**CONTEST** = 意見不成立（附上審查者讀得到的證據）；**ESCALATE** = 需要作者提供文件以外的資訊；**DECLINE** = Nit，記錄但不修改。

本輪結果：PROPOSE FIX 26 項、CONTEST 0 項、ESCALATE 0 項、DECLINE 7 項。其中 **X-M1、X-M4、X-M7** 的修改方式含有作者的選擇，建議用「逐項說明（interactive breakdown）」確認。

## Prioritized Findings & AI Suggestions

### [Issue 1] X-B1 [`egm-design`, `egm-structural`] - Severity: Blocker
*   **Affected Scope:** 全文（風險內容散落在第 33–37、313–318、470、536、614–618 行）
*   **Discrepancy Description:** 缺少 feature-spec 標準要求的「Risks & Mitigations」一節；多項失敗情境（自寫加密出錯、`MASTER_KEY` 遺失、啟動時 migration 失敗、Google OIDC 故障、規則算錯、公開 repo 外洩）沒有列出。
*   **Rationale:** 讀者無法確認主要風險都已處理。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立，文件確實沒有集中的風險說明。
*   ***Proposed Edit:***
    *   Location: 在 `## Alternatives Considered` 之前新增 `## Risks & Mitigations`。
    *   Change: 一張表，欄位為「風險／類別／影響／緩解方式／狀態」，至少 12 列：規則算錯日期；第一次指定檢驗日期公式和真實行照不一致；每年第二次推算期間可能比實際晚；不支援的車種被登錄（見 Issue 7）；自寫 AES-GCM／HKDF 出錯；`MASTER_KEY` 遺失（第一版：清空重建）；資料庫備份和金鑰在同一台主機；`api` 啟動時 migration 失敗（第一版：清空重建後重新部署）；Google OIDC 故障或 redirect URI 設錯（無替代登入，接受）；公開 repo 外洩密鑰（`.gitignore` + `check-secrets.sh` + gitleaks）；`LICENCE_DATE_MISMATCH` log 在重建容器時遺失（接受）；法規修改沒有過渡邏輯（B1）。同時把 C11、F4 的已知限制改成指向這一節。

### [Issue 2] X-B2 [`egm-design`, `egm-structural`, `egm-style`] - Severity: Blocker
*   **Affected Scope:** C2、F2、B8、C9、A3b、H2；第 18、95、96、224、228、235、327、361、455、470、487、536 行的審查編號；第 339 行 `concepts_brief.md`
*   **Discrepancy Description:** 部分否決理由只有「作者否決」；審查編號和 `concepts_brief.md` 沒有連結。
*   **Rationale:** 標準要求每個否決寫出具體取捨，且不能只引用無法查證的出處。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:***
    *   C2：「作者否決：沒有管理頁時，資料庫裡的規則只是另一份要和程式同步的資料，第一版沒有任何程式需要從資料庫讀規則。」
    *   F2：「作者否決：單獨拿到資料庫備份的人就能偽造任何人的登入 cookie。」
    *   B8：「作者否決：只用 Google 登入、沒有角色，這些方案要多架一個服務或多一組資料表，帶來的功能第一版用不到。」
    *   C9：「作者否決：需要影像辨識服務並保存行照照片，範圍和個資都大幅增加。」
    *   A3b：「作者否決：系統沒有已驗車紀錄，大多數已準時驗車的使用者也會連續 6 個月看到這個提示；作者選擇讓免責聲明承擔這個提醒。」
    *   H2：寫出理由，不用「同上」。
    *   審查編號：從 Goals、Technical Plan、Open Questions 中移除（改寫成理由本身）；Alternatives 中保留時改寫成「（設計審查時再次確認）」並連結 `../../zoom_out_report.md` 等審查報告。
    *   第 339 行：`concepts_brief.md` 改成連結 `[concepts_brief.md](../../concepts_brief.md)`。

### [Issue 3] X-M1 [`egm-design`] - Severity: Major
*   **Affected Scope:** 第 14 行、G9（第 54–58 行）、A2、C11
*   **Discrepancy Description:** 成功標準無法驗證「規則算得對」：規則和測試預期值出自同一人的理解；A2 不拿真實行照比對；`docker compose up -d --build` 會重建容器，清掉 `LICENCE_DATE_MISMATCH` log。
*   **Rationale:** 核心假設沒有標準答案可以對照。
*   ***AI Suggestion:*** PROPOSE FIX（含作者選擇）
*   ***Justification:*** 意見成立；「每次部署都清掉 log」是先前沒注意到的事實。
*   ***Proposed Edit（預設採用選項 b，作者可改選 a）：***
    *   (a) 加一項成功標準 G9-5：「作者自己的車輛（至少 2 台）行照上的指定檢驗日期，和公式算出的一致，做成單元測試的固定資料。」
    *   (b) 第 14 行改成「目的是先驗證規則的實作符合作者對法規的理解（法規本身的正確性見 Risks & Mitigations）」，並在 Risks 一節列出「規則和法規不一致」與「重建容器會清掉 log」。

### [Issue 4] X-M2 [`egm-design`] - Severity: Major
*   **Affected Scope:** G4、CUJ 4、A2
*   **Discrepancy Description:** 功能規格沒有定義有填行照日期時，期間過了以後、滿 10 年後、填了過去日期時怎麼處理。
*   **Rationale:** 行照日期每次驗車後都會換新，存的值會變舊。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 規則已寫在詳細實作計畫的虛擬碼，但功能規格應能單獨讀懂。
*   ***Proposed Edit:***
    *   G4 加子項目：「有填指定檢驗日期 L 時：期間為 L、L + 1 年、L + 2 年……的前後一個月，顯示最後期限在今天或之後的最早一個；某個日期當天車齡滿 10 年時，再加一個 + 6 個月的推算期間。填過去的日期也可以，系統會自動往後推到下一個期間。」
    *   編輯頁欄位說明：「每次驗完車，請改成新行照上的指定檢驗日期。」
    *   新增 CUJ 4b：今天 2026-11-19，指定檢驗日期填 115/03/10（期間已過）→ 顯示 116/02/10–116/04/10。

### [Issue 5] X-M3 [`egm-design`; comprehension 重跑 #1] - Severity: Major
*   **Affected Scope:** 第 26–27、35、82、85、121、137 行
*   **Discrepancy Description:** 月底、閏日、滿 5 年落在排氣定檢期間內、出廠到發照剛好整數年等邊界沒有定義。
*   **Rationale:** 這些決定日期是否正確，也要符合「只會提早」的原則。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:*** 在 Technical Plan 第 2 節之後新增「日期計算規則」小節：
    *   月份加減的結果落在不存在的日期時，取該月最後一天（例如 3/31 − 1 個月 = 2/28 或 2/29；8/31 + 6 個月 = 2/28 或 2/29），結果一律較早，符合「只會提早」。
    *   2/29 發照、非閏年的週年日取 2/28。
    *   排氣定檢：最後期限在「出廠日 + 5 年」當天或之後的第一個期間就要驗（例如出廠 2019-03-10、3 月發照，2024 年 2/1–4/30 這個期間要驗）。
    *   每年第二次的推算期間：只對「當天車齡已滿 10 年」的指定日期產生。
    *   間隔年數表：未逾 1 年（含剛好 1 年）+5；逾 1 年、未逾 2 年（含剛好 2 年）+4；逾 2 年、未逾 3 年 +3；逾 3 年、未逾 4 年（含剛好 4 年）+2；逾 4 年 +1。
    *   G9-1 列出上述邊界案例名稱。

### [Issue 6] X-M5 [`egm-design`; comprehension 重跑 #10] - Severity: Major
*   **Affected Scope:** 第 18 行、A3、第 209 行、C5
*   **Discrepancy Description:** 收集排氣量的理由（之後算牌照稅）對機車不成立；C5 的門檻說明錯誤（501–600cc 不會跨過 250cc）。
*   **Rationale:** 收集資料要有說得通的用途。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立；機車級距的分界（250、550）其實是驗車門檻和黃牌／紅牌分界，不是稅級。
*   ***Proposed Edit:***
    *   第 18 行：「汽車級距照牌照稅級距，供之後計算牌照稅；機車級距依車種分級（一般燃油機車 50cc 以下／51–250cc；大型重型機車 251–550cc（黃牌）／551cc 以上（紅牌）），作為車輛紀錄。排氣量不是個資。」
    *   C5 理由改成：「機車的牌照稅級距（例如 151–250、251–500、501–600）和驗車門檻（250cc）、黃紅牌分界（550cc）都對不上；機車級距改依車種分級，只作紀錄。」
    *   在 Technical Plan 第 2 節 Domain 加一張「車種 → 可用級距 → 必填」對照表。

### [Issue 7] X-M4 [`egm-design`] - Severity: Major
*   **Affected Scope:** 第 45、69 行、CUJ 6、第 36 行
*   **Discrepancy Description:** 不支援的車種（電動大型重機、營業用車）可以被登錄成支援的車種，結果少算了檢驗。
*   **Rationale:** 違反「只會提早」原則，也沒寫進免責聲明。
*   ***AI Suggestion:*** PROPOSE FIX（含作者選擇）
*   ***Justification:*** 意見成立。但作者在腦力激盪時決定「電動大型重機不支援、也不在畫面註明」，這個決定目前沒寫進設計文件，審查者看不到。
*   ***Proposed Edit（預設：尊重作者的決定，只記錄風險）：***
    *   Non-Goals 第 69 行補上「畫面不另外註明（作者決定）」。
    *   Risks 一節列一列：「不支援的車種被登錄成支援的車種，系統少算檢驗——接受（第一版只有名單上的使用者）。」
    *   作者若改變主意，可另外在車種選單加上「電動機車（40 馬力以下）」「自用小客車（非營業用）」的標籤。

### [Issue 8] X-M6 [`egm-design`] - Severity: Major
*   **Affected Scope:** 第 274、296 行；第 6 節安全（第 313–318 行）
*   **Discrepancy Description:** 功能規格沒寫 `returnUrl` 的開放轉址防護（詳細實作計畫第 700、922 行已有）。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 防護已存在於後半部，功能規格的安全一節應寫明。
*   ***Proposed Edit:*** 第 6 節加一項：「`returnUrl` 只接受站內相對路徑，拒絕 `//`、`/\` 開頭，不合法時改導回 `/`，避免登入流程被用來導向外部網站。」

### [Issue 9] X-M7 [`egm-design`, `egm-style`; comprehension 重跑 #9] - Severity: Major
*   **Affected Scope:** G3、第 224 行、D2（第 485–487 行）
*   **Discrepancy Description:** 「30 天自動永久刪除」說法太強；不再登入的使用者資料會一直留著；cookie 有效期限沒寫；第 224 行把還原、永久刪除列為「讀取」；第 487 行和第 224 行的觸發時機不一致；PUT 可能撞到看不到的過期垃圾桶車輛。
*   ***AI Suggestion:*** PROPOSE FIX（含作者選擇）
*   ***Justification:*** 意見成立。
*   ***Proposed Edit（預設方案）：***
    *   清除時機改為：新增車輛、修改車輛（PUT）、登入時，清除該使用者的過期垃圾桶車輛；另外 `api` 啟動時清除**所有**使用者的過期垃圾桶車輛（每次部署都會重啟，所以不再登入的使用者資料，最晚在下次部署時被刪除）。
    *   G3 改成：「丟進垃圾桶滿 30 天後就看不到；實際刪除發生在該使用者下一次新增、修改車輛或登入時，或下一次系統重新啟動時。」
    *   cookie 有效期限：14 天，有使用就自動延長（ASP.NET Core 預設值）。
    *   第 224 行「讀取（……還原、永久刪除）」改成「查詢或操作（……）」；第 487 行改成和第 224 行一致。

### [Issue 10] X-M8 [`egm-structural`, `egm-style`, `egm-design`] - Severity: Major
*   **Affected Scope:** 第 75–100 行；第 37 行
*   **Discrepancy Description:** Open Questions 的 A、C 類都已決定，只有 B 類是延後事項；第 37 行的指引和內容不符。
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:***
    *   新增 `## Decisions`（已決定事項），放原本的 A（改編號為 `D-R1`–`D-R5`，法規規則）和 C（改編號為 `D-T1`–`D-T7`，技術選擇）；原 A4 刪除，內容併入延後事項。
    *   `## Open Questions` 只留原 B1、B2，改編號為 `OQ-1`、`OQ-2`。
    *   第 37 行改成：「第一次指定檢驗日期的公式（D-R2）只依網路資料整理，作者決定不以真實行照比對；每年第二次的期間是推算的。」

### [Issue 11] X-M9 [`egm-structural`] - Severity: Major
*   **Affected Scope:** Open Questions 和 Alternatives 編號重複；第 180、215、216、331 行等引用
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:*** 配合 Issue 10，已決定事項用 `D-R*`／`D-T*`，延後事項用 `OQ-*`；Alternatives 編號改成 `ALT-A1`、`ALT-B1`……；全文引用一併更新（例如「作者決定 C1」→「作者決定 D-T1」）。

### [Issue 12] X-M10 [`egm-style`; comprehension 重跑 #6] - Severity: Major
*   **Affected Scope:** 全文術語
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:*** 在 Overview 後加「術語」小節，並全文統一：
    *   **檢驗期間**（不用「驗車期間」或單獨的「期間」作為正式名稱）；
    *   **定期檢驗**（不用「定檢」）、**排氣定檢**；
    *   **指定檢驗日期**（第一次出現寫「行照上的指定檢驗日期」，不用「行照日期」）；
    *   **Data Protection 金鑰**（第一次出現說明：用來簽署和加密登入 cookie 的金鑰；不再用「登入金鑰」）；
    *   **查詢服務**（不用「查詢介面」）；
    *   **大型重型機車**（簡稱「大型重機」，不用「重機」）；
    *   **推算標示**：畫面文字固定為「推算日期，請以行照為準」，只在 G5 定義一次。

### [Issue 13] X-M11 [`egm-style`] - Severity: Major
*   **Affected Scope:** 第 20、36、43、47、286 行
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:***
    *   第 36 行：拿掉「原則上」，改成「除了滿 10 年後每年第二次的推算期間以外，系統的簡化做法只會讓顯示的期限比實際早：(1) 不處理假日順延；(2) 只知出廠年月時填該月 1 日。」
    *   第 47 行：「同一台車在兩個分頁或裝置被同時修改時，後儲存的一方會被擋下……」
    *   第 43 行：「作者從 `ALLOWED_EMAILS` 移除某人並重建 `api` 容器後，該使用者的下一個請求會收到 401。」
    *   第 20 行：「遵守 SOLID」改成「依賴方向由專案參考和架構測試強制」（作者要求的 SOLID 保留在後半部的設計說明，但不作為可驗證的目標）。
    *   第 286 行：「Email 有兩份：cookie 裡的 Email claim（名單比對、`/api/me` 顯示）；資料庫裡加密的 Email（只用來處理人工刪除請求）。」

### [Issue 14] X-M12 [`egm-style`] - Severity: Major
*   **Affected Scope:** 第 18、89、96 行
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:*** OQ-1（原 B1）改寫：「每條規則都記錄 `EffectiveFrom`（目前條文的修正日期），測試案例命名本來就需要這個日期，所以不算為之後的功能預先寫程式；`RuleTable.Current()` 只是取得全部規則的單一入口，不含過渡邏輯。」

### [Issue 15] X-M13 [`egm-style`] - Severity: Major
*   **Affected Scope:** C2（第 95 行）、A2（第 82 行）
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:*** C2 拆成「寫入／讀取／例外／實作」四個子項目；A2 改寫成「第一次指定檢驗日期 = 原發照日 +（依間隔年數表）年」並附 CUJ 3 的例子（配合 Issue 5 的表）。

### [Issue 16] X-M14 [comprehension 重跑 #7；`egm-design` N2] - Severity: Major
*   **Affected Scope:** 第 277–278 行、`ICurrentUser`、第 95 行對照第 217 行
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** 意見成立。
*   ***Proposed Edit:***
    *   登入資料流：「登入 cookie 裡放三個 claim：內部使用者 ID（`users.id`）、Google `sub`、Email。`ICurrentUser` 直接從 cookie 讀取使用者 ID，每次請求不查資料庫。」
    *   第 217 行：`/api/me` 從查詢服務清單移到 `IUserRepository`，和 C2 的例外一致。

### Nit（PROPOSE FIX）

| 編號 | 修改 |
| --- | --- |
| X-N1 | Alternatives 改成 C10 在 C11 之前；A3b 改成 A4、後面依序調整；G1（延後）移到新的「延後的做法」小組 |
| X-N4 | 道安規則第 44 條、空污法第 40 條、處罰條例第 17 條加上全國法規資料庫連結；內部引用改成錨點連結 |
| X-N5 | 架構圖補上 Infrastructure → PostgreSQL 箭頭；中文字寬對齊（或改用 Mermaid） |
| X-N12 | 在功能規格列出完整狀態代碼和觸發條件（`EXEMPT`、`UPCOMING`、`IN_WINDOW`、`UNCOMPUTABLE`）；新增 CUJ「未滿 5 年的汽車」 |
| X-N13 | 「Google 金鑰」→「Google Client Secret」；「每年至少一次」與「每年一次」統一為後者；「機械性選擇」→「不影響設計的命名選擇」；第 599 行刪除「授權條件可能再變」 |
| X-N15 | Alternatives 新增「滿 10 年的車必填指定檢驗日期，取代 + 6 個月推算」：優點是不違反「只會提早」；缺點是輸入門檻提高；作者否決，改以推算標示和免責聲明處理 |
| X-N16 | 依出廠日排序：舊的在前；同一天依車牌排序 |
| X-N17 | 車牌正規化統一放在 Domain（`PlateNormalizer`），修正 C4 和測試對照表 |

### Nit（DECLINE，記錄不修改）

| 編號 | 理由 |
| --- | --- |
| X-N2 | 技術計畫的粗體標籤改成 H4：不影響理解，維持 |
| X-N3 | 清單符號混用：不影響內容，定稿時統一格式即可 |
| X-N6 | C7 的技術選擇沒有替代方案：屬於低影響的預設選擇，已在 D-T7 說明 |
| X-N7 | Alternatives 裡「最簡單」等用語：都附有缺點或取捨，不影響判斷 |
| X-N8 | A4 只是指標：已在 Issue 10 的重整中處理 |
| X-N9、X-N10、X-N11、X-N14 | 用詞或位置的偏好；X-N11 的內容會移到 Risks 一節（Issue 1） |

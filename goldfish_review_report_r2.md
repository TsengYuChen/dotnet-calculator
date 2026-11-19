# Goldfish Annotated Review Report（第 7 階段第 2 輪）

**Target:** `docs/designs/vehicle_management.md`（第 1–711 行，功能規格）
**來源：** `critic_report_r2.md`（第 7 階段第 2 輪，egm-design、egm-structural、egm-style）

分類說明：**PROPOSE FIX** = 意見成立，提出修改；**CONTEST** = 意見不成立；**ESCALATE** = 需要作者提供文件以外的資訊；**DECLINE** = 記錄但不修改。

本輪結果：PROPOSE FIX 8 個 Major + 23 個 Nit、CONTEST 0、ESCALATE 0、DECLINE 1（N-23）。其中 **D-M1** 的修改方式含有作者的選擇。

## Major

### [Issue 1] D-M1 填了未來的指定檢驗日期會漏掉推算期間 — PROPOSE FIX（含作者選擇）
*   **Justification:** 意見成立。以 CUJ 3 的車驗算：L = 116/08/20 時只產生 L、L + 1 年……和各自的 + 6 個月，116/02/20 的推算期間不會出現。
*   **Proposed Edit（預設選項 a）：**
    *   (a) 有填 L 時，序列從 L − 1 年開始產生推算期間（不產生 L − 1 年本身的檢驗期間），也就是補上 L − 6 個月的推算期間（條件：L − 1 年當天車齡已滿 10 年）。修改 G4、第 3 節、虛擬碼；新增 CUJ 4c：CUJ 3 的車填 116/08/20 → 顯示 116/01/20–116/03/20（推算），`basis = LICENCE`、`estimated = true`、`ruleId = RTS44-CAR-LICENCE`；G9-1 和 `InspectionCalculatorLicenceDateTests` 加這個案例。
    *   (b) 不改計算，只在欄位說明和風險表註明「滿 10 年的車請填目前有效的指定檢驗日期，不要填下一次的」。缺點：使用者手上的新行照只有下一次的日期。

### [Issue 2] D-M2 啟動時清除和擁有者過濾器矛盾 — PROPOSE FIX
*   **Proposed Edit:** 第 271 行補：「唯一的例外是 `PurgeAllExpiredTrashAsync`：只在 `api` 啟動時呼叫，關閉兩個過濾器、只刪除 `deleted_at` 早於 cutoff 的車；架構測試確保 Controllers 和 handler 不會呼叫它。」Risks 表新增一列：「`api` 啟動時清除失敗或主機時鐘錯誤｜維運｜`api` 反覆重啟，或刪掉未滿 30 天的車｜清除失敗只寫 Error log、不阻止啟動；時鐘由主機 NTP 維持｜接受」。詳細實作計畫的 `Program.cs`、架構測試同步。

### [Issue 3] S-M1 ALT-G1 否決原因空白 — PROPOSE FIX
*   **Proposed Edit:** 「作者否決（接受審查建議）：本機就是開發機，`git pull` 可能蓋掉或合併到進行中的修改；部署腳本只建置目前工作目錄的程式。」

### [Issue 4] T-M1 免責聲明的「推算」 — PROPOSE FIX
*   **Proposed Edit:** 「檢驗期間依您登錄的資料計算，僅供參考，請以行照、監理站及環保單位的正式通知為準。期限遇假日可能順延；標示「推算日期」的期間可能比實際早或晚。本系統不會提醒已過期或已完成的檢驗。本系統不支援電動大型重型機車、營業用車及貨車。」

### [Issue 5] T-M2 「依行照指定檢驗日期」標示的條件 — PROPOSE FIX
*   **Proposed Edit:** 「使用者有填指定檢驗日期（`basis = LICENCE`），而且不是推算期間時，顯示「依行照指定檢驗日期」。」

### [Issue 6] T-M3 沒填時滿 10 年後的規則 — PROPOSE FIX
*   **Proposed Edit:** 「沒填時，用公式算出第一次指定檢驗日期（D-R2），之後每年同月同日一次；滿 10 年後的推算期間規則和有填時相同（見第 3 節）。」

### [Issue 7] T-M4 D-R5「沒有修改過」 — PROPOSE FIX
*   **Proposed Edit:** 「第一版只放現行條文；目前沒有已公布、尚未生效的修正，所以沒有過渡條款要處理（見 OQ-1）。」

### [Issue 8] T-M5 不一致的判斷規則 — PROPOSE FIX
*   **Proposed Edit:** 第 3 節新增「不一致的判斷」：填入的指定檢驗日期，月日和同年度公式算出的指定檢驗日期不同時判定不一致；某個指定檢驗日期當天車齡已滿 10 年時，和公式日期 + 6 個月的月日相同也算一致。CUJ 4 改成：「公式算出的指定檢驗日期是每年 7/15，和填入的 3/10 不同，依第 3 節判定不一致：……」

## Nit

| 編號 | 建議 | 修改 |
| --- | --- | --- |
| N-1 | PROPOSE FIX | 第 3 節寫明：期間起日也取較早的日期，可能讓使用者提早幾天前往；接受（監理站可以說明） |
| N-2 | PROPOSE FIX | 週年日一律從原發照日（或 L）直接加 N 年計算，不從前一年累加；閏年仍是 2/29 |
| N-3 | PROPOSE FIX | Risks 表加一列規模限制：每人上限 50 台、記憶體排序、不分頁，已緩解 |
| N-4 | PROPOSE FIX | G5：「依該車所有檢驗中最早的最後期限」 |
| N-5 | PROPOSE FIX | 第 135 行改成「CUJ 3、4、4b、5、6、12」（若採用 D-M1 (a) 再加 4c）；第 407 行同 |
| N-6 | PROPOSE FIX | 第 422 行補「設計審查後改變決定」的說明 |
| N-7 | PROPOSE FIX | 第 422 行改連 `critic_report.md`（第 1 輪，現在保留不覆蓋）和 `goldfish_review_report.md` |
| N-8 | PROPOSE FIX | ALT-G3 補：「需求限制：作者指定 Vue + PostgreSQL；另外 SQLite 不支援第一版用到的 `xmin`、`SELECT ... FOR UPDATE`」 |
| N-9 | PROPOSE FIX | D-R4 改指向 Technical Plan 第 2 節的對照表 |
| N-10 | PROPOSE FIX | OQ-2 補「觸發：作者決定部署到正式 VPS 時」 |
| N-11 | PROPOSE FIX | 虛擬碼變數 `D1` 改名 `firstDate` |
| N-12–N-18、N-20–N-22 | PROPOSE FIX | 依 `critic_report_r2.md` 的建議逐項改寫 |
| N-19、N-24 | PROPOSE FIX | 第 270 行、第 18 行拆成子項目 |
| N-23 | DECLINE | 作者在第 1 輪 X-M11 已決定「Clean Architecture，遵守 SOLID」和依賴方向強制兩者都保留 |

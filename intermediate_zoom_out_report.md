# 第 8 階段：Intermediate Zoom Out 報告

**Status:** resolved（2026-11-19）

**作者決定（逐項確認）：** Z8-M1 選 (a)：只在新增和修改車輛時清除，拿掉登入和啟動時的清除；Z8-M2 選 (b)：維持加密，改寫 ALT-F2、ALT-F4 的理由和風險表；Z8-N1、Z8-N2、Z8-N4 接受；Z8-N3 在 ALT-F6 補上保留的理由。1.2 節 `LICENCE` 規則那一列也補上 L − 6 個月的推算期間。已套用到設計文件。

*   **審查對象：** `docs/designs/vehicle_management.md`（全文 1078 行）
*   **審查者：** egm-zoom-out（zoom-out-standard），對照 `zoom_out_report.md`（第 5 階段）、`goldfish_review_report.md`、`goldfish_review_report_r2.md`
*   **日期：** 2026-11-19

## 總評

**沒有 Blocker；2 個 Major、4 個 Nit。**

和第 5 階段相比，文件從 945 行長到 1078 行，但增加的內容大多是「讓規格說清楚」，沒有新增功能：術語、Decisions／Open Questions 拆開、Risks 表（標準要求）、第 3 節日期計算規則、CUJ 4b／4c／12（回應第 5 階段 M6）。第 5 階段建議的幾項簡化（測試登入端點移出正式程式碼、拿掉 decorator、合併 `IUserQueries`、拿掉 `pg_dump` 和 migrate 服務、拿掉 `VERSION_REQUIRED`）都已經做了。

真正讓實作變複雜的只有一處：垃圾桶清除變成四個觸發點，加上「啟動時清除所有使用者」這條唯一繞過資料隔離的路徑（Z8-M1）。另外，部署時不再備份之後，保留自寫加密的否決理由和文件本身互相矛盾（Z8-M2）。

功能規格和詳細實作計畫在高層次上一致。

## Major

### Z8-M1：垃圾桶清除的觸發點太多，「啟動時清除所有使用者」不值得做
*   **位置：** G3、Technical Plan 第 2 節（垃圾桶自動清除、Repository 例外）、第 8 節、Risks 表；詳細實作計畫的 `IVehicleRepository`、`PurgeAllExpiredTrashCommand`、`Program.cs`、`LayerDependencyTests`、`VehicleTests`；ALT-D2
*   **問題：**
    *   啟動時的清除需要：新的 command、關閉擁有者過濾器的 `PurgeAllExpiredTrashAsync`（全系統唯一繞過資料隔離的路徑，而資料隔離是 G9-3）、ArchUnit 特例規則、一列風險、一組整合測試。
    *   它解決的是「不再登入的使用者，垃圾桶裡的車牌一直留著」。但這是本機、只有名單上的人、可以隨時清空的實驗專案；Non-Goals 已寫明刪除資料的請求由作者人工處理。
    *   所有查詢和操作都已經把滿 30 天的車視為不存在，使用者看到的結果不受影響。真正需要「實際刪除」的只有車牌唯一索引和 50 台計數，這兩者只在新增和 PUT 時用到，所以登入時清除也是多餘的。
*   **建議：** 只保留新增和 PUT 兩個觸發點；拿掉登入和啟動的清除，以及連帶的 command、方法、過濾器例外、ArchUnit 特例、風險列和測試。G3 改成「實際刪除發生在該使用者下一次新增或修改車輛時；不再使用的帳號，資料由作者人工處理」。若要保留，請在 ALT-D2 寫明本機實驗專案為什麼需要「最晚在下次部署時刪除」這個保證。

### Z8-M2：部署不再備份之後，保留自寫加密的否決理由和文件互相矛盾
*   **位置：** G7、Technical Plan 第 2、7、8 節；Risks 表（自寫 AES-GCM／HKDF、備份和金鑰在同一台主機）；ALT-F2、ALT-F4；詳細實作計畫的 `Crypto/`、`DataProtection/` 和對應測試
*   **改變了什麼（所以重提）：**
    *   第 5 階段時 `deploy.sh` 每次部署都會 `pg_dump`；之後採用 N4，第一版不在部署時備份。
    *   ALT-F2 的否決理由是「資料庫備份可能被複製到別處」，但第一版沒有任何備份。
    *   Risks 表自己承認：拿到這台電腦的人可以解密所有 Email、偽造任何人的登入 cookie，在唯一的部署目標上，加密的防護效果是零；同時還要為自寫加密另外承擔一列風險。
    *   代價仍然是整份設計最大的一塊：三把推導金鑰、版本化格式、第二個 DbContext 和 migration、自寫的 XML 加解密器、固定測試向量和 gitleaks 排除設定。
*   **建議（由作者決定）：**
    *   (a) 簡化：Data Protection 金鑰改用框架內建的檔案存放（Docker volume）；Email 只存 `email_lookup_hash`。HKDF 只剩一把 HMAC 金鑰，拿掉 AES-GCM、第二個 DbContext 和 XML 加解密器，等 OQ-2 時再加回來。
    *   (b) 維持現狀：ALT-F2、ALT-F4 的理由改成第一版實際存在的威脅，並改寫 Risks 表的「資料庫備份」那一列，讓文件前後一致。

## Nit

| 編號 | 位置 | 問題 | 建議 |
| --- | --- | --- | --- |
| Z8-N1 | G4、D-R2、第 3 節、1.3 虛擬碼、G9-1 | 同一組規則寫在四到六個地方，L − 1 年規則這次就要改六處 | G4 只留一句摘要並連到第 3 節；虛擬碼刪掉重複的間隔年數表，改成「見第 3 節」；G9-1 改成「涵蓋第 3 節的每個邊界和 CUJ 的確切結果」 |
| Z8-N2 | Technical Plan 第 2、4 節 | 又寫進實作細節：`[FromServices]`、`SELECT ... FOR UPDATE`、`INSERT ... ON CONFLICT`、方法名稱（第 5 階段 N1 仍未處理） | Technical Plan 只寫元件和資料流；SQL、屬性和方法名稱留在詳細實作計畫（那裡都已經有） |
| Z8-N3 | 2.1 節 `.githooks/pre-commit`、`.gitleaks-version.sha256` | gitleaks 版本和 SHA-256 檢查還在，文件裡找不到作者的決定 | 拿掉，或在 ALT-F6 補一行理由 |
| Z8-N4 | 1.3 虛擬碼 | 純函式的虛擬碼裡夾了畫面文字和排序位置（G5 已寫） | 刪掉這兩行註解，或只保留「`UNCOMPUTABLE` 只在規則表有漏洞或舊資料不符合現行驗證時出現」 |

另外的小落差：1.2 節 `LICENCE` 規則那一列沒提到 L − 1 年（不算錯，期間以虛擬碼為準，可以補半句）。

## 刻意不重提的項目（作者已決定）

排氣量級距必填（ALT-C7）、`rule_mismatches` 表（ALT-C11）、`RECENTLY_ENDED`（ALT-A4）、四個專案與 handler 介面與 ArchUnitNET（D-T1、D-T3、D-T7、ALT-H*）、50 台上限含垃圾桶（ALT-D4）、L − 1 年推算期間（第 7 階段第 2 輪 D-M1 (a)）、Clean Architecture 加 SOLID（N-23）。

## 主流程備註

*   **Z8-N3：** 作者在第 5 階段已經對 N5（拿掉 gitleaks 的 SHA-256 和版本比對）選擇「維持原決定」，只是這個決定沒有寫進設計文件。所以這一項的實際問題是「決定沒有記錄」，建議處理方式是在 ALT-F6 補一行理由，而不是拿掉。

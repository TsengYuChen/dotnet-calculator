# 第 5 階段：Initial Zoom Out 審查報告

*   **審查對象：** `docs/designs/vehicle_management.md`（全文，945 行）
*   **審查者：** egm-zoom-out（套用 zoom-out-standard）、egm-inventive-catalyst
*   **Goldfish 背景資料：** 無（repo 內沒有程式碼）
*   **日期：** 2026-11-19

## 總評

兩位審查者都沒有提出 Blocker。核心設計被認為合理：規則引擎是 Domain 層裡不依賴框架的純函式；檢驗期間在讀取時計算、不存資料庫；用 `xmin` 做同時編輯檢查；實作順序把規則引擎排在最前面。

主要的張力只有一個：文件說這個系統**只在本機執行、可能永遠不會正式部署，甚至只是實驗專案**，卻帶著一整套正式上線等級的安全機制和架構。大部分 Major 意見都來自這個落差。其中多項是作者先前明確做過的決定，審查者仍然提出，目的是讓成本再被看見一次。是否推翻由作者決定。

## Major

### M1. 排氣量級距是為了非目標（牌照稅）而收集的
*   **審查者：** zoom-out（inventive-catalyst 的 Finding 5 也部分相關）
*   **位置：** G2、Open Questions A3、Technical Plan 第 2 節、1.1／1.5／1.6 節、`/api/reference/displacement-brackets`、Alternatives C5–C7
*   **問題：** 第一版沒有任何驗車規則用到級距（大型重機已經是獨立車種）；汽車的 11 個級距是牌照稅級距，而稅金是非目標。這和第 18 行「第一版不為之後的功能寫任何程式」矛盾。目前的成本：15 個代碼、必填欄位、check constraint、`BRACKET_TYPE_MISMATCH`、一支參考資料端點（含 controller、query、handler）、法規出處，以及日後的資料遷移。
*   **建議：** 第一版拿掉級距；或改成選填、不驗證的文字或整數 cc 欄位，並拿掉參考資料端點。
*   **注意：** 推翻作者決定（Alternatives C5–C7「排氣量維持必填」）。

### M2. 只在本機、有登入名單的實驗專案，卻自己寫加密
*   **審查者：** zoom-out（M2）、inventive-catalyst（Finding 2）
*   **位置：** G7、資料模型 `users`、第 6 節、2.4 節的 `Crypto/*` 和 `DataProtection/*`、Alternatives F1–F4
*   **問題：** 設計自己寫 HKDF、版本化的 AES-GCM 格式、Data Protection 的 XML 加解密器，還多一個 DbContext 和 migration。自己寫的加密是整份設計風險最高的部分。它防的是「有人拿到資料庫備份」，但在單一開發機上，備份就放在 `.env` 和 `MASTER_KEY` 旁邊。另外，被加密的 Email 只用來處理人工刪除請求，而真正能識別個人的車牌卻是明文。`deploy.sh` 的 `pg_dump` 備份也要有 `.env` 的金鑰才能還原，但金鑰本身沒有備份。
*   **建議（兩位審查者的做法類似）：**
    *   **不存 Email 本身**，只存 `email_lookup_hash`（帶金鑰的 HMAC）。刪除請求時，用對方提供的 Email 算雜湊來查。這樣就不需要 AES-GCM 加密 Email。
    *   **Data Protection 金鑰在本機階段改用框架內建的檔案存放**（Docker volume），或乾脆不保存，重新啟動就讓所有人重新登入一次（名單上的人只要按一下 Google 登入）。這樣就不需要第二個 DbContext 和自己寫的 XML 加解密器。
    *   加密相關的工作延到 B2（決定部署 VPS）時再做。
    *   Alternatives F4 否決「延後」的理由是「之後要做資料遷移」，但 NFR 已經寫明本機資料庫可以隨時清空重建，這個理由不成立。
*   **注意：** 推翻作者決定（F1–F4「第一版就做加密」）。

### M3. 垃圾桶、還原、永久刪除對「驗證規則」的目標來說太重
*   **審查者：** zoom-out（M3）；inventive-catalyst（Finding 1、6）提出不同的解法
*   **位置：** G3、CUJ 7–8、垃圾桶相關的 4 個端點和約 5 個錯誤代碼、兩個具名查詢過濾器、`TrashController`、`TrashPage.vue`
*   **問題：** 這些都和「驗證規則算得對」無關。車輛上限 50 台又把垃圾桶算進去，導致使用者必須手動永久刪除才能新增。垃圾桶也讓資料隔離的範圍變大。
*   **建議（二選一，或都不採用）：**
    *   **zoom-out：** 改成「確認對話框 + 直接刪除」（Alternatives D1）。可以拿掉垃圾桶端點、錯誤代碼和頁面，車牌重複只剩一種 `PLATE_EXISTS`，查詢過濾器也只剩一個。如果保留垃圾桶，至少不要把垃圾桶算進 50 台上限。
    *   **inventive-catalyst：** 保留垃圾桶，但改成**「讀到時順便清除」**：在新增車輛、垃圾桶列表、登入這幾個寫入路徑上，先刪除自己 30 天前丟進垃圾桶的車。不需要背景排程，`deleted_at` 已經是 `timestamptz`，不必改 schema。另外，車牌和垃圾桶裡的車重複時，前端可以提供「還原並套用我剛填的資料」（還原 → 取得版本號 → PUT），伺服器端不用改。
*   **注意：** 推翻作者決定（D1「選擇垃圾桶」或 D2「不自動清空」）。

### M4. 請求路徑的層數和預留的擴充點
*   **審查者：** zoom-out
*   **位置：** Open Questions C1–C3、Technical Plan 第 1–2 節、2.3／2.4 節
*   **問題：** 每個請求都要經過 Controller → handler 介面 → handler → Repository 或查詢介面 → EF 實作 → DTO 轉換，再加上自訂的 `Result` 型別和錯誤代碼轉換。約 12 個端點就有 4 個正式專案、8 個 Application 介面、約 15 個 handler 類別。C3 的「需要共同處理時用 decorator 實作」並沒有任何具體需求，屬於預留的擴充點。使用者同時有 `IUserRepository` 和 `IUserQueries`，但只為了一個偏好欄位。
*   **建議（不推翻四個專案的決定）：** 刪掉 decorator 那句；`IUserQueries` 併入 `IUserRepository`；寫明第一版沒有 handler pipeline 或共同處理；把規則引擎當作關鍵路徑，不要讓基礎設施拖慢實驗。

### M5. 測試專用的登入端點放在正式的 Web 專案裡
*   **審查者：** zoom-out
*   **位置：** Technical Plan 第 2 節 Web、`Controllers/IntegrationTestAuthController.cs`、`TestClient.cs`
*   **問題：** 這屬於 zoom-out 標準中點名的「繞過正式驗證」反模式。安全性只靠執行時檢查一個環境名稱；`ASPNETCORE_ENVIRONMENT` 設錯就會暴露一個免驗證的登入端點，而 repo 是公開的。
*   **建議：** 保留「發真正 cookie」的目標，但把這個端點移到 `IntegrationTests` 專案，由 `WebApplicationFactory.ConfigureTestServices` 或測試專用的端點對應註冊。正式程式碼裡完全不存在這段程式，「其他環境沒有這個端點」的測試也就不需要了。

### M6. 核心功能的 CUJ 沒有寫出明確的預期結果
*   **審查者：** zoom-out
*   **位置：** CUJ 3、CUJ 5
*   **問題：** 第一版存在的理由就是「規則算得對」，但唯一一個滿 10 年的 CUJ 列了三種「如果……」的結果，沒說哪一種成立；CUJ 5 用「7 年前」，沒有固定「今天」。手動驗證要走過這些 CUJ，結果卻永遠不會失敗。
*   **建議：** 每個 CUJ 固定「今天」（例如 2026-11-19），寫出確切的預期結果：檢驗種類、起日、最後期限、狀態、日期來源、是否推算。這樣 CUJ 就變成驗收標準，也能直接拿來當單元測試的案例。

### M7. 行照日期不一致的訊號只寫進 log，最後會遺失
*   **審查者：** inventive-catalyst（Finding 3）
*   **位置：** A2、CUJ 4、Application 的不一致判斷、`LoggingTests`
*   **問題：** 第一版的目的是證明規則正確，但 A2 決定不拿真實行照比對，所以測試只是在檢查作者自己寫的預期值。唯一和現實比對的訊號是 `LICENCE_DATE_MISMATCH`，卻只寫進 console log，Docker 輪替 log 後就不見了，也沒有任何步驟把它變成修正或測試。
*   **建議：** 把不一致的輸入（車種、出廠日、原發照日、行照日期、公式日期、規則 ID，不含車牌、Email、擁有者）寫到持久的地方，例如一張小的 `rule_mismatches` 表，或格式固定、可用腳本收集的 log。README 加一個步驟：用腳本把這些紀錄轉成 xUnit 測試案例。
*   **代價：** 多一個資料存放處；要有人判斷是規則錯還是使用者打錯。

### M8. 期限一過，畫面就默默跳回「下次期間」
*   **審查者：** inventive-catalyst（Finding 4，產品安全建議）
*   **位置：** 規則引擎虛擬碼「choose earliest window whose deadline >= today」、Alternatives A3／A4
*   **問題：** 系統無法宣稱「已逾期」，但它其實知道一個事實：「上一個期間已經在 N 天前結束」。現在的做法是在最需要提醒的時候（逾期會被罰、6 個月後註銷牌照）什麼都不說。
*   **建議：** 期限結束後的一段時間內（例如 6 個月，對應註銷門檻），也回傳剛結束的期間，用中性的狀態（例如 `RECENTLY_ENDED`），文字「上次檢驗期間已於 X 結束；如已完成請忽略」，不變色；排序仍用下一個期間。
*   **注意：** 部分推翻作者決定（A3「不顯示逾期」、A4「只有進入期間才變色」）。

## Nit

*   **N1（zoom-out）技術計畫裡有實作細節。** 第 2、3 節提到 `[FromServices]`、`INSERT ... ON CONFLICT`、`xmin`、HKDF、哪個過濾器可以關閉；這些在詳細實作計畫已經寫了。建議技術計畫只寫元件和資料流。
*   **N2（zoom-out）ArchUnitNET 和專案參考重複。** 四個專案的依賴方向已經由編譯器強制，除非有人加了套件參考，而那在 code review 會被看到。建議拿掉架構測試，或只保留一個針對具體漏洞的檢查。
*   **N3（zoom-out、inventive-catalyst Finding 5）同時編輯檢查和 50 台上限。** 單人使用時，兩個分頁衝突的機率低；上限是為了合理化記憶體排序，但規則引擎每台車只要微秒等級，數千台也沒問題。「先計數再新增」不是原子操作，兩個分頁同時新增會變成 51 台。建議：拿掉 `VERSION_REQUIRED`（沒帶版本當作一般驗證錯誤）；上限拿掉、改成很高的防濫用上限（例如 500）且不算垃圾桶，或如果一定要精確，就在新增的交易裡對使用者那一列加鎖。
*   **N4（zoom-out）部署機制和「資料庫可隨時清空」矛盾。** 每次部署都 `pg_dump`、獨立的 `migrate` 服務和第二個 Dockerfile target。建議本機階段在 app 啟動時套用 migration、拿掉 `pg_dump`，備份和 migration bundle 移到 B2。
*   **N5（zoom-out）密鑰掃描的工具鏈太重。** `.gitleaks-version`、SHA-256 檢查碼、版本不同就讓 hook 失敗，再加上 `check-secrets.sh`。建議 CI 保留 gitleaks（固定版本），本機 hook 保留輕量檢查，拿掉 SHA-256 和版本比對。
*   **N6（inventive-catalyst Finding 7）移出名單要重建容器。** 可以把名單改成掛載的檔案，用 `IOptionsMonitor` 輪詢讀取。若採用 M2 的「重新啟動就登出所有人」，這項優先度更低。
*   **N7（inventive-catalyst Finding 8）規則沒有預留版本欄位。** 現在就給每條規則一個 `EffectiveFrom`（填目前的修正日期），用一行函式選出現行規則集，日後法規修改時不必同時改引擎介面和查詢方式。和「不預先寫程式」有些衝突，所以只加欄位和選擇函式，不寫過渡邏輯。
*   **N8（zoom-out）過時或指向文件外的文字：**
    *   第 239 行說前端選擇由作者稍後決定，但 C7 已經決定了，應刪除。
    *   第 817 行引用「第 14 節 2a」，本文件沒有這一節，應改指 Open Questions A2。
    *   第 905 行引用「brief 第 8 節」，是文件外的內容，應改成文件內就能理解的說法。
    *   Open Questions C 項的順序是 C1–C4、C6、C7、C5，應重新排序。
    *   1.2 節的規則表和 1.3 節的虛擬碼是同一組規則的兩種寫法，而且虛擬碼沒有說明規則 ID 怎麼產生。應指定一個為準，並寫明規則 ID 的產生方式。

## 理想結果（inventive-catalyst）

系統顯示正確的檢驗期間，而且會越用越準，同時幾乎不需要保存狀態、密鑰或額外維護：
*   **規則會自我修正：** 每個使用者輸入的行照日期，不是印證公式，就是變成新的回歸測試（M7）。
*   **個資會自己消失：** 垃圾桶不靠排程也會過期（M3），系統只保存 Email 的帶金鑰雜湊（M2）。
*   **登入狀態可以丟棄：** 本機階段用重新啟動來撤銷登入（M2）；持久化並加密的金鑰在 B2 時再加回來。
*   **（B2 之後）主動提醒：** 每個使用者一個唯讀、帶祕密 token 的 iCalendar（.ics）訂閱網址，讓行事曆 App 自動抓取檢驗期間。不需要背景排程、寄信或 LINE，避開了 A1 被否決的原因。代價是網址可能外洩，需要能更換；也和目前只用 cookie 的驗證方式不同；在只跑在 localhost 時沒有用。

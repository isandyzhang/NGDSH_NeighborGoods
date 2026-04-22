# NeighborGoods Frontend Style Guide

> 目的：提供 `NeighborGoods.Frontend` 可直接落地的視覺與元件規範，確保登入、商品列表、訊息功能在同一設計語言下開發。

## 1. 視覺方向

- 風格關鍵字：Warm Minimal、High Contrast Typography、Large White Space。
- 參考畫面語氣：暖米色大面積背景 + 深色粗體主標 + 簡潔導覽列。
- 體驗原則：
  - 重視內容掃描速度（清楚層級、高對比文字）
  - 減少裝飾元素（以留白與字級建立節奏）
  - 互動狀態清楚可辨（hover/focus/disabled）

## 2. 設計 Tokens（實作來源：`src/index.css`）

### 色彩

- `bg`: `#efe2d1`（整體背景）
- `surface`: `#f7f0e4`（卡片/區塊底）
- `surface-2`: `#f2e7d7`（次層容器）
- `border`: `#dfd1be`（邊框與分隔）
- `brand`: `#1f1b1c`（主按鈕/深色塊）
- `brand-strong`: `#121011`（主按鈕 hover）
- `brand-foreground`: `#fefaf5`（深色底文字）
- `text-main`: `#1f1b1c`（主文字）
- `text-subtle`: `#5f554c`（次要文字）
- `text-muted`: `#8f8275`（輔助資訊）
- `danger`: `#b91c1c`（錯誤）

### 字體

- `font-sans`: `"Inter", "Noto Sans TC", "Segoe UI", sans-serif`
- 中文優先可讀性，數字與標題維持簡潔現代感。

### 陰影

- `shadow-soft`: `0 10px 30px rgba(37, 25, 16, 0.1)`

## 3. 排版規範

### 容器與留白

- 主內容最大寬度：
  - 一般頁面：`max-w-6xl`
  - 訊息與登入內容：`max-w-4xl`
- 主要內距：`px-4 py-8`（手機/桌機同一基準）
- 區塊垂直間距：`gap-3 ~ gap-8`，避免過度緊貼。

### 字級層級

- Hero Title：`text-5xl font-semibold`
- 頁面標題：`text-4xl font-semibold`
- 區塊標題：`text-lg ~ text-2xl font-semibold`
- 正文：`text-sm ~ text-base`
- 補充資訊：`text-xs text-text-muted`

## 4. 元件規範

### Button

- 主按鈕：`bg-brand text-brand-foreground hover:bg-brand-strong`
- 次按鈕：`border border-border bg-surface text-text-main`
- 圓角：`rounded-xl`
- Disabled：`opacity-60 + cursor-not-allowed`

### Input

- 容器：`rounded-xl border border-border bg-surface`
- Focus：`focus:border-brand`
- Label 與欄位間距：`gap-2`
- 錯誤訊息：`text-danger text-xs`

### Card

- 統一容器樣式：`rounded-2xl border border-border bg-surface shadow-soft`
- 內容內距：`p-5`

### EmptyState

- 使用虛線邊框呈現空資料：`border-dashed border-border`
- 結構：標題 + 描述，避免多餘 CTA。

### TopNav

- 導覽列固定頂部（`sticky top-0`）
- 背景半透明：`bg-bg/90 + backdrop-blur`
- Active 連結：`bg-surface text-text-main`

## 5. 頁面模板

### Login

- 左側：品牌 + 大標 + 短說明（價值主張）
- 右側：登入表單卡片
- 桌機採雙欄，手機自動堆疊。

### ListingHome

- 頂部 Hero 標題
- Hero 副標文案固定為「社宅專屬二手交易平台」
- 篩選卡片改為按鈕式條件（展開型 + 直接切換型）
- 商品卡片網格（`md:2欄 / xl:3欄`）
- 頁尾分頁（上一頁 / 頁碼 / 下一頁）

### Messages

- Conversations：列表卡片，包含未讀數與最後訊息時間
- Chat：上方標題、中間滾動訊息區、底部輸入框 + 送出按鈕
- 訊息泡泡區分自己與對方（深底/淺底）

## 6. 互動與狀態準則

- Loading：
  - 列表頁使用 skeleton 卡片（`animate-pulse`）
  - 訊息頁顯示「載入中」明確文字
- Error：
  - 優先顯示 API 返回訊息
  - 後備文案保持短句（例如「讀取商品列表失敗」）
- Empty：
  - 用 EmptyState 呈現，避免白畫面
- Authentication：
  - 未登入存取訊息頁時，導回 `/login`
  - 401 先做 refresh，失敗後清除登入狀態並要求重登入

## 7. 開發使用準則

- 新頁面優先使用現有 `shared/ui` 元件，避免重複造輪子。
- 任何新增色彩需先補到 token，再決定是否擴展 utility。
- 若需新增大型元件（如 FilterBar、MessageComposer），先定義可重用 API，再實作樣式。

## 8. Hero 與動效規格（第二階段）

### Hero 文案

- 網站名稱保持：`NeighborGoods`
- Listing 首頁副標統一：`社宅專屬二手交易平台`

### 進場動畫（中等節奏）

- 動畫組合：`fade-in + rise-up`
- 位移：`translateY(20px -> 0)`
- 透明度：`0 -> 1`
- duration：`450ms`
- easing：`cubic-bezier(0.22, 1, 0.36, 1)`
- 延遲：
  - 品牌名稱：`0ms`
  - 主標：`80ms`
  - 副標：`140ms`
  - 篩選區：`220ms`

## 9. 篩選按鈕交互規格（第二階段）

### 查詢觸發原則

- 所有條件均為按鈕狀態驅動。
- 任一條件按下或取消後，都立即重新打 query。
- 「清除條件」重置全部 active 狀態後，立即重查。

### 條件按鈕類型

- 展開型（可多選）：`分類`、`品況`、`社宅`
  - 點擊主按鈕後展開選項群（chips）
  - 支援多選；任一選項變更即重查
- 直接切換型（單鍵開關）：`是否免費`、`愛心捐贈`、`以物易物`
  - On / Off 即時重查

### Query 介面建議

- 展開型條件：陣列或逗號串（例如 `categoryCodes=1,3,7`）
- 切換型條件：布林（例如 `isFree=true`）
- 清除條件：移除條件參數，回預設列表

## 10. 第二階段色彩 Token（柔和語意色）

### 快速條件按鈕

- `pill-free-default`: `#E7F4EA`
- `pill-free-active`: `#2F7D4E`
- `pill-charity-default`: `#FBECEA`
- `pill-charity-active`: `#B45B4D`
- `pill-trade-default`: `#ECEAF9`
- `pill-trade-active`: `#5E5AB5`

### 視覺原則

- 展開型主按鈕使用深色主題（`brand` 系）
- 選項 chip 使用淺底；active 切深底高對比
- 所有篩選按鈕保持 `shadow-soft`，hover 可加強陰影

## 11. 動效與互動細節

- 按鈕 hover：`150ms`（shadow + opacity）
- 按鈕 active：`90ms`（`scale(0.98)`）
- 展開區塊：`200ms`（height + opacity）
- 手機展開：bottom sheet `250ms`
- 重查時可用短 skeleton（`150~250ms`）減少畫面閃爍

## 12. 篩選區 RWD 規格（第二階段）

- Desktop：展開型 + 直接切換型同列或一列半顯示
- Tablet：上下兩列（展開型在上、切換型在下）
- Mobile：
  - 第一排：分類 / 品況 / 社宅
  - 第二排：免費 / 愛心 / 以物易物 / 清除條件
  - 展開內容採 bottom sheet，避免壓縮商品列表

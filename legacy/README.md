# Legacy 專案

此目錄存放 **已不再作為主要產品路徑** 的舊版程式（例如 `NeighborGoods.Web`），僅供歷史對照、考古或必要時本機建置。

## 注意事項

- **不要**與現行 `NeighborGoods.Api` 的資料庫 migration 策略混用；正式環境 schema 以 Api 所擁有之 EF migrations 為準。
- 未與 Azure Functions 背景排程連動分析；舊站相關之 `NotificationQueueBackgroundService` 等視為已下線封存。

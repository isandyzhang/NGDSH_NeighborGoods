# Bicep 基礎範本（單一生產部署）

本資料夾目前只保留單一生產部署入口 `deploy.bicep`，適用於：

- 單一 Azure 生產資源群組
- Container App 後端 + Static Web Apps 前端混合部署
- 以低成本部署為優先
- 既有 SQL / Storage 由 IaC 接管屬性（不重建）

---

## 目前使用的檔案

- `deploy.bicep`
  - 唯一實際部署入口
- `deploy.parameters.prod.json`
  - 正式環境參數檔（committed，敏感欄位皆為 `__SET_BY_GITHUB_SECRETS__` 占位符）
- `deploy.parameters.example.json`
  - 範例參數檔（提供開發者參考）
- `deploy.parameters.json`
  - 本地開發/個人試跑用（已被 `.gitignore` 排除，不進 git/CI）
- `modules/`
  - `sql-manage-existing.bicep` / `storage-manage-existing.bicep`：prod 接管既有資源用
  - 其他通用模組：SignalR / Container App / Static Web App / Email / Logging
- `modules/legacy/`
  - `sql.bicep` / `storage.bicep`：保留作為「災備重建」或「未來開新環境」的 fallback；prod 不啟用

---

## 部署策略（prod）

`deploy.parameters.prod.json` 的預設策略是：

- `provisionSqlResources=false`、`provisionStorageResources=false` — 不建立新 SQL/Storage
- `manageExistingSql=true`、`manageExistingStorage=true` — 由 IaC 接管既有資源屬性
- SQL 目前預設為 `DTU Basic 5`（`databaseSkuName=Basic`、`databaseTier=Basic`、`databaseCapacity=5`），先求穩定與低成本，後續可再切回 vCore
- 透過 `existingSqlServerName` / `existingSqlDatabaseName` / `existingStorageAccountName` 指定既有資源名稱
- 連線字串由 GitHub Actions Secrets 在 deploy 時注入：
  - `AZURE_SQL_CONNECTION_STRING` → `existingSqlConnectionString`
  - `AZURE_BLOB_CONNECTION_STRING` → `existingBlobConnectionString`
  - 其餘敏感金鑰同樣以 `--parameters key=value` 形式注入

> 之所以保留 `provisionSqlResources` / `provisionStorageResources` 兩個旗標與 `modules/legacy/`，是為了未來「開新環境」或「既有資源損毀重建」時可以無痛切換。

---

## 部署流程

CI 自動部署：

- `pull_request` / `push main`：自動跑 `validate` + `what-if`（不會實際變更）
- `workflow_dispatch`：人工觸發後才跑 `deploy`
- 觸發點與 secret 名稱見 `.github/workflows/infra_bicep.yml`

本地手動驗證（可選）：

```bash
az deployment group what-if \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/deploy.bicep \
  --parameters @infra/bicep/deploy.parameters.prod.json \
  --parameters existingSqlConnectionString="<...>" \
  --parameters existingBlobConnectionString="<...>" \
  --parameters sqlAdminLogin="<...>" \
  --parameters sqlAdminPassword="<...>" \
  ... # 其餘 secrets
```

正式部署原則上由 GitHub Actions 觸發，本地僅作為例外 fallback。

---

## 注意事項

- 第一次接管時，請務必看 `what-if` 的輸出，確認 `Microsoft.Sql/servers/databases` 與 `Microsoft.Storage/storageAccounts/blobServices/containers` 顯示為 `Modify` 而非 `Create`。
  - 若是 `Create` → 既有資源名稱對不上 → 停止部署並修正 `existingSqlServerName` 等參數。
- `modules/legacy/` 不再由 prod 部署啟用；要啟用請改 `provisionSqlResources=true`，並關掉對應的 `manageExistingSql`。
- 任何敏感值（連線字串、密碼、JWT 簽章金鑰、LINE secret）都應放 GitHub Actions Secrets，不要直接寫入 `deploy.parameters.prod.json`。

---

## 已移除的舊版 Bicep 範本

以下舊版檔案已移除，因為現在採用統一 `deploy.bicep`：

- `main.bicep`
- `main.json`
- `main.parameters.example.json`
- `containerapps.main.bicep`
- `containerapps.main.json`
- `containerapps.parameters.example.json`
- `containerapps.parameters.local.json`
- `staticwebapps.main.bicep`
- `staticwebapps.main.json`
- `staticwebapps.parameters.example.json`
- `deploy.json`

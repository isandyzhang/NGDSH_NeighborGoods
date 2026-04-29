# Bicep 基礎範本（單一生產部署）

本資料夾目前只保留單一生產部署入口 `deploy.bicep`，適用於：

- 單一 Azure 生產資源群組
- Container App 後端 + Static Web Apps 前端混合部署
- 以低成本部署為優先
- 既有 SQL / Storage 也可重用

---

## 目前使用的檔案

- `deploy.bicep`
  - 目前唯一實際部署入口
- `deploy.parameters.json`
  - 生產環境參數檔
- `modules/`
  - 包含 SQL / Storage / SignalR / Container App / Static Web App 等模組

---

## 部署流程

1. 編輯 `infra/bicep/deploy.parameters.json`
2. 驗證部署：

```bash
az deployment group what-if \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/deploy.bicep \
  --parameters @infra/bicep/deploy.parameters.prod.json
```

3. 正式部署：

```bash
az deployment group create \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/deploy.bicep \
  --parameters @infra/bicep/deploy.parameters.prod.json
```

---

## 注意事項

- 若你要重用既有 SQL / Storage，請設定：
  - `provisionSqlResources: false`
  - `provisionStorageResources: false`
  - 並填入 `existingSqlConnectionString` / `existingBlobConnectionString` / `existingBlobContainerName`
- 若要讓 Bicep 建立新的 SQL / Storage，請設定 `provisionSqlResources: true` 和 `provisionStorageResources: true`

> 目前專案只保留正式環境部署，開發請在本地執行，不需要額外的 `dev` 參數檔。
- `lineOidcChannelSecret`、`jwtSigningKey` 等敏感值請勿直接提交到 git

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

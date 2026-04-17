# Bicep 基礎範本（單環境）

本資料夾提供 NeighborGoods 單環境部署範本，符合目前策略：

- 本地開發 + 單一 Azure 正式環境
- 不使用 Key Vault（先以 App Settings 注入）
- 優先低成本方案

---

## 檔案說明

- `main.bicep`
  - 建立以下資源：
    - App Service Plan
    - Web App
    - Azure SQL Server + Database（Basic）
    - Storage Account + Blob Container
    - Azure SignalR（可選，預設開啟）
- `main.parameters.example.json`
  - 參數範例，請複製成 `main.parameters.json` 後填入實際值
- `containerapps.main.bicep`
  - Container Apps 版本（使用 Docker image 部署）
- `containerapps.parameters.example.json`
  - Container Apps 版本參數範例
- `CONTAINERAPPS.md`
  - Container Apps 專用部署與成本說明

---

## 使用前注意

- 目前預設 `App Service Plan = F1 (Free, Windows)`，是最低成本起步。
- 若你要改 Linux App Service，需改為 `B1+` 並調整 `reserved` 與 runtime 設定。
- Azure SQL 沒有長期免費層，`Basic` 為低成本起步。
- `sqlAdminPassword`、`lineOidcChannelSecret` 等參數屬敏感值，請勿提交到 git。

---

## 部署步驟

1. 複製參數檔：

```bash
cp infra/bicep/main.parameters.example.json infra/bicep/main.parameters.json
```

2. 編輯 `infra/bicep/main.parameters.json`，填入真實參數。

3. 先做驗證部署（what-if）：

```bash
az deployment group what-if \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.json
```

4. 正式部署：

```bash
az deployment group create \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.json
```

---

## 部署完成後

- 在輸出中可取得 `webAppUrl`。
- 建議立刻做：
  - 開站健康檢查
  - DB 連線測試
  - Blob 上傳測試
  - LINE OIDC 登入測試

---

## 下一步建議

- 後續可把此範本拆成 module：
  - `app.bicep`
  - `data.bicep`
  - `realtime.bicep`
- 等前後端分離後，可新增前端資源（Static Web Apps）模組。

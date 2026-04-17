# Azure Container Apps 部署說明

這份文件對應：

- `infra/bicep/containerapps.main.bicep`
- `infra/bicep/containerapps.parameters.example.json`

用途：把現有 `Dockerfile` 建出的映像，部署到 Azure Container Apps。

---

## 你問的「Docker 空的」是什麼意思？

- `Dockerfile` 你已經有內容，不是空的。
- 如果你指的是 registry（例如 GHCR）目前沒映像，這是正常的。
- 第一次跑 CI/CD 後，workflow 會自動建置並推第一個 image。

---

## 建議使用流程（GHCR）

1. GitHub Actions 進行：
   - `docker build`
   - `docker push ghcr.io/<owner>/neighborgoods-web:<tag>`
2. Bicep / az CLI 更新 Container App image。

---

## 先決條件

- Azure Resource Group 已建立
- GitHub repo 可使用 Actions
- 已設定 Azure OIDC secrets（建議）：
  - `AZURE_CLIENT_ID`
  - `AZURE_TENANT_ID`
  - `AZURE_SUBSCRIPTION_ID`
- 部署時對應參數檔中要填入真實值（SQL、LINE、Email）

---

## 本機私有參數檔（推薦）

`containerapps.parameters.example.json` 是「可上 Git 的範本」，請不要直接把真機密塞回去。

請在本機建立私有檔（已加入 `.gitignore`，避免誤提交）：

1. 複製範本：

```bash
cp infra/bicep/containerapps.parameters.example.json infra/bicep/containerapps.parameters.local.json
```

2. 編輯 `infra/bicep/containerapps.parameters.local.json`，把所有 `PLEASE-SET-...` 改成真值（SQL/LINE/Email 等）。

3.（選用）第一次只想先把 Container App「建起來」：可把 `containerImage` 暫時改成任意 **公開** 且可拉取的映像；等 GHCR 有映像後，再用 GitHub Actions 或 `az containerapp update` 切回 `ghcr.io/...`。

---

## 使用既有 Azure SQL / Blob（不新建資料層資源）

若你希望 **沿用既有** SQL Database 與 Storage Blob（只新建 Container Apps 相關運算資源），請在參數檔設定：

- `provisionSqlResources`：`false`，並提供 `existingSqlConnectionString`
- `provisionStorageResources`：`false`，並提供 `existingBlobConnectionString` 與 `existingBlobContainerName`

當上述開關為 `false` 時，`sqlAdminLogin` / `sqlAdminPassword` 可不填（留空字串即可）。

---

## Bicep 部署（第一次建資源）

```bash
az deployment group what-if \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/containerapps.main.bicep \
  --parameters @infra/bicep/containerapps.parameters.local.json
```

```bash
az deployment group create \
  --resource-group <YOUR_RG> \
  --template-file infra/bicep/containerapps.main.bicep \
  --parameters @infra/bicep/containerapps.parameters.local.json
```

---

## P1 快速流程（你目前環境）

> 目標：在新資源群組建立全新環境（East Asia）。
>
> 目前範例：`subscription = b6a3c20b-3204-450e-83a5-8bfb0f025082`、`resource group = nbg-sys`。

1. 登入 Azure 並切換訂閱：

```bash
az login
az account set --subscription "b6a3c20b-3204-450e-83a5-8bfb0f025082"
az account show --query "{subscription:id,name:name,tenantId:tenantId}" -o table
```

2. 建立資源群組（East Asia）：

```bash
az group create -n "nbg-sys" -l "eastasia"
```

3. 先做 `what-if` 預演：

```bash
az deployment group what-if \
  --resource-group "nbg-sys" \
  --template-file infra/bicep/containerapps.main.bicep \
  --parameters @infra/bicep/containerapps.parameters.local.json
```

4. 預演無誤再正式建立：

```bash
az deployment group create \
  --resource-group "nbg-sys" \
  --template-file infra/bicep/containerapps.main.bicep \
  --parameters @infra/bicep/containerapps.parameters.local.json
```

5. 查詢部署輸出（Container App 名稱與 URL）：

```bash
az deployment group list -g "nbg-sys" --query "[0].name" -o tsv
az deployment group show \
  --resource-group "nbg-sys" \
  --name "<DEPLOYMENT_NAME>" \
  --query "properties.outputs" -o json
```

6. 建立後記得回填 URL 設定：

- `lineMessagingBaseUrl`
- `emailBaseUrl`

改成實際 `https://<container-app-fqdn>` 後，再跑一次 `create` 套用設定。

---

## Container Apps 重要設定

- 預設對外 ingress：開啟
- 目標埠：`8080`
- `ASPNETCORE_URLS` 會設為 `http://0.0.0.0:8080`
- 最低副本：`0`（省成本）
- 最高副本：`1`（先單實例）

---

## 成本提醒

- Container Apps 低流量下通常比固定 B1 友善，但不是「完全免費」。
- 主要成本通常仍是 Azure SQL。
- Log Analytics 會有少量費用，請留意保留天數與 log 量。

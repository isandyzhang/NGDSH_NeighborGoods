# 第一階段：編譯 (使用完整的 SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 1. 先複製 sln 和 csproj (這是為了利用 Docker 快取，沒改套件就不會重新 Restore)
COPY NeighborGoods.sln ./
COPY NeighborGoods.Web/NeighborGoods.Web.csproj ./NeighborGoods.Web/
RUN dotnet restore

# 2. 複製剩下的所有程式碼
COPY . ./

# 3. 指定專案檔進行編譯與發布
WORKDIR /app/NeighborGoods.Web
RUN dotnet publish -c Release -o /out

# 第二階段：執行 (使用輕量的 Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# 從編譯階段把成品抓過來
COPY --from=build-env /out .

# 設定啟動點 (請確認編譯後的 DLL 名稱是否為這個)
ENTRYPOINT ["dotnet", "NeighborGoods.Web.dll"]

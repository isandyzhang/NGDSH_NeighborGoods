# 第一階段：編譯環境 (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 複製專案檔並進行 Restore (利用 Docker Layer Cache 節省時間)
COPY *.sln .
COPY NGDSH_NeighborGoods/*.csproj ./NGDSH_NeighborGoods/
RUN dotnet restore

# 複製其餘所有原始碼並編譯
COPY . .
RUN dotnet publish -c Release -o out

# 第二階段：執行環境 (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 設定啟動指令
ENTRYPOINT ["dotnet", "NGDSH_NeighborGoods.dll"]

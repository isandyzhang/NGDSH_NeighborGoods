FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NeighborGoods.Api/NeighborGoods.Api.csproj", "NeighborGoods.Api/"]
RUN dotnet restore "NeighborGoods.Api/NeighborGoods.Api.csproj"

COPY . .
RUN dotnet publish "NeighborGoods.Api/NeighborGoods.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NeighborGoods.Api.dll"]

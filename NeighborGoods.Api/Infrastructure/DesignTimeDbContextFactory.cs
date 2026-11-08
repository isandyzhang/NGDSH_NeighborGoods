using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeighborGoods.Data;

namespace NeighborGoods.Api.Infrastructure;

/// <summary>
/// 讓 <c>dotnet ef</c> 明確載入本機 appsettings / user-secrets，
/// 避免 design-time 因 Host 啟動失敗而拿到空連線字串。
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NeighborGoodsDbContext>
{
    public NeighborGoodsDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "NeighborGoods.Api"));
        if (!Directory.Exists(basePath) || !File.Exists(Path.Combine(basePath, "NeighborGoods.Api.csproj")))
        {
            // 若 cwd 已在 Api 專案目錄
            basePath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets(typeof(DesignTimeDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"找不到 ConnectionStrings:DefaultConnection。請確認 {Path.Combine(basePath, $"appsettings.{environment}.json")} 或 user-secrets / 環境變數已設定。");
        }

        var options = new DbContextOptionsBuilder<NeighborGoodsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NeighborGoodsDbContext(options);
    }
}

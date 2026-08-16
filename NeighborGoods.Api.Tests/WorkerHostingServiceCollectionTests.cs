using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Workers;
using NeighborGoods.Workers.Line;
using NeighborGoods.Workers.Messaging;
using NeighborGoods.Workers.PurchaseRequests;

namespace NeighborGoods.Api.Tests;

public sealed class WorkerHostingServiceCollectionTests
{
    [Fact]
    public void AddNeighborGoodsWorkerHosting_RegistersScheduledJobDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=NeighborGoodsWorkerHostingTest;Trusted_Connection=True;Encrypt=False"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddNeighborGoodsWorkerHosting(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPurchaseRequestScheduledOperations>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LinePreferencePushJob>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UnreadMessageEmailNotificationJob>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<QuickResponderEvaluationService>());
    }
}

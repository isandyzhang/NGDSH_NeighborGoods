using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class HostedServicesRegistrationTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public void Program_DoesNotRegisterBackgroundWorkers_AsHostedServices()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);

        var hostedServices = factory.Services.GetServices<IHostedService>().ToList();
        var typeNames = hostedServices.Select(s => s.GetType().FullName ?? s.GetType().Name).ToList();

        foreach (var removed in new[]
                 {
                     "LinePreferencePushWorker",
                     "UnreadMessageEmailNotificationWorker",
                     "PurchaseRequestExpirationWorker",
                     "QuickResponderBadgeWorker"
                 })
        {
            Assert.DoesNotContain(typeNames, n => n.Contains(removed, StringComparison.Ordinal));
        }
    }
}

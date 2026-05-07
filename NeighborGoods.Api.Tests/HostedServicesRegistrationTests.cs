using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeighborGoods.Api.Features.Integrations.Line.Services;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class HostedServicesRegistrationTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public void Program_RegistersLinePreferencePushWorker()
    {
        using var factory = new ListingApiFactory(fixture.ConnectionString);

        var hostedServices = factory.Services.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, service => service is LinePreferencePushWorker);
    }
}

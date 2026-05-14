using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeighborGoods.Functions;
using NeighborGoods.Workers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddNeighborGoodsWorkerHosting(context.Configuration);
        services.AddSingleton<ScheduledTriggers>();
    })
    .Build();

await host.RunAsync();

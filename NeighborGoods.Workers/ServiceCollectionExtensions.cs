using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Data;
using NeighborGoods.Messaging;
using NeighborGoods.Notifications;
using NeighborGoods.Workers.Line;
using NeighborGoods.Workers.Listings;
using NeighborGoods.Workers.Messaging;
using NeighborGoods.Workers.PurchaseRequests;

namespace NeighborGoods.Workers;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 僅註冊背景工作類別（假設 Api 已註冊 DbContext、Email、LINE、SignalR、<see cref="ISystemMessageRealtimePublisher"/>）。
    /// </summary>
    public static IServiceCollection AddNeighborGoodsWorkerJobs(this IServiceCollection services)
    {
        services.AddScoped<IPurchaseRequestScheduledOperations, PurchaseRequestScheduledOperations>();
        services.AddScoped<QuickResponderEvaluationService>();
        services.AddScoped<UnreadMessageEmailNotificationJob>();
        services.AddScoped<LinePreferencePushJob>();
        services.AddScoped<ListingExpiryJob>();
        return services;
    }

    /// <summary>
    /// Azure Functions 等無 WebHost 主機：註冊 Db、通知、SignalR 與所有背景工作。
    /// </summary>
    public static IServiceCollection AddNeighborGoodsWorkerHosting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
        }

        services.AddDbContext<NeighborGoodsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<EmailSenderOptions>(configuration.GetSection(EmailSenderOptions.SectionName));
        services.Configure<LineMessagingOptions>(configuration.GetSection(LineMessagingOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IEmailSender, AcsEmailSender>();
        services.AddHttpClient<ILineMessageSender, LineMessageSender>();
        services.AddHttpClient<LineMessagingQuotaService>();
        services.AddSingleton<LinePushQuotaTracker>();
        services.AddSingleton<LinePushPolicyService>();
        services.AddScoped<LineFlexMessageBuilder>();

        var azureSignalRConnectionString = configuration["Azure:SignalR:ConnectionString"]
            ?? configuration["AzureSignalR:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(azureSignalRConnectionString))
        {
            services.AddSignalR().AddAzureSignalR(azureSignalRConnectionString);
        }
        else
        {
            services.AddSignalR();
        }

        services.AddScoped<ISystemMessageRealtimePublisher, SystemMessageRealtimePublisher>();

        services.AddNeighborGoodsWorkerJobs();

        return services;
    }
}

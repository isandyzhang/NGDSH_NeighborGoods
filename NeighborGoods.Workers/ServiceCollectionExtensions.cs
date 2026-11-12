using System.Net.Http;
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
using NeighborGoods.Workers.Storage;

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
        services.AddScoped<UnreadMessageLineNotificationJob>();
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

        services.AddOptions();
        services.AddMemoryCache();
        services.Configure<EmailSenderOptions>(configuration.GetSection(EmailSenderOptions.SectionName));
        services.Configure<LineMessagingOptions>(configuration.GetSection(LineMessagingOptions.SectionName));
        services.Configure<AzureBlobOptions>(configuration.GetSection(AzureBlobOptions.SectionName));
        services.AddSingleton<ListingImagePublicUrlResolver>();
        services.AddSingleton<IEmailSender, AcsEmailSender>();
        // Functions isolated worker 對 typed-client 介面註冊較容易缺 ITypedHttpClientFactory；
        // 改成具名 HttpClient + ActivatorUtilities，避免 UnreadMessageLineTimer DI 解析失敗。
        services.AddHttpClient(nameof(LineMessageSender));
        services.AddHttpClient(nameof(LineMessagingQuotaService));
        services.AddSingleton<LinePushQuotaTracker>();
        services.AddSingleton<LinePushPolicyService>();
        services.AddTransient<ILineMessageSender>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(nameof(LineMessageSender));
            return ActivatorUtilities.CreateInstance<LineMessageSender>(sp, httpClient);
        });
        services.AddTransient(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(nameof(LineMessagingQuotaService));
            return ActivatorUtilities.CreateInstance<LineMessagingQuotaService>(sp, httpClient);
        });
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
        services.AddScoped<ILineMenuQueryService, LineMenuQueryService>();
        services.AddScoped<LineWebhookReplyService>();

        return services;
    }
}

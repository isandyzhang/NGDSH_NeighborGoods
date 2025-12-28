using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Text;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Hubs;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Configuration;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext + Identity 設定
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // 明確設定密碼規則，並在前端顯示給使用者
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 1;
        
        // 啟用帳號鎖定功能
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // 鎖定 15 分鐘
        options.Lockout.MaxFailedAccessAttempts = 5; // 5 次失敗後鎖定
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<ChineseIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// Authentication：在 Identity 既有設定上額外加入 LINE OIDC
builder.Services.AddAuthentication()
    .AddOpenIdConnect("LINE", "使用 LINE 登入", options =>
    {
        var lineSection = builder.Configuration.GetSection("Authentication:Line");
        var channelId = builder.Configuration["Authentication:Line:ChannelId"];
        var channelSecret = builder.Configuration["Authentication:Line:ChannelSecret"];

        if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(channelSecret))
        {
            throw new InvalidOperationException("LINE 的 ChannelId 或 ChannelSecret 未設定，請使用 dotnet user-secrets 設定。");
        }

        options.Authority = "https://access.line.me";
        options.MetadataAddress = "https://access.line.me/.well-known/openid-configuration";

        options.ClientId = channelId;
        options.ClientSecret = channelSecret;

        options.CallbackPath = lineSection.GetValue<string>("CallbackPath") ?? "/signin-line";

        options.ResponseType = "code";
        options.SaveTokens = true;

        options.Scope.Clear();
        foreach (var scope in (lineSection.GetValue<string>("Scope") ?? "openid profile").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }

        // LINE 的 id_token 使用 HS256，以 Channel Secret 當成對稱金鑰
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "https://access.line.me",
            ValidAudience = channelId,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(channelSecret)),
            NameClaimType = "name"
        };
    });

// Azure Blob Storage 設定
var blobOptions = builder.Configuration.GetSection(AzureBlobOptions.SectionName).Get<AzureBlobOptions>();
if (blobOptions == null || string.IsNullOrEmpty(blobOptions.ConnectionString))
{
    throw new InvalidOperationException(
        "AzureBlob:ConnectionString 未設定。請在 appsettings.json、appsettings.Development.json 或 user-secrets 中設定。\n" +
        "設定方式：dotnet user-secrets set \"AzureBlob:ConnectionString\" \"你的連線字串\"");
}

builder.Services.AddSingleton(blobOptions);
builder.Services.AddSingleton<IBlobService, BlobService>();

// LINE Messaging API 設定
var lineMessagingApiOptions = builder.Configuration.GetSection("LineMessagingApi").Get<LineMessagingApiOptions>();
if (lineMessagingApiOptions != null && 
    !string.IsNullOrEmpty(lineMessagingApiOptions.ChannelAccessToken) &&
    !string.IsNullOrEmpty(lineMessagingApiOptions.ChannelSecret))
{
    builder.Services.Configure<LineMessagingApiOptions>(builder.Configuration.GetSection("LineMessagingApi"));
    
    // 註冊 LINE Messaging API 服務
    builder.Services.AddHttpClient<ILineMessagingApiService, LineMessagingApiService>();
    builder.Services.AddSingleton<ILineMessagingApiService>(sp =>
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LineMessagingApiOptions>>();
        var logger = sp.GetRequiredService<ILogger<LineMessagingApiService>>();
        return new LineMessagingApiService(httpClient, options, logger);
    });
    
    // 註冊背景服務
    builder.Services.AddHostedService<NotificationQueueBackgroundService>();
}
else
{
    // 如果設定不完整，記錄警告但不中斷應用程式啟動
    var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
    logger.LogWarning("LINE Messaging API 設定不完整，LINE 通知功能將無法使用。請設定 LineMessagingApi:ChannelAccessToken 和 LineMessagingApi:ChannelSecret");
}

// 註冊服務層
builder.Services.AddScoped<IListingService, ListingService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Session 設定
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // 設定 JSON 序列化選項，確保 DateTime 使用 ISO 8601 格式（包含時區資訊）
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // DateTime 預設會序列化為 ISO 8601 格式，包含時區資訊
    });

// SignalR 設定
builder.Services.AddSignalR();

// 速率限制設定
builder.Services.AddRateLimiter(options =>
{
    // 登入速率限制：每分鐘最多 5 次
    options.AddFixedWindowLimiter("Login", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });

    // 註冊速率限制：每分鐘最多 3 次
    options.AddFixedWindowLimiter("Register", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 3;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 1;
    });

    // 發送訊息速率限制：每 10 秒最多 10 次
    options.AddFixedWindowLimiter("SendMessage", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 3;
    });

    // 全域速率限制：每秒最多 100 個請求
    options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(1)
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

// 速率限制必須在 UseAuthentication 和 UseAuthorization 之前
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 映射 SignalR Hub
app.MapHub<MessageHub>("/messageHub");

app.Run();

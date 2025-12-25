using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// Add services to the container.
builder.Services.AddControllersWithViews();

// SignalR 設定
builder.Services.AddSignalR();

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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 映射 SignalR Hub
app.MapHub<MessageHub>("/messageHub");

app.Run();

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NeighborGoods.Api.Features.Account;
using NeighborGoods.Api.Features.Account.Services;
using NeighborGoods.Api.Features.Auth;
using NeighborGoods.Api.Features.Auth.Configuration;
using NeighborGoods.Api.Features.Auth.Services;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.Listing.Services;
using NeighborGoods.Api.Features.Integrations.Line;
using NeighborGoods.Api.Features.Integrations.Line.Services;
using NeighborGoods.Api.Features.Messaging;
using NeighborGoods.Api.Features.Messaging.Services;
using NeighborGoods.Api.Features.PurchaseRequests;
using NeighborGoods.Api.Features.PurchaseRequests.Services;
using NeighborGoods.Api.Infrastructure.Storage;
using NeighborGoods.Api.Features.Lookups;
using NeighborGoods.Api.Features.System;
using NeighborGoods.Api.Shared.ApiContracts;
using NeighborGoods.Api.Shared.Notifications;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
const string corsPolicyName = "FrontendCors";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ListingQueryService>();
builder.Services.AddScoped<ListingCommandService>();
builder.Services.AddScoped<ListingConversationNotifyService>();
builder.Services.AddScoped<ListingTopPinService>();
builder.Services.AddScoped<ListingStatusService>();
builder.Services.AddScoped<ListingFavoriteService>();
builder.Services.AddScoped<MessagingQueryService>();
builder.Services.AddScoped<MessagingCommandService>();
builder.Services.AddScoped<PurchaseRequestService>();
builder.Services.AddHostedService<PurchaseRequestExpirationWorker>();
builder.Services.AddSignalR();
builder.Services.AddScoped<AccountRegistrationService>();
builder.Services.AddScoped<AccountEmailVerificationService>();
builder.Services.AddScoped<AccountProfileService>();
builder.Services.AddScoped<AccountLineBindingService>();
builder.Services.AddScoped<AccountLinePreferenceService>();
builder.Services.AddScoped<LineWebhookService>();
builder.Services.AddScoped<LineMenuQueryService>();
builder.Services.AddScoped<LineFlexMessageBuilder>();
builder.Services.AddScoped<PasswordAuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.Configure<EmailSenderOptions>(builder.Configuration.GetSection(EmailSenderOptions.SectionName));
builder.Services.Configure<LineMessagingOptions>(builder.Configuration.GetSection(LineMessagingOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
builder.Services.AddHttpClient<ILineMessageSender, LineMessageSender>();
builder.Services.AddHttpClient<LineMessagingQuotaService>();
builder.Services.AddSingleton<LinePushQuotaTracker>();
builder.Services.AddSingleton<LinePushPolicyService>();
builder.Services.AddSingleton<IBlobStorage, AzureBlobStorage>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILineOAuthStateStore, LineOAuthStateStore>();
builder.Services.AddHttpClient<ILineOAuthClient, LineOAuthClient>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
}

builder.Services.AddDbContext<NeighborGoodsDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<LineOAuthOptions>(builder.Configuration.GetSection(LineOAuthOptions.SectionName));
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection(AzureBlobOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
ValidateJwtOptions(jwtOptions);
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        if (corsAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsAllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/messages"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthWrite", authOptions =>
    {
        authOptions.PermitLimit = 10;
        authOptions.Window = TimeSpan.FromMinutes(1);
        authOptions.QueueLimit = 0;
        authOptions.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("MessagingWrite", messagingOptions =>
    {
        messagingOptions.PermitLimit = 60;
        messagingOptions.Window = TimeSpan.FromMinutes(1);
        messagingOptions.QueueLimit = 0;
        messagingOptions.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("AccountSendCode", accountSendCodeOptions =>
    {
        accountSendCodeOptions.PermitLimit = 5;
        accountSendCodeOptions.Window = TimeSpan.FromMinutes(1);
        accountSendCodeOptions.QueueLimit = 0;
        accountSendCodeOptions.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("AccountWrite", accountWriteOptions =>
    {
        accountWriteOptions.PermitLimit = 20;
        accountWriteOptions.Window = TimeSpan.FromMinutes(1);
        accountWriteOptions.QueueLimit = 0;
        accountWriteOptions.AutoReplenishment = true;
    });
    options.OnRejected = async (context, token) =>
    {
        var response = ApiResponseFactory.Error(
            "RATE_LIMITED",
            "請求過於頻繁，請稍後再試",
            context.HttpContext);
        await context.HttpContext.Response.WriteAsJsonAsync(response, token);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors(corsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapLineWebhookEndpoints();
app.MapLookupEndpoints();
app.MapListingEndpoints();
app.MapMessagingEndpoints();
app.MapPurchaseRequestEndpoints();
app.MapHub<MessageHub>("/hubs/messages");

app.Run();

static void ValidateJwtOptions(JwtOptions options)
{
    if (string.IsNullOrWhiteSpace(options.Issuer))
    {
        throw new InvalidOperationException("Jwt:Issuer is required.");
    }

    if (string.IsNullOrWhiteSpace(options.Audience))
    {
        throw new InvalidOperationException("Jwt:Audience is required.");
    }

    if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey is required and must be at least 32 characters.");
    }
}

public partial class Program;

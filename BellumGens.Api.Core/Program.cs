using System;
#if SQLITE_PROVIDER
using System.Linq;
using System.Security.Cryptography;
using System.Text;
#endif
using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

string[] devCors = [
    "http://localhost:4200",
    "http://localhost:4000",
    "http://localhost:4201",
    "http://localhost:4001"
];

string[] prodCors = [
    "https://bellumgens.com",
    "https://www.bellumgens.com",
    "https://eb-league.com",
    "https://www.eb-league.com",
    "http://staging.bellumgens.com",
    "http://staging.eb-league.com"
];

var builder = WebApplication.CreateBuilder(args);

Program.PublicClientId = builder.Configuration["publicClientId"];

builder.Services.AddDbContext<BellumGensDbContext>(options =>
{
#if SQLITE_PROVIDER
    if (string.Equals(builder.Configuration.GetValue<string>("Database:Provider"), "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
        return;
    }
#endif
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<BellumGensDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMemoryCache();

builder.Services.AddAuthentication("Cookies")
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    })
    .AddBattleNet(options =>
    {
        options.ClientId = builder.Configuration.GetValue<string>("battleNet:clientId");
        options.ClientSecret = builder.Configuration.GetValue<string>("battleNet:secret");
        options.Scope.Clear();
        options.Scope.Add("sc2.profile");
    })
    .AddTwitch(options =>
    {
        options.ClientId = builder.Configuration.GetValue<string>("twitch:clientId");
        options.ClientSecret = builder.Configuration.GetValue<string>("twitch:secret");
        options.CallbackPath = "/signin-twitch";
    })
    .AddSteam(options =>
    {
        options.ApplicationKey = builder.Configuration["steamApiKey"];
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings.
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters = string.Empty;
});

builder.Services.AddSingleton<AppConfiguration>();
builder.Services.AddScoped<ISteamService, SteamServiceProvider>();
builder.Services.AddScoped<IBattleNetService, BattleNetServiceProvider>();
builder.Services.AddScoped<INotificationService, NotificationsService>();
builder.Services.AddScoped<EmailServiceProvider>();
builder.Services.AddScoped<IStorageService, StorageService>();

builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        // Default
        "text/plain",
        "text/css",
        "application/javascript",
        "text/html",
        "application/xml",
        "text/xml",
        "application/json",
        "text/json",

        // Custom
        "image/svg+xml",
        "application/font-woff2"
    };
});

builder.Services.AddControllers();
// builder.Services.AddOpenApiDocument();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    //app.UseOpenApi();
    //app.UseSwaggerUi3();
}

using (var serviceScope = app.Services.CreateScope())
{
    var context = serviceScope.ServiceProvider.GetRequiredService<BellumGensDbContext>();
#if SQLITE_PROVIDER
    if (context.Database.IsSqlite())
    {
        SqliteDevelopmentDatabase.EnsureCurrentModel(context);
    }
    else
#endif
    {
        context.Database.Migrate();
    }
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(o => o.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
                      .WithOrigins(devCors));
}
else
{
    app.UseCors(o => o.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
                      .WithOrigins(prodCors));
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCompression();

app.MapControllers();

app.Run();

partial class Program
{
    internal static string PublicClientId { get; set; }
}

#if SQLITE_PROVIDER
internal static class SqliteDevelopmentDatabase
{
    internal static void EnsureCurrentModel(BellumGensDbContext context)
    {
        var schemaHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(context.Database.GenerateCreateScript())));
        var created = context.Database.EnsureCreated();
        var hasSchemaRecord = context.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = '__DevSchema'")
            .AsEnumerable().Any();

        if (!created && (!hasSchemaRecord || context.Database.SqlQueryRaw<string>(
                "SELECT Hash AS Value FROM \"__DevSchema\"").AsEnumerable().SingleOrDefault() != schemaHash))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }

        context.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"__DevSchema\" (\"Hash\" TEXT NOT NULL)");
        context.Database.ExecuteSqlRaw("DELETE FROM \"__DevSchema\"");
        context.Database.ExecuteSqlInterpolated($"INSERT INTO \"__DevSchema\" (\"Hash\") VALUES ({schemaHash})");
    }
}
#endif

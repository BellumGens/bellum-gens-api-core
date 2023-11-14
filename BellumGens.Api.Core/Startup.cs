using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BellumGens.Api.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using BellumGens.Api.Core.Providers;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace BellumGens.Api.Core
{
    public class Startup
    {
        public static string PublicClientId { get; private set; }

        private static readonly string[] devCors = new string[] {
            "http://localhost:4200",
            "http://localhost:4000",
            "http://localhost:4201",
            "http://localhost:4001"
        };

        private static readonly string[] prodCors = new string[] {
            "https://bellumgens.com",
            "https://www.bellumgens.com",
            "https://eb-league.com",
            "https://www.eb-league.com",
            "http://staging.bellumgens.com",
            "http://staging.eb-league.com"
        };

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            PublicClientId = Configuration["publicClientId"];
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<BellumGensDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<BellumGensDbContext>()
                .AddDefaultTokenProviders();

            services.AddMemoryCache();

            services.AddAuthentication("Cookies")
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
                    options.ClientId = Configuration.GetValue<string>("battleNet:clientId");
                    options.ClientSecret = Configuration.GetValue<string>("battleNet:secret");
                    options.Scope.Clear();
                    options.Scope.Add("sc2.profile");
                })
                .AddTwitch(options =>
                {
                    options.ClientId = Configuration.GetValue<string>("twitch:clientId");
                    options.ClientSecret = Configuration.GetValue<string>("twitch:secret");
                    options.CallbackPath = "/signin-twitch";
                })
                .AddSteam(options =>
                {
                    options.ApplicationKey = Configuration["steamApiKey"];
                });

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
            });

            services.Configure<IdentityOptions>(options =>
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

            services.AddSingleton<AppConfiguration>();
            services.AddScoped<ISteamService, SteamServiceProvider>();
            services.AddScoped<IBattleNetService, BattleNetServiceProvider>();
            services.AddScoped<INotificationService, NotificationsService>();
            services.AddScoped<IEmailSender, EmailServiceProvider>();
            services.AddScoped<IStorageService, StorageService>();

            services.AddResponseCompression(options =>
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

            services.AddControllers();
            services.AddOpenApiDocument();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseOpenApi();
                app.UseSwaggerUi3();
            }

            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetRequiredService<BellumGensDbContext>();
                context.Database.Migrate();
            }

            app.UseHttpsRedirection();
            
            if (env.IsDevelopment())
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

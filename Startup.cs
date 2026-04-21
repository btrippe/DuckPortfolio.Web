using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DuckPortfolio.Web.Data;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace DuckPortfolio.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var portfolioApiScope = Configuration["PortfolioApi:Scope"];

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddMicrosoftIdentityWebApp(options =>
                {
                    Configuration.Bind("AzureAd", options);
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SaveTokens = true;

                    if (!string.IsNullOrWhiteSpace(portfolioApiScope)
                        && !options.Scope.Contains(portfolioApiScope, StringComparer.OrdinalIgnoreCase))
                    {
                        options.Scope.Add(portfolioApiScope);
                    }
                })
                .EnableTokenAcquisitionToCallDownstreamApi(
                    string.IsNullOrWhiteSpace(portfolioApiScope) ? Array.Empty<string>() : new[] { portfolioApiScope })
                .AddInMemoryTokenCaches();

            services.AddAuthorization();
            services.AddHttpClient();

            services.AddControllersWithViews()
                .AddMicrosoftIdentityUI();

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<WeatherForecastService>();
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var portfolioApiScope = Configuration["PortfolioApi:Scope"];

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/account/sign-up-sign-in", async context =>
                {
                    var redirectUri = context.Request.Query["redirectUri"].FirstOrDefault();
                    var prompt = context.Request.Query["prompt"].FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(redirectUri)
                        || !redirectUri.StartsWith("/", StringComparison.Ordinal)
                        || redirectUri.StartsWith("//", StringComparison.Ordinal))
                    {
                        redirectUri = "/";
                    }

                    var properties = new AuthenticationProperties
                    {
                        RedirectUri = redirectUri
                    };

                    if (!string.IsNullOrWhiteSpace(prompt))
                    {
                        properties.Parameters["prompt"] = prompt;
                    }

                    if (!string.IsNullOrWhiteSpace(portfolioApiScope))
                    {
                        properties.Parameters["scope"] = $"openid profile offline_access {portfolioApiScope}";
                    }

                    await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
                });

                endpoints.MapGet("/account/consent-api", async context =>
                {
                    var redirectUri = context.Request.Query["redirectUri"].FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(redirectUri)
                        || !redirectUri.StartsWith("/", StringComparison.Ordinal)
                        || redirectUri.StartsWith("//", StringComparison.Ordinal))
                    {
                        redirectUri = "/auth-check";
                    }

                    var properties = new AuthenticationProperties
                    {
                        RedirectUri = redirectUri
                    };

                    properties.Parameters["prompt"] = "consent";

                    if (!string.IsNullOrWhiteSpace(portfolioApiScope))
                    {
                        properties.Parameters["scope"] = $"openid profile offline_access {portfolioApiScope}";
                    }

                    await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
                });

                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

    }
}

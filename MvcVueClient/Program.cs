using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MvcVueClient;

/// <summary>
///
/// </summary>
public class Program
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        Run(Configure(WebApplication.CreateBuilder(args)).Build());
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    private static WebApplicationBuilder Configure(WebApplicationBuilder builder)
    {

        builder
            .Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        // Add framework services.
        builder.Services.AddMvc();

        // Simple example with dependency injection for a data provider.
        builder.Services.AddSingleton<Providers.IWeatherProvider, Providers.WeatherProviderFake>();

        // Identity provider
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "Cookies";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("Cookies")
            .AddOpenIdConnect("oidc", options =>
            {
                options.SignInScheme = "Cookies";

                options.Authority = "http://localhost:5000";
                options.RequireHttpsMetadata = false;

                options.ResponseType = "code id_token";

                options.ClientId = "mvcvue";
                options.ClientSecret = "secret";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Add("offline_access");

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // Instead of doing a login-redirect, we send back a Unauthorized.
                    if (context.Request.Path.StartsWithSegments(new PathString("/api"))) {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        context.HandleResponse();
                    }

                    return Task.CompletedTask;
                };

            });

        return builder;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="app"></param>
    private static void Run(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();

            //Needs a update ...
            // Webpack initialization with hot-reload.
            //app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
            //{
            //    HotModuleReplacement = true,
            //});
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }

        app.UseAuthentication();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(c =>
        {
            c.MapDefaultControllerRoute();
            //TODO: Update for new Aspnet Core 3.1 endpoint api
            //routes.MapSpaFallbackRoute(
            //    name: "spa-fallback",
            //    defaults: new { controller = "Home", action = "Index" });
        });
    }
}

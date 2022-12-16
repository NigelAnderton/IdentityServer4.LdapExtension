// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MvcClient;

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
        Console.Title = "MVC Client";
        
        Run(Configure(WebApplication.CreateBuilder(args)).Build());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    private static WebApplicationBuilder Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddControllersWithViews();

        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Cookies";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("Cookies")
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = "https://localhost:5001";

                options.ClientId = "mvc";
                options.ClientSecret = "secret";
                options.ResponseType = "code";

                options.Scope.Add("api1");

                options.SaveTokens = true;
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
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }

        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute()
                .RequireAuthorization();
        });
    }
}
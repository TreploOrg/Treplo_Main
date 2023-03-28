﻿using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Treplo.Infrastructure.AspNet;

public static class StartupExtensions
{
    public static IServiceCollection AddMediatr(this IServiceCollection services, params Assembly[] hints)
    {
        services.AddMediatR(hints.Append(Assembly.GetCallingAssembly()).ToArray());
        return services;
    }

    public static IServiceCollection SetupSwaggerAndOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    public static IHostBuilder SetupSerilog(this IHostBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .CreateBootstrapLogger();
        builder.UseSerilog(
            (ctx, config)
                => config.ReadFrom.Configuration(ctx.Configuration)
        );

        return builder;
    }

    public static WebApplication SetupSwaggerEndpoints(this WebApplication app, bool devOnly = true)
    {
        switch (devOnly)
        {
            case true when app.Environment.IsDevelopment():
                app.UseSwagger();
                app.UseSwaggerUI();
                break;
            case false:
                app.UseSwagger();
                app.UseSwaggerUI();
                break;
        }

        return app;
    }
}
using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Treplo.Infrastructure.AspNet;

public static class StartupExtensions
{
    public static WebApplicationBuilder AddMediatr(this WebApplicationBuilder builder, params Assembly[] hints)
    {
        builder.Services.AddMediatR(hints.Append(Assembly.GetCallingAssembly()).ToArray());
        return builder;
    }
    
    public static WebApplicationBuilder SetupSwaggerAndOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        return builder;
    }
    
    public static WebApplicationBuilder SetupSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, config)
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
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Presentation.Extensions;

public static class SerilogExtensions
{
    public static void ConfigureSerilog(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment;
        var configuration = builder.Configuration;

        // Clear default logging providers
        builder.Logging.ClearProviders();

        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "FashionHubAPI")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.WithExceptionDetails()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.StaticFiles", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning);

        if (environment.IsProduction())
        {
            // Read Seq URL from configuration or fallback
            var seqUrl = configuration["Seq:ServerUrl"] ?? "http://fashionhub-seq:80";

            loggerConfig
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.StaticFiles"))
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting.Diagnostics"))
                .WriteTo.File(
                    path: "Logs/Production/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 10485760,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{MachineName}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "Logs/Production/errors/errors-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    fileSizeLimitBytes: 52428800,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "================================================================================={NewLine}[PRODUCTION ERROR]{NewLine}Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}{NewLine}Level: {Level:u3}{NewLine}Source: {SourceContext}{NewLine}Machine: {MachineName}{NewLine}================================================================================={NewLine}{Message:lj}{NewLine}================================================================================={NewLine}{Exception}{NewLine}================================================================================={NewLine}{NewLine}")
                .WriteTo.Seq(seqUrl);
        }
        else
        {
            loggerConfig
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "Logs/fashionhub-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 10485760,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();

        // Use Serilog for all logging
        builder.Host.UseSerilog();

        Log.Information("FashionHub API Starting up in {Environment} mode", environment.EnvironmentName);
    }

    public static void EnsureSerilogClosed(this WebApplication app)
    {
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            Log.Information("FashionHub API is shutting down...");
            Log.CloseAndFlush();
        });
    }
}

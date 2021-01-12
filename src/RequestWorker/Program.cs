using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace RequestWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                CreateLogger();
                var host = CreateHostBuilder(args).Build();
                var env = host.Services.GetRequiredService<IWebHostEnvironment>();
                Log.Information("Starting Host...\n\tEnvironment: {env}", env.EnvironmentName);
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices((hostContext, services) =>
                    {
                        services.AddDaprClient(builder =>
                        {
                            builder.UseJsonSerializationOptions(new()
                            {
                                PropertyNameCaseInsensitive = true,
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                        });
                        services.AddHostedService<Worker>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        app.UseCloudEvents();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapSubscribeHandler();
                            endpoints.MapGet("/config", context =>
                            {
                                if (context.RequestServices.GetService<IConfiguration>() is IConfigurationRoot root)
                                    return context.Response.WriteAsync(root.GetDebugView());

                                return Task.CompletedTask;
                            });

                        });
                    });

                    webBuilder.UseSerilog();
                });

        private static void CreateLogger() =>
            Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.ControlledBy(new LoggingLevelSwitch(LogEventLevel.Information))
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("System", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
                        .Enrich.FromLogContext()
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                            theme: AnsiConsoleTheme.Literate)
                        .CreateLogger();
    }
}

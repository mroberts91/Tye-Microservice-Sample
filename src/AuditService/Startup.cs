using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuditService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddDaprClient(builder =>
            {
                builder.UseJsonSerializationOptions(new()
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            });
            var redisConnection = Configuration.GetValue<string>("RedisHost");
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
            services.AddControllers().AddDapr();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseAuthorization();

            app.UseCloudEvents();

            app.UseEndpoints(endpoints =>
            {
                var logger = endpoints.ServiceProvider.GetRequiredService<ILogger<Startup>>();

                endpoints.MapSubscribeHandler();

                endpoints.MapGet("/config", context =>
                {
                    if (context.RequestServices.GetService<IConfiguration>() is IConfigurationRoot root)
                        return context.Response.WriteAsync(root.GetDebugView());

                    return Task.CompletedTask;

                });

                endpoints.MapGet("/list", async context =>
                {
                    var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                    var redis = redisConnection.GetDatabase();
                    var records = (await redis.ListRangeAsync(Configuration.GetValue<string>("AuditStore")))
                                  .Select(r => JsonSerializer.Deserialize<DocumentAudit>(r.ToString()));

                    await context.Response.WriteAsJsonAsync(records);
                });

                endpoints.MapPost("/processed", async context =>
                {
                    try
                    {
                        logger.LogInformation("Recieved Processed Event");
                        var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                        var redis = redisConnection.GetDatabase();
                        var dapr = context.RequestServices.GetRequiredService<DaprClient>();

                        var processResult = await context.Request.ReadFromJsonAsync<DocumentProcessResult>()
                            ?? throw new ArgumentNullException("Document Process Result", "Unable to write log for process result due to null request body");

                        var auditRecord = new DocumentAudit(Guid.NewGuid(), processResult.Document with { Body = null }, processResult.ProcessType, DateTime.UtcNow);
                        var data = JsonSerializer.Serialize(auditRecord);
                        await redis.ListRightPushAsync(Configuration.GetValue<string>("AuditStore"), data);

                        logger.LogInformation("Wrote Audit Event to Audit store");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unexpected error during processed request.");
                        return;
                    }

                }).WithTopic(Configuration.GetValue<string>("DocumentPubSub"), Configuration.GetValue<string>("DocumentProcessedTopic"));
            });
        }
    }

    public record DocumentProcessResult(Document Document, ProcessType ProcessType) { }
    public record Document(Guid Id, string Title, byte[] Body, DateTime ProcessedUtc, string ExternalServiceName) { }
    public record DocumentAudit(Guid Id, Document Document, ProcessType ProcessType, DateTime CreatedUtc) { }
    public enum ProcessType { Unchanged, Insert, Update }

}

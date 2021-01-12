using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitorService
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

                endpoints.MapGet("/metric", async context =>
                {
                    var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                    var redis = redisConnection.GetDatabase();
                    var records = (await redis.ListRangeAsync(Configuration.GetValue<string>("MonitorStore")))
                                  .Select(r => JsonSerializer.Deserialize<RequestPerformanceMetric>(r.ToString()));

                    await context.Response.WriteAsJsonAsync(records);
                });

                endpoints.MapGet("/error", async context =>
                {
                    var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                    var redis = redisConnection.GetDatabase();
                    var records = (await redis.ListRangeAsync(Configuration.GetValue<string>("ErrorStore")))
                                  .Select(r => JsonSerializer.Deserialize<ErrorThrownData>(r.ToString()));

                    await context.Response.WriteAsJsonAsync(records);
                });

                endpoints.MapGet("/metric/{id:guid}", async context =>
                {
                    try
                    {
                        var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                        var redis = redisConnection.GetDatabase();
                        Guid.TryParse(context.Request.RouteValues["id"].ToString(), out var id);
                        var records = (await redis.ListRangeAsync(Configuration.GetValue<string>("MonitorStore")))
                                      .Select(r => JsonSerializer.Deserialize<RequestPerformanceMetric>(r.ToString()))
                                      .Where(r => r.Id == id);

                        await context.Response.WriteAsJsonAsync(records);
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                    
                });


                endpoints.MapPost($"/{Configuration.GetValue<string>("MonitorTopic")}", async context =>
                {
                    try
                    {
                        logger.LogInformation("Recieved Request Metric");
                        var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                        var redis = redisConnection.GetDatabase();

                        var metric = await context.Request.ReadFromJsonAsync<RequestPerformanceMetric>() 
                            ?? throw new ArgumentNullException("Request Metric", "Unable to process request metric due to null request body");

                        var data = JsonSerializer.Serialize(metric);
                        await redis.ListRightPushAsync(Configuration.GetValue<string>("MonitorStore"), data);

                        logger.LogInformation("Added Request Metric to the Monitor store");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unexpected error during processed request.");
                        return;
                    }

                }).WithTopic(Configuration.GetValue<string>("MonitorPubSub"), Configuration.GetValue<string>("MonitorTopic"));

                endpoints.MapPost($"/{Configuration.GetValue<string>("ErrorTopic")}", async context =>
                {
                    try
                    {
                        logger.LogInformation("Recieved Request Metric");
                        var redisConnection = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                        var redis = redisConnection.GetDatabase();

                        var metric = await context.Request.ReadFromJsonAsync<ErrorThrownData>()
                            ?? throw new ArgumentNullException("Error Metric", "Unable to process error metric due to null request body");

                        var data = JsonSerializer.Serialize(metric);
                        await redis.ListRightPushAsync(Configuration.GetValue<string>("ErrorStore"), data);

                        logger.LogInformation("Added Error Metric to the Error store");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unexpected error during processed request.");
                        return;
                    }

                }).WithTopic(Configuration.GetValue<string>("MonitorPubSub"), Configuration.GetValue<string>("ErrorTopic"));
            });
        }
    }

    public record RequestPerformanceMetric(Guid Id, string ServiceName, string Route, string HttpMethod, long CompletionMilliseconds, DateTime RequestTimeUtc) { }
    public record ErrorThrownData(Guid Id, string ServiceName, string ExceptionMessage, string CallStack, DateTime ErrorDateTimeUtc ) { }
}

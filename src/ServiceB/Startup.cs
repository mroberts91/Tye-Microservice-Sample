using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ServiceB
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
            services.AddControllers();
            services.AddDaprClient(client =>
            {
                client.UseJsonSerializationOptions(new()
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseServiceExceptionHandler("/Error");

            app.UseRouting();

            app.UseCloudEvents();

            app.UseAuthorization();

            app.UseMiddleware<RequestPerformanceMiddleware>();

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

                endpoints.MapPost("/process", async (context) =>
                {
                    var client = context.RequestServices.GetRequiredService<DaprClient>();
                    try
                    {
                        var content = await context.Request.ReadFromJsonAsync<ProcessRequest>();

                        if (content is null || !content.IsValid)
                            throw new InvalidOperationException("Request Content Invalid");

                        if (DateTime.UtcNow.Second % 2 == 0)
                        {
                            var errorDoc = content with { Body = string.Empty };
                            var data = JsonSerializer.Serialize(errorDoc);
                            throw new ApplicationException($"Error while receiving document {data}");
                        }

                        var bytes = Encoding.UTF8.GetBytes(content.Body);

                        Document document =
                            new(Guid.NewGuid(), content.Title,
                                Encoding.UTF8.GetBytes(content.Body),
                                DateTime.UtcNow, Configuration.GetValue<string>("AppName")
                            );

                        await client.PublishEventAsync(Configuration.GetValue<string>("DocumentPubSub"), Configuration.GetValue<string>("DocumentPostTopic"), document)
                        .ContinueWith(task =>
                        {
                            logger.LogInformation(
                                "Published Docuemnt to topic: {topic}",
                                $"{Configuration.GetValue<string>("DocumentPubSub")}:{Configuration.GetValue<string>("DocumentPostTopic")}");

                        });

                        context.Response.StatusCode = (int)HttpStatusCode.OK;

                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogError(ex, "Route: {route} | Message: {msg}", context.Request.Path, ex.Message);
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        await context.Response.WriteAsJsonAsync(new ProcessResponse(HttpStatusCode.BadRequest, ex.Message));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Route: {route} | Message: {msg}", context.Request.Path, ex.Message);
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        await context.Response.WriteAsJsonAsync(new ProcessResponse(HttpStatusCode.InternalServerError, ex.Message));
                    }
                });
            });
        }

        public record ProcessRequest(string Title, string Body)
        {
            public bool IsValid => !Title.IsNullOrWhitespace() && Body is not null;
        }
        public record ProcessResponse(HttpStatusCode HttpStatusCode, string Message) { }
        public record Document(Guid Id, string Title, byte[] Body, DateTime ProcessedUtc, string ExternalServiceName) { }
    }

    public static class Extensions
    {
        public static bool IsNullOrWhitespace(this string value) => string.IsNullOrWhiteSpace(value);
    }
}

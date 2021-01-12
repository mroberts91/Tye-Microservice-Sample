using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceB
{
    public class RequestPerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        public const string CacheKey = "RequestPerf";

        public RequestPerformanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, DaprClient dapr, IConfiguration config)
        {
            var watch = new Stopwatch();
            watch.Start();

            RequestPerformanceMetric record =
                new(Guid.NewGuid(),
                    config.GetValue<string>("AppName"),
                    context.Request.Path.Value ?? "/",
                    context.Request.Method,
                    0, DateTime.UtcNow
                );

            await _next(context)
                .ContinueWith(task =>
                {
                    watch.Stop();
                    record = record with { CompletionMilliseconds = watch.ElapsedMilliseconds };
                });

            await dapr.PublishEventAsync(config.GetValue<string>("MonitorPubSub"), config.GetValue<string>("MonitorTopic"), record);
        }
    }

    public record RequestPerformanceMetric(Guid Id, string ServiceName, string Route, string HttpMethod, long CompletionMilliseconds, DateTime RequestTimeUtc) { }
}

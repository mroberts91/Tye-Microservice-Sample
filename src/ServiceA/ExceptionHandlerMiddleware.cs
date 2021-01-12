using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace ServiceA
{
    public static class ExceptionHandlerExtensions
    {
        public static IApplicationBuilder UseServiceExceptionHandler(this IApplicationBuilder app, string errorPagePath)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            errorPagePath ??= "/Error";

            var options = new ExceptionHandlerOptions()
            {
                ExceptionHandlingPath = new PathString(errorPagePath)
            };
            return app.UseMiddleware<ExceptionHandlerMiddleware>(Options.Create(options));
        }
    }
    /// <summary>
    /// Exception handler designed to capture unhandled exceptions that occur
    /// during the lifecycle of a request. Based of the 
    /// Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware implementation
    /// but with additional logging added during the handling process.
    /// </summary>
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ExceptionHandlerOptions _options;
        private readonly DaprClient _daprClient;
        private readonly IConfiguration _config;
        private readonly Func<object, Task> _clearCacheHeadersDelegate;

        public ExceptionHandlerMiddleware(
            RequestDelegate next,
            DaprClient daprClient,
            IConfiguration config,
            IOptions<ExceptionHandlerOptions> options)
        {
            _next = next;
            _options = options.Value;
            _daprClient = daprClient;
            _config = config;
            _clearCacheHeadersDelegate = ClearCacheHeaders;

            if (_options.ExceptionHandler == null)
            {
                if (_options.ExceptionHandlingPath == null)
                {
                    throw new InvalidOperationException("ExceptionHandlerOptions Not Configured Correctly");
                }
                else
                {
                    _options.ExceptionHandler = _next;
                }
            }
        }

        public Task Invoke(HttpContext context)
        {
            ExceptionDispatchInfo edi;
            try
            {
                var task = _next(context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(this, context, task);
                }

                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                // Get the Exception, but don't continue processing in the catch block as its bad for stack usage.
                edi = ExceptionDispatchInfo.Capture(exception);
            }

            return HandleException(context, edi);

            static async Task Awaited(ExceptionHandlerMiddleware middleware, HttpContext context, Task task)
            {
                ExceptionDispatchInfo edi = null;
                try
                {
                    await task;
                }
                catch (Exception exception)
                {
                    // Get the Exception, but don't continue processing in the catch block as its bad for stack usage.
                    edi = ExceptionDispatchInfo.Capture(exception);
                }

                if (edi != null)
                {
                    await middleware.HandleException(context, edi);
                }
            }
        }

        private async Task HandleException(HttpContext context, ExceptionDispatchInfo edi)
        {
            PathString originalPath = context.Request.Path;
            UnhandledException(edi.SourceException, context);
            // We can't do anything if the response has already started, just abort.
            if (context.Response.HasStarted)
            {
                edi.Throw();
            }

            if (_options.ExceptionHandlingPath.HasValue)
            {
                context.Request.Path = _options.ExceptionHandlingPath;
            }
            try
            {
                ClearHttpContext(context);

                var exceptionHandlerFeature = new ExceptionHandlerFeature()
                {
                    Error = edi.SourceException,
                    Path = originalPath.Value,
                };
                context.Features.Set<IExceptionHandlerFeature>(exceptionHandlerFeature);
                context.Features.Set<IExceptionHandlerPathFeature>(exceptionHandlerFeature);
                context.Response.StatusCode = 500;
                context.Response.OnStarting(_clearCacheHeadersDelegate, context.Response);

                await _options.ExceptionHandler(context);

                return;
            }
            catch (Exception ex2)
            {
                ErrorHandlerException(ex2);
            }
            finally
            {
                context.Request.Path = originalPath;
            }

            edi.Throw(); // Re-throw the original if we couldn't handle it
        }

        private static void ClearHttpContext(HttpContext context)
        {
            context.Response.Clear();

            // An endpoint may have already been set. Since we're going to re-invoke the middleware pipeline we need to reset
            // the endpoint and route values to ensure things are re-calculated.
            context.SetEndpoint(endpoint: null);
            var routeValuesFeature = context.Features.Get<IRouteValuesFeature>();
            routeValuesFeature?.RouteValues?.Clear();
        }

        private static Task ClearCacheHeaders(object state)
        {
            var headers = ((HttpResponse)state).Headers;
            headers[HeaderNames.CacheControl] = "no-cache";
            headers[HeaderNames.Pragma] = "no-cache";
            headers[HeaderNames.Expires] = "-1";
            headers.Remove(HeaderNames.ETag);
            return Task.CompletedTask;
        }

        private void ErrorHandlerException(Exception ex)
        {
            ErrorThrownData data = new(Guid.NewGuid(), _config.GetValue<string>("AppName"), ex.Message, ex.StackTrace, DateTime.UtcNow);
            _daprClient.PublishEventAsync(_config.GetValue<string>("MonitorPubSub"), _config.GetValue<string>("ErrorTopic"), data);
            
        }

        private void UnhandledException(Exception ex, HttpContext ctx)
        {
            ErrorThrownData data = new(Guid.NewGuid(), _config.GetValue<string>("AppName"), ex.Message, ex.StackTrace, DateTime.UtcNow);
            _daprClient.PublishEventAsync(_config.GetValue<string>("MonitorPubSub"), _config.GetValue<string>("ErrorTopic"), data);
        }
    }
    public record ErrorThrownData(Guid Id, string ServiceName, string ExceptionMessage, string CallStack, DateTime ErrorDateTimeUtc) { }
}


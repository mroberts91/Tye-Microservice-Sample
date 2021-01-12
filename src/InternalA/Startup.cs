using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InternalA
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
            services.AddDaprClient(client =>
            {
                client.UseJsonSerializationOptions(new()
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            });

            services.AddControllers().AddDapr();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseServiceExceptionHandler("/Error");

            app.UseRouting();

            app.UseCloudEvents();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                var logger = endpoints.ServiceProvider.GetRequiredService<ILogger<Startup>>();

                endpoints.MapSubscribeHandler();
                endpoints.MapControllers();

                endpoints.MapGet("/config", context =>
                {
                    if (context.RequestServices.GetService<IConfiguration>() is IConfigurationRoot root)
                        return context.Response.WriteAsync(root.GetDebugView());

                    return Task.CompletedTask;

                });

                //endpoints.MapPost($"/documentposted", async context => 
                //{
                //    var document = await context.Request.ReadFromJsonAsync<Documents.Document>();
                //    logger.LogInformation(
                //        "Revieved Document: {id} from Service: {service} at {time}",
                //        document.Id, document.ExternalServiceName, document.ProcessedUtc
                //    );

                //})
                //.WithTopic("document", "documentposted");
                //.WithTopic(Configuration.GetValue<string>("DocumentPubSub"), Configuration.GetValue<string>("DocumentPostTopic"));
            });
        }
    }
}

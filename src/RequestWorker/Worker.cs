using AutoBogus;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RequestWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _services;
        private readonly Random _random = new();

        public Worker(IServiceProvider services, ILogger<Worker> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(10000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await SendRequest(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task SendRequest(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _services.CreateScope();
                var faker = new AutoFaker<ProcessRequest>();
                var dapr = scope.ServiceProvider.GetRequiredService<DaprClient>();
                var data = faker.RuleFor(r => r.Title, r => r.Company.CompanyName())
                                .RuleFor(r => r.Body, r => r.Lorem.Paragraphs(_random.Next(1, 6)))
                                .Generate();

                var app = _random.Next(1, 3) switch
                {
                    2 => "serviceb",
                    _ => "servicea"
                };

                //var metadata = new Dictionary<string, string>
                //{
                //    { "http.verb", "POST" }
                //};

                //await dapr.InvokeBindingAsync(binding, "process", data, metadata, stoppingToken);
                await dapr.InvokeMethodAsync(app, "process", data, new() { Method = HttpMethod.Post }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to send request due to exception: {message}", ex.Message);
                return;
            }
           
        }
    }

    public record ProcessRequest(string Title, string Body) { }
    public record Document(Guid Id, string Title, byte[] Body, DateTime ProcessedUtc, string ExternalServiceName) { }
    public record DocumentAudit(Guid Id, Document Document, ProcessType ProcessType, DateTime CreatedUtc) { }
    public enum ProcessType { Unchanged, Insert, Update }
}

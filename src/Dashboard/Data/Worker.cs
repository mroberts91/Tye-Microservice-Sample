using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Dashboard.Data
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _services;
        private readonly Random _random;

        public Worker(IServiceProvider services, ILogger<Worker> logger)
        {
            _services = services;
            _logger = logger;
            _random = new();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(10000, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetUpdates(stoppingToken);
                await Task.Delay(2 * 60000, stoppingToken);
            }
        }

        private async Task GetUpdates(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dapr = scope.ServiceProvider.GetRequiredService<DaprClient>();
                var metricsManager = scope.ServiceProvider.GetRequiredService<MetricsManager>();
                await Task.Run(() => _logger.LogInformation("Getting Updates in Background"), stoppingToken);
                metricsManager.Metrics = DummyMetrics();
                //var metrics = await dapr.InvokeMethodAsync<RequestPerformanceMetric[]>("monitorservice", "metric", new() { Method = HttpMethod.Get }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to send request due to exception: {message}", ex.Message);
                return;
            }
           
        }

        private IEnumerable<RequestPerformanceMetric> DummyMetrics() 
        {
            return new List<RequestPerformanceMetric>
            {
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "PUT", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 10000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalA", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "PUT", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "PUT", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "PUT", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalB", "/process", "PUT", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalC", "/process", "POST", _random.Next(100, 500), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "POST", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "GET", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "GET", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "GET", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
                new(Guid.NewGuid(), "ExternalD", "/process", "GET", _random.Next(100, 5000), DateTime.Now.AddMinutes(-1 * _random.Next(1, 30))),
            };
        }
    }
}

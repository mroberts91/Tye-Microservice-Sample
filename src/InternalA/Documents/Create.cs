using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace InternalA.Documents
{
    [ApiController]
    public class Create : ControllerBase
    {

        [Topic("document", "posted")]
        [HttpPost("posted")]
        public async Task ProcessDocument(Document document, [FromServices] IConfiguration config, [FromServices] DaprClient dapr, [FromServices] ILogger<Create> logger)
        {
            logger.LogInformation("Revieved Document: {id} from Service: {service} at {time}", document.Id, document.ExternalServiceName, document.ProcessedUtc);

            if (DateTime.UtcNow.Second % 2 == 0)
            {
                var errorDoc = document with { Body = Array.Empty<byte>() };
                var data = JsonSerializer.Serialize(errorDoc);
                throw new ApplicationException($"Error while receiving document {data}");
            }
            

            var state = await dapr.GetStateEntryAsync<Document>(config.GetValue<string>("DocumentStore"), document.Id.ToString());
            DocumentProcessResult result = state.Value switch
            {
                null => new(document, ProcessType.Insert),
                { } d when !d.Equals(document) => new(document, ProcessType.Update),
                _ => new(state.Value, ProcessType.Unchanged)
            };

            state.Value = result.Document;
            await state.SaveAsync();
            logger.LogInformation("Updated state for Document: {id} - {processType}", result.Document.Id, result.ProcessType.ToString());

            await dapr.PublishEventAsync(config.GetValue<string>("DocumentPubSub"), config.GetValue<string>("DocumentProcessedTopic"), result);
        }

        private record DocumentProcessResult(Document Document, ProcessType ProcessType) { }
        private enum ProcessType { Unchanged, Insert, Update }
    }
}

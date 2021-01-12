using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InternalA.Documents
{
    public record Document(Guid Id, string Title, byte[] Body, DateTime ProcessedUtc, string ExternalServiceName) { }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Data
{
    public record RequestPerformanceMetric(Guid Id, string ServiceName, string Route, string HttpMethod, long CompletionMilliseconds, DateTime RequestTimeUtc) { }
}

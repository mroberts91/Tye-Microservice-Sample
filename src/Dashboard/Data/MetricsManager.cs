using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dashboard.Data
{
    public class MetricsManager
    {
        public delegate void MetricsUpdateEventHandler(object sender, MetricsUpdateEventArgs e);
        public event MetricsUpdateEventHandler MetricsUpdated;

        private IEnumerable<RequestPerformanceMetric> _metrics;
        public IEnumerable<RequestPerformanceMetric> Metrics 
        { 
            get => _metrics;
            set
            {
                _metrics = value;
                LastUpdated = DateTime.Now;
                MetricsUpdatedEvent();
            }
        }

        public DateTime? LastUpdated { get; private set; }

        private void MetricsUpdatedEvent() => MetricsUpdated?.Invoke(this, new MetricsUpdateEventArgs(true));
    }

    public record RequestPerMinuteGrouping(DateTime Timestamp, int Count) 
    {
        public string ColumnColor => Count > 2 ? "#5CB85C" : "#FF6358";
    }
}

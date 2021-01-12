using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InternalA.Documents
{
    public class List : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly DaprClient _dapr;

        public List(IConfiguration config, DaprClient dapr)
        {
            _config = config;
            _dapr = dapr;
        }
    }
}

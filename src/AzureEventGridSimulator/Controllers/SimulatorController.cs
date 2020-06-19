using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Controllers
{
    public abstract class SimulatorController : ControllerBase
    {
        private readonly SimulatorSettings _simulatorSettings;

        protected SimulatorController(SimulatorSettings simulatorSettings)
        {
            _simulatorSettings = simulatorSettings;
        }

        protected TopicSettings Topic
        {
            get
            {
                return _simulatorSettings.Topics.First(t => t.Port == HttpContext.Connection.LocalPort);
            }
        }

        protected EventGridEvent[] Events
        {
            get
            {

                var body = HttpContext.RequestBody().Result;
                return JsonConvert.DeserializeObject<EventGridEvent[]>(body);
                
            }
        }
    }
}

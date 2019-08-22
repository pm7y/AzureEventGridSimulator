using System.IO;
using System.Linq;
using AzureEventGridSimulator.Domain.Entities;
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
                using (var reader = new StreamReader(HttpContext.Request.Body))
                {
                    var events = (EventGridEvent[])new JsonSerializer().Deserialize(reader, typeof(EventGridEvent[]));

                    return events;
                }
            }
        }
    }
}

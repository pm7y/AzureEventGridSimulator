using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AzureEventGridSimulator
{
    public class InternalSubscriber
    {
        private readonly HttpListener _listener;
        private readonly int _port;
        private readonly string _topicName;


        private InternalSubscriber(string topicName)
        {
            _topicName = topicName;
            _listener = new HttpListener();
            _port = FreeTcpPort();
        }

        public string Prefix => $"http://127.0.0.1:{_port}/{_topicName}/";

        public string Name => $"{_topicName}-NullSubscriber";

        public static InternalSubscriber New(string topicName)
        {
            var internalSubscriber = new InternalSubscriber(topicName);
            internalSubscriber.Start();

            return internalSubscriber;
        }

        public void Start()
        {
            _listener.Prefixes.Add(Prefix);
            _listener.Start();

#pragma warning disable 4014
            Process();
#pragma warning restore 4014
        }

        private async Task Process()
        {
            while (true)
            {
                var context = await _listener.GetContextAsync();
                var response = context.Response;

                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
            }
        }

        private static int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}

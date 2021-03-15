using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AzureEventGridSimulator.Infrastructure
{
    public class ValidationIpAddress
    {
        private readonly string _ipAddress;

        public ValidationIpAddress()
        {
            var hostName = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(hostName);
            _ipAddress = addresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork &&
                                               !ip.ToString().StartsWith("172") &&
                                               !IPAddress.IsLoopback(ip)).ToString();
        }

        public override string ToString()
        {
            return _ipAddress;
        }

        public static implicit operator string(ValidationIpAddress d)
        {
            return d.ToString();
        }
    }
}

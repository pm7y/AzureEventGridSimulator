using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class KestrelServerOptionsExtensions
    {
        public static KestrelServerOptions ConfigureSimulatorCertificate(this KestrelServerOptions options)
        {
            var configuration = options.ApplicationServices.GetService<IConfiguration>();

            var certificateFile = configuration["Kestrel:Certificates:Default:Path"];
            var certificateFileSpecified = !string.IsNullOrWhiteSpace(certificateFile);

            var certificatePassword = configuration["Kestrel:Certificates:Default:Password"];
            var certificatePasswordSpecified = !string.IsNullOrWhiteSpace(certificatePassword);

            X509Certificate2 certificate = null;
            switch (certificateFileSpecified)
            {
                case true when certificatePasswordSpecified:
                    // The certificate file and password was specified.
                    certificate = new X509Certificate2(certificateFile, certificatePassword);
                    break;
                case true when !certificatePasswordSpecified:
                    // The certificate file was specified but the password wasn't.
                    certificate = new X509Certificate2(certificateFile);
                    break;
            }

            options.ConfigureHttpsDefaults(httpsOptions => { httpsOptions.ServerCertificate = certificate; });

            return options;
        }
    }
}

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class KestrelServerOptionsExtensions
{
    public static KestrelServerOptions ConfigureSimulatorCertificate(this KestrelServerOptions options)
    {
        var configuration = options.ApplicationServices.GetService<IConfiguration>();

        var certificateFile = configuration!["Kestrel:Certificates:Default:Path"];
        var certificateFileSpecified = !string.IsNullOrWhiteSpace(certificateFile);

        var certificatePassword = configuration["Kestrel:Certificates:Default:Password"];
        var certificatePasswordSpecified = !string.IsNullOrWhiteSpace(certificatePassword);

        X509Certificate2 certificate = null;
        if (certificateFileSpecified && certificatePasswordSpecified)
        {
            // The certificate file and password was specified.
            certificate = new X509Certificate2(certificateFile, certificatePassword);
        }
        else if (certificateFileSpecified && !certificatePasswordSpecified)
        {
            // The certificate file was specified but the password wasn't.
            throw new InvalidOperationException("A certificate with a password is required.");
        }

        options.ConfigureHttpsDefaults(httpsOptions => { httpsOptions.ServerCertificate = certificate; });

        return options;
    }
}

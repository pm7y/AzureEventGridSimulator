using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class KestrelServerOptionsExtensions
{
    public static KestrelServerOptions ConfigureSimulatorCertificate(
        this KestrelServerOptions options
    )
    {
        var configuration = options.ApplicationServices.GetRequiredService<IConfiguration>();

        var certificateFile = configuration["Kestrel:Certificates:Default:Path"];
        var certificatePassword = configuration["Kestrel:Certificates:Default:Password"];

        X509Certificate2? certificate = null;
        if (!string.IsNullOrWhiteSpace(certificateFile))
        {
            if (string.IsNullOrWhiteSpace(certificatePassword))
            {
                // The certificate file was specified but the password wasn't.
                throw new InvalidOperationException("A certificate with a password is required.");
            }

            // The certificate file and password was specified.
            certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificateFile,
                certificatePassword
            );
        }

        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = certificate;
        });

        return options;
    }
}

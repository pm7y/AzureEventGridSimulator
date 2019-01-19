<#
Needs to be run with elevated privelages.

Finds a localhost certificate (i.e. the developer cert) and binds this to a port or ports.
We need to do this so that the AzureEventGridEmulator can listen via https (which is the only scheme Azure Event Grid supports).

A port->cert mapping (i.e. netsh statement below) is required for each topic configured in appsettings.json.

#>
clear;

$localhostCertificate = Get-ChildItem -path cert:\LocalMachine\Root | `
                        where { $_.Subject -match "CN\=localhost" -and $_.notafter -ge (Get-Date)  } | `
                        Sort-Object -Descending -Property notafter | `
                        Select -First 1;

$thumbprint = $localhostCertificate.thumbprint;

&netsh.exe http add sslcert ipport=127.0.0.1:60101 certhash=$thumbprint appid="{9c959566-4d24-41f9-8ff5-b7236a886585}"

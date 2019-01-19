<#
Needs to be run with elevated privelages.

Finds a localhost certificate (i.e. the developer cert) and binds this to a port or ports.
We need to do this so that the AzureEventGridEmulator.exe can listen via https (which is the only scheme the event grid client will send to).

Have a look in AzureEventGridSimulator\appsettings.json - you will need a 'netsh.exe ...' line below for each topic port defined in appsettings.json.
#>
clear;

$localhostCertificate = Get-ChildItem -path cert:\LocalMachine\Root | `
                        where { $_.Subject -match "CN\=localhost" -and $_.notafter -ge (Get-Date)  } | `
                        Sort-Object -Descending -Property notafter | `
                        Select -First 1;

$thumbprint = $localhostCertificate.thumbprint;

&netsh.exe http add sslcert ipport=127.0.0.1:60101 certhash=$thumbprint appid="{9c959566-4d24-41f9-8ff5-b7236a886585}"

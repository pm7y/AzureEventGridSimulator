# Stop and remove containers, networks, and volumes created by docker-compose-up.ps1

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

docker-compose -f "$scriptDir/docker-compose.yml" down `
                    --remove-orphans `
                    --volumes

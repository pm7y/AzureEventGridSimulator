# (re)build the image and run it using the settings from 'docker-compose.yml'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

docker-compose -f "$scriptDir/docker-compose.yml" up `
                    --build `
                    --force-recreate `
                    --remove-orphans `
                    --detach
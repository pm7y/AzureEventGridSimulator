# Note: Powershell may throw misleading 'error' message
#       https://stackoverflow.com/questions/2095088/error-when-calling-3rd-party-executable-from-powershell-when-using-an-ide


# rebuild the image and run it using the settings from 'docker-compose.yml'
docker-compose up   --build `
                    --force-recreate `
                    --always-recreate-deps `
                    --remove-orphans `
                    --no-log-prefix `
                    --detach

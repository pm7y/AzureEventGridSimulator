
# rebuild the image and run it using the settings from 'docker-compose.yml'
docker-compose up   --build `
                    --force-recreate `
                    --always-recreate-deps `
                    --remove-orphans `
                    --no-log-prefix `
                    --detach
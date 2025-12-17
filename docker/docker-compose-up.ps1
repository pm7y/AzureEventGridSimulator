# (re)build the image and run it using the settings from 'docker-compose.yml'

docker-compose up   --build `
                    --force-recreate `
                    --remove-orphans `
                    --detach
#!/usr/bin/env bash
# (Re)build the image and run it using the settings from 'docker-compose.yml'.
# Works from any directory, e.g. ./docker/docker-compose-up.sh from the repository root.

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker compose -f "$script_dir/docker-compose.yml" up \
    --build \
    --force-recreate \
    --remove-orphans \
    --detach

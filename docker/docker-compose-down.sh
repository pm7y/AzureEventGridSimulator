#!/usr/bin/env bash
# Stop and remove the containers, networks and volumes created by docker-compose-up.sh.
# Works from any directory, e.g. ./docker/docker-compose-down.sh from the repository root.

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker compose -f "$script_dir/docker-compose.yml" down \
    --remove-orphans \
    --volumes

#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Rebuilds and restarts the Azure Event Grid Simulator Docker Compose stack.

.DESCRIPTION
    Stops all containers, rebuilds images with no cache, and starts the stack in detached mode.

.EXAMPLE
    ./rebuild.ps1
#>

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try {
    Write-Host "Stopping containers..." -ForegroundColor Yellow
    docker-compose down

    Write-Host "Rebuilding images (no cache)..." -ForegroundColor Yellow
    docker-compose build --no-cache

    Write-Host "Starting containers..." -ForegroundColor Green
    docker-compose up -d

    Write-Host "Done. Use 'docker-compose logs -f' to view logs." -ForegroundColor Cyan
}
finally {
    Pop-Location
}

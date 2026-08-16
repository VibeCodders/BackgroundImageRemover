#!/usr/bin/env pwsh
# Builds and launches BackgroundImageRemover.

$ErrorActionPreference = "Stop"
$projectPath = Join-Path $PSScriptRoot "src/BackgroundImageRemover/BackgroundImageRemover.csproj"

dotnet run --project $projectPath

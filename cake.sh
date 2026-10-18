#!/usr/bin/env sh
set -e
dotnet run --project ./cake/cake.csproj -- "$@"

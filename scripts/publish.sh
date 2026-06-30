#!/usr/bin/env bash
set -euo pipefail

OUTPUT="${1:-./publish}"

dotnet restore ./HTX586CONTRACT.slnx
dotnet build ./HTX586CONTRACT.slnx -c Release
dotnet publish ./src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj -c Release -o "$OUTPUT"

echo "Published to $OUTPUT"

#!/usr/bin/env bash
# Regenerates the type-safe C# API client (Shiron.Solaris3Proxy.Sdk) from the running
# service's OpenAPI document using Kiota.
#
# Prerequisites:
#   - Kiota CLI:  dotnet tool install --global Microsoft.OpenApi.Kiota
#   - The API running in Development so the OpenAPI doc is reachable:
#       dotnet run --project src/Solaris3Proxy   # serves http://localhost:5000
set -euo pipefail

cd "$(dirname "$0")/.."

kiota generate \
  -l CSharp \
  -c Solaris3ProxyClient \
  -n Shiron.Solaris3Proxy.Sdk \
  -d http://127.0.0.1:5000/openapi/v1.json \
  -o ./src/generated/Sdk

#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

echo "=== Building Rivet ==="
dotnet build src/Rivet.csproj -c Release "$@"

echo ""
echo "=== Publishing standalone binary ==="
dotnet publish src/Rivet.csproj -c Release -o publish \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=embedded

echo ""
echo "=== Build complete ==="
echo "Binary: publish/Rivet"
echo "Run: ./publish/Rivet -port 25000 -servername \"My Server\" -maxplayers 8"

#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PORT="${SV_PORT:-25000}"
NAME="${SV_NAME:-"Rivet Free Roam"}"
MAX="${SV_MAXPLAYERS:-254}"
PASS="${SV_PASSWORD:-}"

ARGS="-port $PORT -servername \"$NAME\" -maxplayers $MAX"
if [ -n "$PASS" ]; then
    ARGS="$ARGS -password \"$PASS\""
fi

echo "Starting Rivet server on port $PORT..."
exec dotnet run --project src/Rivet.csproj -- $ARGS

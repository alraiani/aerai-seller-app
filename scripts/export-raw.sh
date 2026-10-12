#!/usr/bin/env bash
# Copies the raw landing-zone files from local Azurite to plain files on disk so they can be opened
# in any editor. Read-only against Azurite: it never starts the app and never calls Amazon SP-API.
# Files already on disk are skipped, so re-running after a sync only fetches what is new.
#
# Requires the Azure CLI (az) and the Azurite container (docker compose up -d azurite).
#
# Usage (from anywhere):  scripts/export-raw.sh [destination-folder] [container]
#   destination-folder  default: $AERAI_RAW_EXPORT_DIR, else C:\docker-data\aeraigroup\raw
#   container           default: raw  (use product-images for the picture container)

set -euo pipefail

destination="${1:-${AERAI_RAW_EXPORT_DIR:-C:\\docker-data\\aeraigroup\\raw}}"
container="${2:-raw}"

# Azurite's well-known development account; the same shorthand the app uses locally.
connection="UseDevelopmentStorage=true"

if ! command -v az >/dev/null 2>&1; then
  echo "The Azure CLI (az) is not installed or not on PATH." >&2
  exit 1
fi

# Fail with a clear message if Azurite is not running, instead of az's long retry output.
if ! az storage container exists --name "$container" --connection-string "$connection" --query exists -o tsv 2>/dev/null | grep -qi true; then
  echo "Container '$container' was not found in Azurite. Is it running (docker compose up -d azurite) and has the app written anything yet?" >&2
  exit 1
fi

mkdir -p "$destination"

# Blobs are write-once, so a file that exists on disk never needs fetching again. Download only the
# missing ones (download-batch aborts on the first file that already exists).
blob_names="$(az storage blob list --container-name "$container" --connection-string "$connection" --query "[].name" -o tsv | tr -d '\r')"
total=0
copied=0
while IFS= read -r name; do
  [[ -z "$name" ]] && continue
  total=$((total + 1))
  [[ -f "$destination/$name" ]] && continue
  mkdir -p "$(dirname "$destination/$name")"
  az storage blob download --container-name "$container" --name "$name" --file "$destination/$name" \
    --connection-string "$connection" --no-progress -o none >/dev/null
  copied=$((copied + 1))
done <<< "$blob_names"

if (( copied == 0 )); then
  echo "Up to date: $total file(s) in '$container', nothing new to copy to $destination"
else
  echo "Copied $copied new file(s) ($total total in '$container') to $destination"
fi

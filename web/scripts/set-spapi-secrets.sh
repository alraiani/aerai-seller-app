#!/usr/bin/env bash
# Stores SP-API credentials in .NET user-secrets for local development and switches the app to
# Live mode. Prompts with hidden input so the values never appear in shell history, the terminal,
# or any file inside the repository (user-secrets live under ~/.microsoft/usersecrets).
#
# Usage (from anywhere):  web/scripts/set-spapi-secrets.sh
# Undo / back to the simulator:  web/scripts/set-spapi-secrets.sh --simulated

set -euo pipefail

project="$(cd "$(dirname "$0")/../src/AERai.Web.UI" && pwd)"

if [[ "${1:-}" == "--simulated" ]]; then
  dotnet user-secrets --project "$project" remove "SpApi:Mode" >/dev/null 2>&1 || true
  echo "SpApi:Mode override removed — Development uses the simulator again. Credentials were left in place."
  exit 0
fi

read -rp  "LWA client id (amzn1.application-oa2-client...): " client_id
read -rsp "LWA client secret (hidden): " client_secret; echo
read -rsp "Refresh token (hidden, starts with Atzr|): " refresh_token; echo

if [[ -z "$client_id" || -z "$client_secret" || -z "$refresh_token" ]]; then
  echo "All three values are required; nothing was changed." >&2
  exit 1
fi

# Values go to dotnet via stdin as JSON (not command-line arguments, which other processes can see).
CLIENT_ID="$client_id" CLIENT_SECRET="$client_secret" REFRESH_TOKEN="$refresh_token" python3 -c '
import json, os
print(json.dumps({
    "SpApi:Mode": "Live",
    "SpApi:ClientId": os.environ["CLIENT_ID"],
    "SpApi:ClientSecret": os.environ["CLIENT_SECRET"],
    "SpApi:RefreshToken": os.environ["REFRESH_TOKEN"],
}))' | dotnet user-secrets --project "$project" set >/dev/null

unset client_id client_secret refresh_token
echo "Saved. SpApi:Mode is now Live for local runs. Restart the app (F5) and open Tools → Amazon sync."

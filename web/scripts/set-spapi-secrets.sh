#!/usr/bin/env bash
# Stores Amazon credentials in .NET user-secrets for local development:
#   - Selling Partner API (SP-API): reports for orders, inventory, settlements. Also switches local
#     runs to Live mode.
#   - Amazon Ads API: stored for the upcoming advertising features (not yet used by the app).
#
# Prompts with hidden input for secrets, so values never appear in shell history, the terminal, or
# any file inside the repository. user-secrets live in
# ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json, outside the repo.
# Press Enter at any prompt to skip it; skipped values keep whatever is already stored.
#
# Usage (from anywhere):  web/scripts/set-spapi-secrets.sh
# Back to the simulator:  web/scripts/set-spapi-secrets.sh --simulated

set -euo pipefail

project="$(cd "$(dirname "$0")/../src/AERai.Web.UI" && pwd)"

if [[ "${1:-}" == "--simulated" ]]; then
  dotnet user-secrets --project "$project" remove "SpApi:Mode" >/dev/null 2>&1 || true
  echo "SpApi:Mode override removed — Development uses the simulator again. Credentials were left in place."
  exit 0
fi

echo "== Selling Partner API (Seller Central → Apps and Services → Develop Apps) =="
read -rp  "  LWA client id (amzn1.application-oa2-client.…): " sp_client_id
read -rsp "  LWA client secret (hidden, amzn1.oa2-cs.v1.…): " sp_client_secret; echo
read -rsp "  Refresh token (hidden, Atzr|…): " sp_refresh_token; echo

echo
echo "== Amazon Ads API (advertising.amazon.com → your Ads API LWA app) — Enter to skip =="
read -rp  "  Ads client id (Enter = same as SP-API client id): " ads_client_id
if [[ -z "$ads_client_id" && -n "$sp_client_id" ]]; then
  ads_client_id="$sp_client_id"
  read -rsp "  Ads client secret (hidden, Enter = same as SP-API): " ads_client_secret; echo
  ads_client_secret="${ads_client_secret:-$sp_client_secret}"
else
  read -rsp "  Ads client secret (hidden): " ads_client_secret; echo
fi
read -rsp "  Ads refresh token (hidden, Atzr|…; issued for the advertising scope): " ads_refresh_token; echo
read -rp  "  Ads profile id (numeric, one per marketplace/account; optional): " ads_profile_id

# Values go to dotnet as JSON on stdin via the printf builtin (never as command-line arguments,
# which other processes can see). Only non-empty values are written, so skipping a prompt never
# erases a stored secret.
json="$(SP_ID="$sp_client_id" SP_SECRET="$sp_client_secret" SP_TOKEN="$sp_refresh_token" \
  ADS_ID="$ads_client_id" ADS_SECRET="$ads_client_secret" ADS_TOKEN="$ads_refresh_token" ADS_PROFILE="$ads_profile_id" \
  python3 -c '
import json, os
pairs = {
    "SpApi:ClientId": os.environ["SP_ID"],
    "SpApi:ClientSecret": os.environ["SP_SECRET"],
    "SpApi:RefreshToken": os.environ["SP_TOKEN"],
    "AmazonAds:ClientId": os.environ["ADS_ID"],
    "AmazonAds:ClientSecret": os.environ["ADS_SECRET"],
    "AmazonAds:RefreshToken": os.environ["ADS_TOKEN"],
    "AmazonAds:ProfileId": os.environ["ADS_PROFILE"],
}
values = {k: v.strip() for k, v in pairs.items() if v.strip()}
if all(values.get(k) for k in ("SpApi:ClientId", "SpApi:ClientSecret", "SpApi:RefreshToken")):
    values["SpApi:Mode"] = "Live"
print(json.dumps(values))')"

if [[ "$json" == "{}" ]]; then
  echo "Nothing entered; nothing was changed."
  exit 0
fi

printf '%s' "$json" | dotnet user-secrets --project "$project" set >/dev/null

# Confirm by key name only — values are never printed.
printf '%s' "$json" | python3 -c 'import json, sys; print("Saved:", ", ".join(sorted(json.load(sys.stdin))))'

unset json sp_client_id sp_client_secret sp_refresh_token ads_client_id ads_client_secret ads_refresh_token ads_profile_id
echo "Done. If SP-API was saved, local runs are now Live: restart the app (F5) and open Tools → Amazon sync."

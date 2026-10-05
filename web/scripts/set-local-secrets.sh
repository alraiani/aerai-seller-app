#!/usr/bin/env bash
# Stores local-development secrets for the web app in .NET user-secrets:
#   - Database: the SQL Server connection string (ConnectionStrings:Sql). Optional; defaults match
#     the docker-compose SQL Server and take the SA password from web/.env so the two stay in sync.
#   - Selling Partner API (SP-API): reports for orders, inventory, settlements. Also switches local
#     runs to Live mode.
#   - Amazon Ads API: stored for the upcoming advertising features (not yet used by the app).
#
# Prompts with hidden input for secrets, so values never appear in shell history, the terminal, or
# any file inside the repository. user-secrets live in
# ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json, outside the repo.
# Press Enter at any prompt to skip it; skipped values keep whatever is already stored.
#
# Usage (from anywhere):  web/scripts/set-local-secrets.sh
# Back to the simulator:  web/scripts/set-local-secrets.sh --simulated

set -euo pipefail

web_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$web_root/src/AERai.Web.UI"

if [[ "${1:-}" == "--simulated" ]]; then
  dotnet user-secrets --project "$project" remove "SpApi:Mode" >/dev/null 2>&1 || true
  echo "SpApi:Mode override removed — Development uses the simulator again. Credentials were left in place."
  exit 0
fi

echo "== Database (SQL Server) =="
db_connection=""
read -rp "  Update the database connection string? [y/N]: " update_db
if [[ "$update_db" =~ ^[Yy] ]]; then
  # Default password: the one docker-compose gives SQL Server, from web/.env (gitignored).
  env_password="$(grep -E '^MSSQL_SA_PASSWORD=' "$web_root/.env" 2>/dev/null | head -1 | cut -d= -f2- || true)"
  read -rp  "  Server [localhost,1433]: " db_server
  read -rp  "  Database [AERaiSeller]: " db_name
  read -rp  "  User [sa]: " db_user
  if [[ -n "$env_password" ]]; then
    read -rsp "  Password (hidden, Enter = MSSQL_SA_PASSWORD from web/.env): " db_password; echo
    db_password="${db_password:-$env_password}"
  else
    read -rsp "  Password (hidden): " db_password; echo
  fi
  read -rp  "  Local Docker / self-signed certificate? Trust server certificate [Y/n]: " db_trust

  db_connection="$(SERVER="${db_server:-localhost,1433}" DATABASE="${db_name:-AERaiSeller}" USER_ID="${db_user:-sa}" \
    PASSWORD="$db_password" TRUST="${db_trust:-Y}" python3 -c '
import os
dq = chr(34)
def quote(value):
    # Connection-string values containing ; = or quotes must be wrapped in double quotes (doubled inside).
    if any(c in value for c in (";", "=", dq, chr(39))):
        return dq + value.replace(dq, dq + dq) + dq
    return value
trust = "True" if os.environ["TRUST"].strip().lower() in ("", "y", "yes") else "False"
parts = [
    "Server=" + quote(os.environ["SERVER"]),
    "Database=" + quote(os.environ["DATABASE"]),
    "User Id=" + quote(os.environ["USER_ID"]),
    "Password=" + quote(os.environ["PASSWORD"]),
    "Encrypt=True",
    "TrustServerCertificate=" + trust,
]
print(";".join(parts))')"
fi

echo
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
json="$(DB="$db_connection" SP_ID="$sp_client_id" SP_SECRET="$sp_client_secret" SP_TOKEN="$sp_refresh_token" \
  ADS_ID="$ads_client_id" ADS_SECRET="$ads_client_secret" ADS_TOKEN="$ads_refresh_token" ADS_PROFILE="$ads_profile_id" \
  python3 -c '
import json, os
pairs = {
    "ConnectionStrings:Sql": os.environ["DB"],
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

unset json db_connection db_password env_password sp_client_id sp_client_secret sp_refresh_token ads_client_id ads_client_secret ads_refresh_token ads_profile_id
echo "Done. Restart the app (F5) to pick up the changes. If SP-API was saved, local runs are now Live (Tools → Amazon sync)."

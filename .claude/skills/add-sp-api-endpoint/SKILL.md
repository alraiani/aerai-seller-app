---
name: add-sp-api-endpoint
description: Add a new typed SP-API client call to AERai.Seller.SpApiClient, and keep the "Amazon Workspace" Postman collection in sync with it. Use whenever the app needs to call an SP-API operation (Reports, Orders, Finances, FBA Inventory, Feeds, or any other model) that isn't already covered by an existing typed client.
---

# Add an SP-API endpoint

Adds one new operation to the SP-API client layer, and the matching Postman request, together. Read `CLAUDE.md` at the repo root first for the pipeline rule this skill enforces: **all SP-API calls go through `SpApiRequestPipeline` — no exceptions.**

## Steps

1. **Look up the model.** Find the operation's request/response schema in the official models: https://github.com/amzn/selling-partner-api-models/tree/main/models (pick the right model folder, e.g. `reports-api-model`, `orders-api-model`, `finances-api-model`, `fba-inventory-api-model`, `feeds-api-model`).
2. **Verify against a real call in Postman first.** Using the Postman MCP tools against the "Amazon Workspace" workspace:
   - Find (or create, if the folder doesn't exist yet) the folder for this API model inside the app's collection.
   - Add a request for the operation, reusing the workspace's existing Authorization request/environment variables for the access token — don't duplicate auth logic per-request.
   - Send it and inspect the real response shape (not just the spec) before writing C# against it — SP-API responses sometimes have optional/nullable fields the spec doesn't make obvious.
3. **Add the typed client method** in `AERai.Seller.SpApiClient`:
   - Add to an existing typed client (`OrdersApiClient`, `ReportsApiClient`, `FinancesApiClient`, `FbaInventoryApiClient`, `FeedsApiClient`, ...) or create a new one if this is the first operation for that model.
   - The method body must call through `SpApiRequestPipeline` — it does not construct its own `HttpClient` request pipeline, retry logic, or rate limiting. It only builds the request (path/query/body) and maps the response into a plain DTO.
   - Confirm the operation's documented rate limit (requests/sec + burst) and register/verify it's configured for the pipeline's per-operation token-bucket limiter — don't assume the default.
4. **Write a unit test** in `AERai.Seller.SpApiClient.Tests` covering the new method's request construction and response mapping, using a fake `HttpMessageHandler` (no live network calls).
5. **Confirm the Postman request from step 2 is saved** in the collection, named to match the typed client method (e.g. `Reports > Create Report (GET_LEDGER_SUMMARY_VIEW_DATA)`), so it stays a living reference for manually testing this exact call later.

## Guardrails

- Never bypass `SpApiRequestPipeline` — if you find yourself instantiating `HttpClient` outside `SpApiClient`, stop and reconsider.
- Never let a typed client leak Amazon's raw JSON shape upward — return DTOs shaped for what `Application`/`Infrastructure` actually need.
- Don't skip the Postman step even for "simple" calls — it's the fast way to catch real-world response quirks before they become runtime bugs.

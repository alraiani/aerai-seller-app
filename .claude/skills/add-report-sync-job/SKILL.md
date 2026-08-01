---
name: add-report-sync-job
description: Implement a new Reports-API-backed sync job in the AERai Seller App, following the request -> poll -> download -> parse -> idempotent-upsert pattern used by every existing report sync (inventory, orders, settlements, ledger). Use whenever a new SP-API report type needs to be pulled into the local SQLite database.
---

# Add a report sync job

Every report-backed sync in this app follows the same shape. This skill scaffolds a new one consistently. Read `CLAUDE.md` first.

## The pattern

1. **Request the report** via the `ReportsApiClient` (in `AERai.Seller.SpApiClient` — add the report type here first via `add-sp-api-endpoint` if not already supported): `createReport` with the report type, marketplace, and date range.
2. **Poll for completion**: `getReport` on an interval with backoff until status is `DONE` (or `CANCELLED`/`FATAL`, which should surface as a sync failure, not retry forever).
3. **Download**: `getReportDocument` to get the pre-signed URL, then download the (possibly gzip-compressed) document. This download itself does **not** go through `SpApiRequestPipeline`'s rate limiter (it's a pre-signed S3-style URL, not an SP-API operation) but should still go through shared retry/timeout handling.
4. **Parse**: report documents are typically TSV (tab-separated) with a header row — write a small parser mapping columns to a plain DTO. Keep the parser pure (no DB/HTTP calls) so it's unit-testable in isolation.
5. **Idempotent upsert**: write parsed rows into SQLite via EF Core in `AERai.Seller.Infrastructure`, keyed so re-running the same sync (or re-pulling overlapping date ranges) doesn't create duplicates — upsert by natural key (SKU + date, order ID, settlement ID, etc.), not blind insert.
6. **Record sync metadata**: last-successful-sync timestamp per report type, so the Dashboard's "last sync status" and any scheduled `IHostedService` sync can report/resume correctly.

## Where things live

- Report request/poll/download: `AERai.Seller.SpApiClient` (`ReportsApiClient`).
- TSV parsing: pure function in `AERai.Seller.Application` (unit-testable, no I/O).
- Upsert + sync metadata: `AERai.Seller.Infrastructure` (EF Core), behind an `Application`-layer interface (e.g. `IInventorySyncService`) — the orchestration lives in `Application`, calling `SpApiClient` for data and an `Infrastructure`-implemented repository interface for persistence.

## Guardrails

- Never assume a single poll is enough — report generation is asynchronous and can take seconds to minutes; always poll with backoff and a sane timeout.
- Never treat a partial/failed download as a successful sync — surface it as an error via `ILogger` and leave prior data untouched rather than upserting a partial dataset.
- Add a unit test for the parser (`AERai.Seller.Application.Tests`) using a small fixture of real (anonymized) TSV output where possible.

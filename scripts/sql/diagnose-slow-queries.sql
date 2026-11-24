/*
  diagnose-slow-queries.sql — READ-ONLY performance snapshot of the app database.

  Purpose : find which queries make pages time out, before and after a fix.
  Reads   : Query Store, DMVs, and row counts only. Changes nothing.
  Run     : against the app database (Azure Data Studio / SSMS / sqlcmd, Entra sign-in).
            Each result set is labelled in its first column; paste them all back.
  Note    : results contain query text (SQL only, no customer data) and row counts.
*/
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- 1. Query Store must be on (Azure SQL default) for sections 2–3.
SELECT N'1 query store' AS section, actual_state_desc, readonly_reason,
       current_storage_size_mb, max_storage_size_mb, query_capture_mode_desc
FROM sys.database_query_store_options;

-- 2. Top 25 queries by TOTAL duration over the last 7 days.
WITH Stats AS (
    SELECT p.query_id,
           SUM(rs.count_executions)                                   AS executions,
           SUM(rs.avg_duration * rs.count_executions) / 1000.0        AS total_ms,
           MAX(rs.max_duration) / 1000.0                              AS max_ms,
           SUM(rs.avg_duration * rs.count_executions)
             / NULLIF(SUM(rs.count_executions), 0) / 1000.0           AS avg_ms,
           SUM(rs.avg_cpu_time * rs.count_executions)
             / NULLIF(SUM(rs.count_executions), 0) / 1000.0           AS avg_cpu_ms,
           SUM(rs.avg_logical_io_reads * rs.count_executions)
             / NULLIF(SUM(rs.count_executions), 0)                    AS avg_logical_reads,
           SUM(CASE WHEN rs.execution_type <> 0 THEN rs.count_executions ELSE 0 END) AS aborted_or_failed
    FROM sys.query_store_runtime_stats rs
    JOIN sys.query_store_runtime_stats_interval i ON i.runtime_stats_interval_id = rs.runtime_stats_interval_id
    JOIN sys.query_store_plan p ON p.plan_id = rs.plan_id
    WHERE i.start_time >= DATEADD(DAY, -7, SYSUTCDATETIME())
    GROUP BY p.query_id
)
SELECT TOP (25) N'2 top by total' AS section, s.query_id, s.executions,
       CAST(s.total_ms AS decimal(18,0)) AS total_ms, CAST(s.avg_ms AS decimal(18,0)) AS avg_ms,
       CAST(s.max_ms AS decimal(18,0)) AS max_ms, CAST(s.avg_cpu_ms AS decimal(18,0)) AS avg_cpu_ms,
       CAST(s.avg_logical_reads AS bigint) AS avg_logical_reads, s.aborted_or_failed,
       LEFT(REPLACE(REPLACE(t.query_sql_text, CHAR(13), N' '), CHAR(10), N' '), 600) AS query_text
FROM Stats s
JOIN sys.query_store_query q ON q.query_id = s.query_id
JOIN sys.query_store_query_text t ON t.query_text_id = q.query_text_id
ORDER BY s.total_ms DESC;

-- 3. Top 25 queries by MAX duration (the ones that hit the 30 s timeout; aborted = timed out/cancelled).
WITH Stats AS (
    SELECT p.query_id,
           SUM(rs.count_executions)  AS executions,
           MAX(rs.max_duration) / 1000.0 AS max_ms,
           SUM(CASE WHEN rs.execution_type <> 0 THEN rs.count_executions ELSE 0 END) AS aborted_or_failed
    FROM sys.query_store_runtime_stats rs
    JOIN sys.query_store_runtime_stats_interval i ON i.runtime_stats_interval_id = rs.runtime_stats_interval_id
    JOIN sys.query_store_plan p ON p.plan_id = rs.plan_id
    WHERE i.start_time >= DATEADD(DAY, -7, SYSUTCDATETIME())
    GROUP BY p.query_id
)
SELECT TOP (25) N'3 top by max' AS section, s.query_id, s.executions,
       CAST(s.max_ms AS decimal(18,0)) AS max_ms, s.aborted_or_failed,
       LEFT(REPLACE(REPLACE(t.query_sql_text, CHAR(13), N' '), CHAR(10), N' '), 600) AS query_text
FROM Stats s
JOIN sys.query_store_query q ON q.query_id = s.query_id
JOIN sys.query_store_query_text t ON t.query_text_id = q.query_text_id
ORDER BY s.max_ms DESC;

-- 4. Indexes SQL Server thinks are missing (since the last restart / resume).
SELECT TOP (20) N'4 missing index' AS section,
       CAST(gs.avg_user_impact AS decimal(5,1)) AS avg_impact_pct,
       gs.user_seeks + gs.user_scans AS uses,
       d.statement AS table_name, d.equality_columns, d.inequality_columns, d.included_columns
FROM sys.dm_db_missing_index_group_stats gs
JOIN sys.dm_db_missing_index_groups g ON g.index_group_handle = gs.group_handle
JOIN sys.dm_db_missing_index_details d ON d.index_handle = g.index_handle
WHERE d.database_id = DB_ID()
ORDER BY gs.avg_user_impact * (gs.user_seeks + gs.user_scans) DESC;

-- 5. Row counts of the tables that grow with history.
SELECT N'5 row counts' AS section, s.name + N'.' + o.name AS table_name, SUM(p.rows) AS row_count
FROM sys.partitions p
JOIN sys.objects o ON o.object_id = p.object_id
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE p.index_id IN (0, 1) AND o.type = 'U' AND s.name IN (N'stg', N'core', N'ops')
GROUP BY s.name, o.name
ORDER BY row_count DESC;

-- 6. CPU / IO use over roughly the last hour (15 s samples; high cpu = undersized or busy).
SELECT N'6 resource summary' AS section,
       COUNT(*) AS samples, MIN(end_time) AS from_utc, MAX(end_time) AS to_utc,
       CAST(AVG(avg_cpu_percent) AS decimal(5,1)) AS avg_cpu_pct, MAX(avg_cpu_percent) AS max_cpu_pct,
       CAST(AVG(avg_data_io_percent) AS decimal(5,1)) AS avg_data_io_pct, MAX(avg_data_io_percent) AS max_data_io_pct,
       MAX(avg_memory_usage_percent) AS max_memory_pct
FROM sys.dm_db_resource_stats;

-- 7. Database edition / service objective (serverless vCores).
SELECT N'7 service' AS section, DATABASEPROPERTYEX(DB_NAME(), 'Edition') AS edition,
       DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS service_objective;

/*
  stg.ufn_TryParseDateTimeOffset  (V003: adds day-first dates)
  Purpose : Converts report date text to datetimeoffset, or NULL when it cannot be parsed.
  Inputs  : @Value - one of:
              ISO 8601            '2026-10-01T14:22:31+00:00', '2026-10-01T14:22:31Z'
              ISO-like settlement '2026-09-21 01:53:02 UTC'
              Day-first (CA, MX)  '24.08.2026 01:50:50 UTC', '05.10.2026'
            Text without an offset is treated as UTC.
  Notes   : Day-first text is detected by its dd.mm.yyyy shape and parsed with style 104 BEFORE
            any culture-dependent conversion, because the default (month-first) conversion would
            silently misread '07.09.2026' (7 September) as July 9. Invalid dates return NULL.
            Inline-able scalar function (SQL Server 2019+), safe to use in set-based promotion.
*/
CREATE OR ALTER FUNCTION stg.ufn_TryParseDateTimeOffset (@Value nvarchar(400))
RETURNS datetimeoffset(0)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Text nvarchar(400) = LTRIM(RTRIM(REPLACE(@Value, N' UTC', N'')));

    IF @Text LIKE N'[0-3][0-9].[0-1][0-9].[1-2][0-9][0-9][0-9]%'
        RETURN TODATETIMEOFFSET(TRY_CONVERT(datetime2(0), @Text, 104), '+00:00');

    RETURN COALESCE(
        TRY_CONVERT(datetimeoffset(0), @Text, 127),   -- ISO 8601 with 'Z'
        TRY_CONVERT(datetimeoffset(0), @Text));       -- ISO 8601 with offset, or no offset (UTC)
END;

/*
  stg.ufn_TryParseDateTimeOffset
  Purpose : Converts report date text to datetimeoffset, or NULL when it cannot be parsed.
  Inputs  : @Value - ISO 8601 ('2026-10-01T14:22:31+00:00', '...Z') or settlement style
            ('2026-10-01 07:00:00 UTC'). Text without an offset is treated as UTC.
  Notes   : Inline-able scalar function (SQL Server 2019+), safe to use in set-based promotion.
*/
CREATE OR ALTER FUNCTION stg.ufn_TryParseDateTimeOffset (@Value nvarchar(400))
RETURNS datetimeoffset(0)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Text nvarchar(400) = LTRIM(RTRIM(REPLACE(@Value, N' UTC', N'')));

    RETURN COALESCE(
        TRY_CONVERT(datetimeoffset(0), @Text, 127),   -- ISO 8601 with 'Z'
        TRY_CONVERT(datetimeoffset(0), @Text));       -- ISO 8601 with offset, or no offset (UTC)
END;

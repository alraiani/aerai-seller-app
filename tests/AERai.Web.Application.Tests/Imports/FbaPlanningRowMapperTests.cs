using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Tests.Imports;

public sealed class FbaPlanningRowMapperTests
{
    [Fact]
    public void Append_StagesAgeExcessAndAddsUpTheSurchargeBands()
    {
        var batch = new ImportBatch { Source = ImportSource.FbaInventoryPlanning, MarketplaceId = "ATVPDKIKX0DER", FileName = "p.tsv", UploadedBy = "tests" };
        var fields = new Dictionary<string, string>
        {
            ["sku"] = "FOB00BL",
            ["inv-age-0-to-90-days"] = "20",
            ["inv-age-181-to-270-days"] = "7",
            ["estimated-excess-quantity"] = "6",
            ["estimated-ais-181-210-days"] = "2.50",
            ["estimated-ais-211-240-days"] = "1.00",
            ["estimated-ais-241-270-days"] = "",
            ["marketplace"] = "US",
        };

        new FbaPlanningRowMapper().Append(batch, new ParsedRecord(2, "raw", fields));

        var row = Assert.Single(batch.FbaPlanningRows);
        Assert.Equal(("FOB00BL", "20", "7", "6", "3.50", "US"), (row.Sku, row.Age0To90, row.Age181To270, row.EstimatedExcess, row.AgedSurchargeNextMonth, row.Marketplace));
        Assert.Null(row.Age365Plus);
    }

    [Fact]
    public void Append_NoSurchargeColumns_LeavesItUnknown()
    {
        var batch = new ImportBatch { Source = ImportSource.FbaInventoryPlanning, MarketplaceId = "ATVPDKIKX0DER", FileName = "p.tsv", UploadedBy = "tests" };

        new FbaPlanningRowMapper().Append(batch, new ParsedRecord(2, "raw", new Dictionary<string, string> { ["sku"] = "A" }));

        Assert.Null(Assert.Single(batch.FbaPlanningRows).AgedSurchargeNextMonth);
    }
}

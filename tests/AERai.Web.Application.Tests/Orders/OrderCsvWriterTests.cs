using AERai.Web.Application.Orders;

namespace AERai.Web.Application.Tests.Orders;

public sealed class OrderCsvWriterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("plain", "plain")]
    [InlineData("Mat, blue", "\"Mat, blue\"")]
    [InlineData("12\" mat", "\"12\"\" mat\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("=HYPERLINK(1)", "'=HYPERLINK(1)")]
    [InlineData("-50% off", "'-50% off")]
    [InlineData("-3.50", "-3.50")]
    public void Escape_Value_IsQuotedOrGuardedPerRfc4180(string? value, string expected) =>
        Assert.Equal(expected, OrderCsvWriter.Escape(value));

    [Fact]
    public void Write_Rows_HasHeaderAndLocalPurchaseTimes()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var row = new OrderExportRow("111-1", new DateTimeOffset(2026, 10, 1, 2, 30, 0, TimeSpan.Zero), "Shipped", "USD", "SKU-1", "B0X", "Mat, blue", 2, 39.98m, 6.5m);

        var csv = OrderCsvWriter.Write([row], zone);

        Assert.Equal(
            "order-id,purchase-date,order-status,sku,asin,title,quantity,item-price,currency,unit-cost\r\n" +
            "111-1,2026-09-30T22:30:00-04:00,Shipped,SKU-1,B0X,\"Mat, blue\",2,39.98,USD,6.50\r\n",
            csv);
    }

    [Fact]
    public void Write_NoCost_LeavesTheFieldEmpty()
    {
        var row = new OrderExportRow("111-1", DateTimeOffset.UnixEpoch, "Pending", "USD", "SKU-1", null, null, 1, 10m, null);

        var csv = OrderCsvWriter.Write([row], TimeZoneInfo.Utc);

        Assert.EndsWith(",1,10.00,USD,\r\n", csv, StringComparison.Ordinal);
    }
}

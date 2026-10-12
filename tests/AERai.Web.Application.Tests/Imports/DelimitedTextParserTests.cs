using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Tests.Imports;

public sealed class DelimitedTextParserTests
{
    private static Task<Result<ParsedFile>> ParseAsync(string text, int maxRows = 100) =>
        DelimitedTextParser.ParseAsync(new StringReader(text), maxRows, CancellationToken.None);

    [Fact]
    public async Task ParseAsync_CommaDelimited_ReadsHeadersAndFields()
    {
        var result = await ParseAsync("sku,quantity\nA-1,3\nB-2,5\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(["sku", "quantity"], result.Value.Headers);
        Assert.Equal(2, result.Value.Records.Count);
        Assert.Equal("B-2", result.Value.Records[1].Get("sku"));
        Assert.Equal(2, result.Value.Records[1].RowNumber);
    }

    [Fact]
    public async Task ParseAsync_TabInHeader_UsesTabDelimiter()
    {
        var result = await ParseAsync("sku\tproduct-name\nA-1\tMat, black\n");

        Assert.True(result.IsSuccess);
        Assert.Equal("Mat, black", result.Value.Records[0].Get("product-name"));
    }

    [Fact]
    public async Task ParseAsync_QuotedFields_HandlesDelimitersEscapedQuotesAndLineBreaks()
    {
        var result = await ParseAsync("sku,product-name\nA-1,\"Mat, \"\"pro\"\"\nedition\"\n");

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Value.Records);
        Assert.Equal("Mat, \"pro\"\nedition", record.Get("product-name"));
        Assert.Contains("\n", record.RawLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Amazon Order Id", "amazon-order-id")]
    [InlineData("amazon_order_id", "amazon-order-id")]
    [InlineData("﻿sku", "sku")]
    public void NormalizeHeader_VariantSpellings_Normalize(string header, string expected) =>
        Assert.Equal(expected, DelimitedTextParser.NormalizeHeader(header));

    [Fact]
    public async Task ParseAsync_BlankLinesAndShortRows_SkipsBlankAndPadsMissingColumns()
    {
        var result = await ParseAsync("sku,quantity,currency\n\nA-1,3\n");

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Value.Records);
        Assert.Null(record.Get("currency"));
    }

    [Fact]
    public async Task ParseAsync_MoreRowsThanLimit_Fails()
    {
        var result = await ParseAsync("sku\nA\nB\nC\n", maxRows: 2);

        Assert.True(result.IsFailure);
        Assert.Contains("more than 2", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_DuplicateHeaders_Fails()
    {
        var result = await ParseAsync("sku,SKU\nA,B\n");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ParseAsync_EmptyInput_Fails()
    {
        var result = await ParseAsync(string.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Get_ValueLongerThanMax_Truncates()
    {
        var record = new ParsedRecord(1, "x", new Dictionary<string, string> { ["sku"] = new string('x', 500) });

        Assert.Equal(Domain.Staging.StagingRow.MaxFieldLength, record.Get("sku")!.Length);
    }
}

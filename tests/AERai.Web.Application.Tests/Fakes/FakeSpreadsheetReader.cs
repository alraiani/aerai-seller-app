using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="ISpreadsheetReader"/> returning a canned sheet, recording that it was used.</summary>
internal sealed class FakeSpreadsheetReader : ISpreadsheetReader
{
    public ParsedFile Sheet { get; set; } = new([], []);

    public bool WasCalled { get; private set; }

    public Task<Result<ParsedFile>> ReadAsync(Stream content, int maxRows, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.FromResult(Result.Success(Sheet));
    }
}

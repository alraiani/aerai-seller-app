namespace AERai.Web.Application.Abstractions;

/// <summary>Outcome of an Amazon connection test.</summary>
/// <param name="Succeeded">Whether Amazon accepted the call.</param>
/// <param name="Message">Plain-English outcome; never contains credentials.</param>
/// <param name="TestedAt">When the test ran.</param>
public sealed record ConnectionTestResult(bool Succeeded, string Message, DateTimeOffset TestedAt);

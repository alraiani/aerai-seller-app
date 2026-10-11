namespace AERai.Web.Application.Email;

/// <summary>An outgoing email. Bodies are provided as both HTML and plain text for every client.</summary>
/// <param name="To">Recipient address.</param>
/// <param name="ToName">Recipient display name.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body; any user-supplied values must already be HTML-encoded.</param>
/// <param name="TextBody">Plain-text body.</param>
public sealed record EmailMessage(string To, string ToName, string Subject, string HtmlBody, string TextBody);

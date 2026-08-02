namespace AERai.Seller.Presentation.Messaging;

public enum StatusSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Sent via IMessenger from any ViewModel to update the app-wide status bar.</summary>
public sealed record StatusMessage(string Text, StatusSeverity Severity = StatusSeverity.Info, bool IsBusy = false);

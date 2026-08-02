using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace AERai.Seller.Presentation.ViewModels;

/// <summary>
/// App-wide status bar state. Registered as a DI singleton so the same instance persists across
/// page navigation and receives StatusMessage broadcasts from any ViewModel via IMessenger —
/// senders never need a direct reference to this class.
/// </summary>
public partial class StatusBarViewModel : ObservableObject, IRecipient<StatusMessage>
{
    public StatusBarViewModel(IMessenger messenger)
    {
        Text = "Ready";
        messenger.Register(this);
    }

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private StatusSeverity _severity;

    [ObservableProperty]
    private bool _isBusy;

    public void Receive(StatusMessage message)
    {
        Text = message.Text;
        Severity = message.Severity;
        IsBusy = message.IsBusy;
    }
}

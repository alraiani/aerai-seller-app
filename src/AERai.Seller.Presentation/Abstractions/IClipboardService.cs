namespace AERai.Seller.Presentation.Abstractions;

/// <summary>Abstraction over the clipboard so ViewModels never take a direct WPF dependency (System.Windows.Clipboard).</summary>
public interface IClipboardService
{
    void SetText(string text);
}

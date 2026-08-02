using AERai.Seller.Presentation.Abstractions;

namespace AERai.Seller.Desktop;

/// <summary>Composition-root-only implementation of IClipboardService (see CLAUDE.md's composition-root exception).</summary>
public sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            System.Windows.Clipboard.SetText(text);
        }
    }
}

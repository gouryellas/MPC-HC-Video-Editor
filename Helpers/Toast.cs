using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// A sentence that appears over a window and leaves on its own.
/// </summary>
/// <remarks>
/// For outcomes that need saying but not answering — "nothing changed" is
/// the whole message, and a dialog for it is a window to read, aim at and
/// dismiss in exchange for one line. Click it to send it away early.
/// </remarks>
public static class Toast
{
    public static void Show(Window owner, string text)
    {
        var body = new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            MaxWidth = 440,
            TextWrapping = TextWrapping.Wrap
        };
        body.SetResourceReference(TextBlock.ForegroundProperty, "TextBody");

        var shell = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 10, 16, 10),
            Child = body
        };
        shell.SetResourceReference(Border.BackgroundProperty, "InsetBackground");
        shell.SetResourceReference(Border.BorderBrushProperty, "Accent");

        var tip = new Window
        {
            Owner = owner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = shell
        };

        // Centred over the foot of the owner, where a status line would
        // be, rather than over the middle of what is being read.
        tip.Loaded += (_, _) =>
        {
            tip.Left = owner.Left + (owner.ActualWidth - tip.ActualWidth) / 2;
            tip.Top = owner.Top + owner.ActualHeight - tip.ActualHeight - 64;
        };

        var linger = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        linger.Tick += (_, _) => { linger.Stop(); tip.Close(); };

        tip.MouseLeftButtonDown += (_, _) => { linger.Stop(); tip.Close(); };

        tip.Show();
        linger.Start();
    }
}

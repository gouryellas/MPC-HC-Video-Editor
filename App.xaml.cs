using System.Reflection;
using System.Windows;

namespace MpcHcVideoEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Windows sometimes sets "menu drop alignment" so popups open to the left.
        // Force normal left-to-right menu alignment.
        try
        {
            if (SystemParameters.MenuDropAlignment)
            {
                var field = typeof(SystemParameters)
                    .GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, false);
            }
        }
        catch
        {
            // ignore if runtime layout differs
        }

        base.OnStartup(e);
    }
}

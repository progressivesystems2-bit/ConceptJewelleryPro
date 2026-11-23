using System;
using System.Linq;
using System.Windows;

namespace ConceptJewelleryPro.WPF.Helpers;

public static class WindowExtensions
{
    public static System.Windows.Window? GetSafeOwner()
    {
        var main = System.Windows.Application.Current?.MainWindow;
        if (main != null && main.IsLoaded && main.IsVisible && System.Windows.PresentationSource.FromVisual(main) != null)
        {
            return main;
        }

        if (System.Windows.Application.Current != null)
        {
            foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
            {
                if (win != null && win.IsVisible && win.IsLoaded && System.Windows.PresentationSource.FromVisual(win) != null)
                {
                    return win;
                }
            }
        }

        return null;
    }

    public static void SetOwnerSafely(this System.Windows.Window targetWindow)
    {
        if (targetWindow == null) return;
        try
        {
            var owner = GetSafeOwner();
            if (owner != null && owner != targetWindow)
            {
                targetWindow.Owner = owner;
            }
        }
        catch
        {
            // Fallback: leave Owner null
        }
    }
}

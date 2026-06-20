using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NOAA;

/// <summary>
/// https://learn.microsoft.com/en-us/windows/win32/dwm/blur-ovw
/// https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw
/// https://learn.microsoft.com/en-us/windows/win32/dwm/composition-ovw#controlling-non-client-region-rendering
/// Two of the visual effects that DWM enables are transparency of the non-client region of a window, and transition effects.
/// Your application might have to disable or re-enable these effects for styling or compatibility reasons.
/// The following functions are used to manage transparency and transition effect behavior:
///  - DwmGetWindowAttribute
///  - DwmSetWindowAttribute
/// </summary>
/// <remarks>
/// Minimum supported client: Windows Vista [desktop apps only]
/// As of Windows 8, DWM composition is always enabled, so this message is not sent regardless 
/// of video mode changes. If you are using Windows 7/Vista then you may want to add 
/// "override void WndProc(ref Message m)" for DWMCOMPOSITIONCHANGED messages.
/// Portions taken from https://github.com/Zeliper/z-dark-theme-wpf/blob/master/src/ZDarkTheme.Wpf/DarkTitleBar.cs
/// </remarks>
public static class DarkTitleBar
{
    #region [Win32 API]
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Windows 10 20H1+ and Windows 11
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    // Windows 10 1809-1903 (fallback)
    const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    #endregion

    #region [Attached Properties]
    /// <summary>
    /// Gets the value of the ApplyDarkMode attached property.
    /// </summary>
    public static bool GetApplyDarkMode(DependencyObject obj)
    {
        return (bool)obj.GetValue(ApplyDarkModeProperty);
    }

    /// <summary>
    /// Sets the value of the ApplyDarkMode attached property.
    /// </summary>
    public static void SetApplyDarkMode(DependencyObject obj, bool value)
    {
        obj.SetValue(ApplyDarkModeProperty, value);
    }

    /// <summary>
    /// Attached property to apply dark mode to a Window's title bar.
    /// Usage: &lt;Window local:DarkTitleBar.ApplyDarkMode="True"&gt;
    /// </summary>
    public static readonly DependencyProperty ApplyDarkModeProperty =
        DependencyProperty.RegisterAttached(
            "ApplyDarkMode",
            typeof(bool),
            typeof(DarkTitleBar),
            new PropertyMetadata(false, OnApplyDarkModeChanged));

    static void OnApplyDarkModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window)
        {
            if ((bool)e.NewValue)
            {
                if (window.IsLoaded)
                    Apply(window);
                else
                    window.SourceInitialized += Window_SourceInitialized;
            }
        }
    }

    static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            Apply(window);
        }
    }
    #endregion

    #region [Public Methods]
    /// <summary>
    /// Applies dark mode to the specified window's title bar.
    /// Call this method after the window's SourceInitialized event or when the window is loaded.
    /// </summary>
    /// <param name="window">The window to apply dark mode to.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool Apply(Window window)
    {
        if (window == null) return false;
        var hwnd = new WindowInteropHelper(window).Handle;
        return ApplyToHandle(hwnd);
    }

    /// <summary>
    /// Applies dark mode to a window using its handle.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool ApplyToHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        int useDarkMode = 1;
        // Try Windows 10 20H1+ / Windows 11 attribute first
        int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        // Fallback to older Windows 10 attribute
        if (result != 0)
            result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));

        return result == 0;
    }

    /// <summary>
    /// Removes dark mode from the specified window's title bar.
    /// </summary>
    /// <param name="window">The window to remove dark mode from.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool Remove(Window window)
    {
        if (window == null) return false;
        var hwnd = new WindowInteropHelper(window).Handle;
        return RemoveFromHandle(hwnd);
    }

    /// <summary>
    /// Removes dark mode from a window using its handle.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool RemoveFromHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        int useDarkMode = 0;
        // Try Windows 10 20H1+ / Windows 11 attribute first
        int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        // Fallback to older Windows 10 attribute
        if (result != 0)
            result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));

        return result == 0;
    }
    #endregion
}

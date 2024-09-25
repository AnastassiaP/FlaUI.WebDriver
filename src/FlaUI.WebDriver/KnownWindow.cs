using System;
using CoreFoundation; // For macOS specific references
using Foundation;    // For macOS native types

namespace FlaUI.WebDriver
{
    public class KnownWindow
    {
        public KnownWindow(IntPtr windowHandle, string? windowRuntimeId, string windowIdentifier)
        {
            WindowHandle = windowHandle;
            WindowRuntimeId = windowRuntimeId;
            WindowIdentifier = windowIdentifier;
        }

        public string WindowIdentifier { get; }

        /// <summary>
        /// A temporarily unique ID, cannot be used for identity over time but can be used for improving performance of equality tests.
        /// macOS does not provide direct runtime IDs for windows, so this field is kept for reference.
        /// </summary>
        public string? WindowRuntimeId { get; }

        /// <summary>
        /// On macOS, we use an IntPtr to represent an AXUIElementRef for windows.
        /// </summary>
        public IntPtr WindowHandle { get; }

        /// <summary>
        /// Gets the description of the window, useful for logging/debugging.
        /// </summary>
        public string GetWindowDescription()
        {
            return $"Window with identifier: {WindowIdentifier}, Runtime ID: {WindowRuntimeId}";
        }

        /// <summary>
        /// Compares two KnownWindow instances based on their window handle.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is KnownWindow otherWindow)
            {
                return WindowHandle == otherWindow.WindowHandle;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return WindowHandle.GetHashCode();
        }
    }
}
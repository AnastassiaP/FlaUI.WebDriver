using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FlaUI.WebDriver
{
    public static class AutomationElementExtensions
    {
        // Replace AutomationElement with AXUIElement
        public static bool TryGetPattern(this IntPtr element, string patternName, [NotNullWhen(true)] out IntPtr? pattern)
        {
            // MacOS doesn't use patterns the same way FlaUI does.
            // For macOS, this would be AXUIElement attributes or actions
            var result = AXUIElementCopyAttributeValue(element, patternName, out pattern);
            return result == 0 && pattern != IntPtr.Zero;
        }

        public static bool TryGetProperty(this IntPtr element, string propertyName, out object? value)
        {
            // For macOS, properties would be attributes from AXUIElement
            var result = AXUIElementCopyAttributeValue(element, propertyName, out var propertyValue);

            if (result == 0 && propertyValue != IntPtr.Zero)
            {
                value = propertyValue;
                return true;
            }

            value = null;
            return false;
        }

        // Renamed the second TryGetProperty to avoid conflict
        public static bool TryGetPatternProperty(this IntPtr pattern, string propertyName, out object? value)
        {
            // This is macOS-specific, depending on the attributes available
            // AXUIElement attributes will be used instead of FlaUI's patterns.
            var result = AXUIElementCopyAttributeValue(pattern, propertyName, out var propertyValue);

            if (result == 0 && propertyValue != IntPtr.Zero)
            {
                value = propertyValue;
                return true;
            }

            value = null;
            return false;
        }

        #region P/Invoke for AXUIElement (macOS)

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementCopyAttributeValue(IntPtr element, string attribute, out IntPtr value);

        #endregion
    }
}

using System;
using CoreFoundation; // For macOS specific references
using Foundation;    // For macOS native types

namespace FlaUI.WebDriver
{
    public class KnownElement
    {
        public KnownElement(IntPtr elementHandle, string? elementRuntimeId, string elementReference)
        {
            ElementHandle = elementHandle;
            ElementRuntimeId = elementRuntimeId;
            ElementReference = elementReference;
        }

        public string ElementReference { get; }

        /// <summary>
        /// A temporarily unique ID, cannot be used for identity over time but can be used for improving performance of equality tests.
        /// macOS does not provide direct runtime IDs, so this field is kept for reference.
        /// </summary>
        public string? ElementRuntimeId { get; }

        /// <summary>
        /// In macOS, AXUIElementRef (represented by IntPtr) is the equivalent of AutomationElement.
        /// </summary>
        public IntPtr ElementHandle { get; }

        /// <summary>
        /// Gets the description of the element, useful for logging/debugging.
        /// </summary>
        public string GetElementDescription()
        {
            return $"Element with reference: {ElementReference}, Runtime ID: {ElementRuntimeId}";
        }

        /// <summary>
        /// Compares two KnownElement instances based on their element handle.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is KnownElement otherElement)
            {
                return ElementHandle == otherElement.ElementHandle;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ElementHandle.GetHashCode();
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CoreFoundation;
using AppKit;
using Foundation;

namespace FlaUI.WebDriver
{
    public class Session : IDisposable
    {
        public Session(NSApplication? app, bool isAppOwnedBySession)
        {
            App = app;
            SessionId = Guid.NewGuid().ToString();
            InputState = new InputState();
            TimeoutsConfiguration = new TimeoutsConfiguration();
            IsAppOwnedBySession = isAppOwnedBySession;

            if (app != null)
            {
                // We have to capture the initial window reference on macOS
                CurrentWindowWithElement = GetOrAddKnownWindow(GetMainWindow(app));
            }
        }

        public string SessionId { get; }
        public NSApplication? App { get; }
        public InputState InputState { get; }
        private ConcurrentDictionary<string, KnownElement> KnownElementsByElementReference { get; } = new ConcurrentDictionary<string, KnownElement>();
        private ConcurrentDictionary<string, KnownWindow> KnownWindowsByWindowReference { get; } = new ConcurrentDictionary<string, KnownWindow>();
        public TimeSpan ImplicitWaitTimeout => TimeSpan.FromMilliseconds(TimeoutsConfiguration.ImplicitWaitTimeoutMs);
        public TimeSpan PageLoadTimeout => TimeSpan.FromMilliseconds(TimeoutsConfiguration.PageLoadTimeoutMs);
        public TimeSpan? ScriptTimeout => TimeoutsConfiguration.ScriptTimeoutMs.HasValue ? TimeSpan.FromMilliseconds(TimeoutsConfiguration.ScriptTimeoutMs.Value) : null;
        public bool IsAppOwnedBySession { get; }

        public TimeoutsConfiguration TimeoutsConfiguration { get; set; }

        private KnownWindow? CurrentWindowWithElement { get; set; }

        public AXUIElement CurrentWindow
        {
            get
            {
                if (App == null || CurrentWindowWithElement == null)
                {
                    throw WebDriverResponseException.UnsupportedOperation("This operation is not supported for the Root app");
                }
                return CurrentWindowWithElement.Window;
            }
            set
            {
                CurrentWindowWithElement = GetOrAddKnownWindow(value);
            }
        }

        public string CurrentWindowReference
        {
            get
            {
                if (App == null || CurrentWindowWithElement == null)
                {
                    throw WebDriverResponseException.UnsupportedOperation("This operation is not supported for the Root app");
                }
                return CurrentWindowWithElement.WindowReference;
            }
        }

        public bool IsTimedOut => (DateTime.UtcNow - LastNewCommandTimeUtc) > NewCommandTimeout;

        public TimeSpan NewCommandTimeout { get; internal set; } = TimeSpan.FromSeconds(60);
        public DateTime LastNewCommandTimeUtc { get; internal set; } = DateTime.UtcNow;

        public void SetLastCommandTimeToNow()
        {
            LastNewCommandTimeUtc = DateTime.UtcNow;
        }

        public KnownElement GetOrAddKnownElement(AXUIElement element)
        {
            var elementReference = GetElementReference(element);
            var result = KnownElementsByElementReference.Values.FirstOrDefault(knownElement => SafeElementEquals(knownElement.Element, element));
            if (result == null)
            {
                do
                {
                    result = new KnownElement(element, elementReference, Guid.NewGuid().ToString());
                }
                while (!KnownElementsByElementReference.TryAdd(result.ElementReference, result));
            }
            return result;
        }

        public AXUIElement? FindKnownElementById(string elementId)
        {
            if (!KnownElementsByElementReference.TryGetValue(elementId, out var knownElement))
            {
                return null;
            }
            return knownElement.Element;
        }

        public KnownWindow GetOrAddKnownWindow(AXUIElement window)
        {
            var windowReference = GetElementReference(window);
            var result = KnownWindowsByWindowReference.Values.FirstOrDefault(knownWindow => SafeElementEquals(knownWindow.Window, window));
            if (result == null)
            {
                do
                {
                    result = new KnownWindow(window, windowReference, Guid.NewGuid().ToString());
                }
                while (!KnownWindowsByWindowReference.TryAdd(result.WindowReference, result));
            }
            return result;
        }

        public AXUIElement? FindKnownWindowByWindowReference(string windowReference)
        {
            if (!KnownWindowsByWindowReference.TryGetValue(windowReference, out var knownWindow))
            {
                return null;
            }
            return knownWindow.Window;
        }

        public void RemoveKnownWindow(AXUIElement window)
        {
            var item = KnownWindowsByWindowReference.Values.FirstOrDefault(knownWindow => knownWindow.Window.Equals(window));
            if (item != null)
            {
                KnownWindowsByWindowReference.TryRemove(item.WindowReference, out _);
            }
        }

        public void EvictUnavailableElements()
        {
            var unavailableElements = KnownElementsByElementReference.ToArray().Where(item => !IsElementAvailable(item.Value.Element)).Select(item => item.Key);
            foreach (var unavailableElementKey in unavailableElements)
            {
                KnownElementsByElementReference.TryRemove(unavailableElementKey, out _);
            }
        }

        public void EvictUnavailableWindows()
        {
            var unavailableWindows = KnownWindowsByWindowReference.ToArray().Where(item => !IsElementAvailable(item.Value.Window)).Select(item => item.Key).ToArray();
            foreach (var unavailableWindowKey in unavailableWindows)
            {
                KnownWindowsByWindowReference.TryRemove(unavailableWindowKey, out _);
            }
        }

        public void Dispose()
        {
            if (IsAppOwnedBySession && App != null)
            {
                App.Terminate(this);
            }
        }

        private string? GetElementReference(AXUIElement element)
        {
            // On macOS, AXUIElement can be referred by unique attributes (like the reference or memory address)
            // Placeholder logic to generate a unique element reference
            return element.GetHashCode().ToString();
        }

        private bool SafeElementEquals(AXUIElement element1, AXUIElement element2)
        {
            try
            {
                return element1.Equals(element2);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsElementAvailable(AXUIElement element)
        {
            // Implement logic to check if an element is still available
            // For example, try accessing its attributes or properties
            return true; // Simplified, real checks will depend on AXUIElement availability
        }

        private AXUIElement GetMainWindow(NSApplication app)
        {
            // Placeholder logic to get the main window of the application on macOS using AXUIElement
            return new AXUIElement(app.MainWindow.Handle);
        }
    }
    public class AXUIElement
    {
        // P/Invoke for creating AXUIElement based on a process ID (e.g., for an application)
        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        public static extern IntPtr AXUIElementCreateApplication(int pid);

        // P/Invoke for getting the main window of an application (AXUIElement is just a pointer in this case)
        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        public static extern int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);
    }
}

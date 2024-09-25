using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using FlaUI.WebDriver.Models;

namespace FlaUI.WebDriver.Controllers
{
    [Route("session/{sessionId}/[controller]")]
    [ApiController]
    public class WindowController : ControllerBase
    {
        
        private readonly ILogger<WindowController> _logger;
        private readonly ISessionRepository _sessionRepository;

        public WindowController(ILogger<WindowController> logger, ISessionRepository sessionRepository)
        {
            _logger = logger;
            _sessionRepository = sessionRepository;
        }

        [HttpDelete]
        public async Task<ActionResult> CloseWindow([FromRoute] string sessionId)
        {
            var session = GetSession(sessionId);
            if (session.ApplicationElement == IntPtr.Zero)
            {
                throw WebDriverResponseException.NoWindowsOpenForSession();
            }

            // Get the list of open windows before closing
            var windowHandlesBeforeClose = GetWindowHandles(session).ToArray();
            var currentWindow = session.CurrentWindow;

            session.RemoveKnownWindow(currentWindow);

            // Close the window using AXUIElement API
            AXUIElementPerformAction(currentWindow, "AXClose");

            var remainingWindowHandles = windowHandlesBeforeClose.Except(new[] { session.CurrentWindowHandle });
            if (!remainingWindowHandles.Any())
            {
                _sessionRepository.Delete(session);
                session.Dispose();
                _logger.LogInformation("Closed last window of session and deleted session with ID {SessionId}", sessionId);
            }

            return await Task.FromResult(WebDriverResult.Success(remainingWindowHandles));
        }

        [HttpGet("handles")]
        public async Task<ActionResult> GetWindowHandles([FromRoute] string sessionId)
        {
            var session = GetSession(sessionId);
            var windowHandles = GetWindowHandles(session);
            return await Task.FromResult(WebDriverResult.Success(windowHandles));
        }

        [HttpGet]
        public async Task<ActionResult> GetWindowHandle([FromRoute] string sessionId)
        {
            var session = GetSession(sessionId);
            if (session.FindKnownWindowByWindowHandle(session.CurrentWindowHandle) == null)
            {
                throw WebDriverResponseException.WindowNotFoundByHandle(session.CurrentWindowHandle);
            }
            return await Task.FromResult(WebDriverResult.Success(session.CurrentWindowHandle));
        }

        [HttpPost]
        public async Task<ActionResult> SwitchToWindow([FromRoute] string sessionId, [FromBody] SwitchWindowRequest switchWindowRequest)
        {
            var session = GetSession(sessionId);
            var window = session.FindKnownWindowByWindowHandle(switchWindowRequest.Handle);
            if (window == IntPtr.Zero)
            {
                throw WebDriverResponseException.WindowNotFoundByHandle(switchWindowRequest.Handle);
            }

            session.CurrentWindow = window;
            AXUIElementPerformAction(window, "AXRaise"); // Bring the window to the foreground

            _logger.LogInformation("Switched to window with handle {WindowHandle} (session {SessionId})", switchWindowRequest.Handle, session.SessionId);
            return await Task.FromResult(WebDriverResult.Success());
        }

        [HttpGet("rect")]
        public async Task<ActionResult> GetWindowRect([FromRoute] string sessionId)
        {
            var session = GetSession(sessionId);
            var windowRect = GetWindowRect(session.CurrentWindow);
            return await Task.FromResult(WebDriverResult.Success(windowRect));
        }

        [HttpPost("rect")]
        public async Task<ActionResult> SetWindowRect([FromRoute] string sessionId, [FromBody] WindowRect windowRect)
        {
            var session = GetSession(sessionId);
            var currentWindow = session.CurrentWindow;

            if (windowRect.Width.HasValue && windowRect.Height.HasValue)
            {
                SetWindowSize(currentWindow, windowRect.Width.Value, windowRect.Height.Value);
            }

            if (windowRect.X.HasValue && windowRect.Y.HasValue)
            {
                SetWindowPosition(currentWindow, windowRect.X.Value, windowRect.Y.Value);
            }

            var updatedRect = GetWindowRect(currentWindow);
            return await Task.FromResult(WebDriverResult.Success(updatedRect));
        }

        private IEnumerable<string> GetWindowHandles(Session session)
        {
            if (session.ApplicationElement == IntPtr.Zero)
            {
                throw WebDriverResponseException.UnsupportedOperation("Window operations not supported for Root app");
            }

            // Get the list of open windows (macOS specific implementation)
            var windows = GetAllTopLevelWindows();
            return windows.Select(window => window.ToString()); // Convert to window handles as strings
        }

        private WindowRect GetWindowRect(IntPtr window)
        {
            // Get window position and size (macOS specific implementation)
            var position = GetWindowPosition(window);
            var size = GetWindowSize(window);

            return new WindowRect
            {
                X = position.X,
                Y = position.Y,
                Width = size.Width,
                Height = size.Height
            };
        }

        private Session GetSession(string sessionId)
        {
            var session = _sessionRepository.FindById(sessionId);
            if (session == null)
            {
                throw WebDriverResponseException.SessionNotFound(sessionId);
            }
            session.SetLastCommandTimeToNow();
            return session;
        }

        #region P/Invoke for macOS window operations

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementPerformAction(IntPtr element, string action);

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementCopyAttributeValue(IntPtr element, string attribute, out IntPtr value);

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementSetAttributeValue(IntPtr element, string attribute, IntPtr value);

        private void SetWindowPosition(IntPtr window, double x, double y)
        {
            // macOS API for setting window position
            var position = new CGPoint { X = x, Y = y };
            var positionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(position));
            Marshal.StructureToPtr(position, positionPtr, false);

            // Set the window position using AXUIElement
            AXUIElementSetAttributeValue(window, "AXPosition", positionPtr);

            Marshal.FreeHGlobal(positionPtr);
        }

        private void SetWindowSize(IntPtr window, double width, double height)
        {
            // macOS API for setting window size
            var size = new CGSize { Width = width, Height = height };
            var sizePtr = Marshal.AllocHGlobal(Marshal.SizeOf(size));
            Marshal.StructureToPtr(size, sizePtr, false);

            // Set the window size using AXUIElement
            AXUIElementSetAttributeValue(window, "AXSize", sizePtr);

            Marshal.FreeHGlobal(sizePtr);
        }

        private (double X, double Y) GetWindowPosition(IntPtr window)
        {
            // macOS API for getting window position
            AXUIElementCopyAttributeValue(window, "AXPosition", out var positionPtr);
            if (positionPtr != IntPtr.Zero)
            {
                var position = Marshal.PtrToStructure<CGPoint>(positionPtr);
                return (position.X, position.Y);
            }
            return (0, 0); // Default value if unable to get position
        }

        private (double Width, double Height) GetWindowSize(IntPtr window)
        {
            // macOS API for getting window size
            AXUIElementCopyAttributeValue(window, "AXSize", out var sizePtr);
            if (sizePtr != IntPtr.Zero)
            {
                var size = Marshal.PtrToStructure<CGSize>(sizePtr);
                return (size.Width, size.Height);
            }
            return (0, 0); // Default value if unable to get size
        }

        private IntPtr[] GetAllTopLevelWindows()
        {
            // macOS API to get all top-level windows
            return Array.Empty<IntPtr>(); // Placeholder return
        }

        #endregion

        #region Structs for CGPoint and CGSize

        [StructLayout(LayoutKind.Sequential)]
        public struct CGPoint
        {
            public double X;
            public double Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CGSize
        {
            public double Width;
            public double Height;
        }

        #endregion
    }
}

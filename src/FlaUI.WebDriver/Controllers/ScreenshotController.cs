using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.IO;
using FlaUI.WebDriver.Models;
using FlaUI.WebDriver.Services;
using CoreGraphics;
using AppKit;
using Foundation;

namespace FlaUI.WebDriver.Controllers
{
    [Route("session/{sessionId}")]
    [ApiController]
    public class ScreenshotController : ControllerBase
    {
        private readonly ILogger<ScreenshotController> _logger;
        private readonly ISessionRepository _sessionRepository;

        public ScreenshotController(ILogger<ScreenshotController> logger, ISessionRepository sessionRepository)
        {
            _logger = logger;
            _sessionRepository = sessionRepository;
        }

        [HttpGet("screenshot")]
        public async Task<ActionResult> TakeScreenshot([FromRoute] string sessionId)
        {
            var session = GetActiveSession(sessionId);
            _logger.LogInformation("Taking screenshot for session {SessionId}", session.SessionId);

            // Use macOS API to capture the entire screen
            var screenshot = CaptureScreen();
            var base64Data = GetBase64Data(screenshot);

            return await Task.FromResult(WebDriverResult.Success(base64Data));
        }

        [HttpGet("element/{elementId}/screenshot")]
        public async Task<ActionResult> TakeElementScreenshot([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);

            _logger.LogInformation("Taking screenshot of element with ID {ElementId} for session {SessionId}", elementId, session.SessionId);

            // Use macOS API to capture the element's bounding rectangle
            var screenshot = CaptureElement(element);
            var base64Data = GetBase64Data(screenshot);

            return await Task.FromResult(WebDriverResult.Success(base64Data));
        }

        private static string GetBase64Data(NSImage screenshot)
        {
            using var imageData = screenshot.AsTiff();
            using var memoryStream = new MemoryStream(imageData.AsStream().ToArray());
            return Convert.ToBase64String(memoryStream.ToArray());
        }

        private static NSImage CaptureScreen()
        {
            // Capture the full screen using CGWindowListCreateImage
            var screenRect = NSScreen.MainScreen.Frame;
            var screenImage = CGWindowList.CreateImage(screenRect, CGWindowListOption.All, CGWindowImageOption.Default);

            return new NSImage(screenImage, screenRect.Size);
        }

        private static NSImage CaptureElement(IntPtr element)
        {
            // Get the bounding rectangle of the element
            if (TryGetAttribute(element, "AXFrame", out var frameValue))
            {
                var frame = (CGRect)frameValue;
                var elementImage = CGWindowList.CreateImage(frame, CGWindowListOption.All, CGWindowImageOption.Default);

                return new NSImage(elementImage, frame.Size);
            }

            throw new InvalidOperationException("Could not capture element screenshot. Element has no frame.");
        }

        private IntPtr GetElement(Session session, string elementId)
        {
            var element = session.FindKnownElementById(elementId);
            if (element == IntPtr.Zero)
            {
                throw WebDriverResponseException.ElementNotFound(elementId);
            }
            return element;
        }

        private Session GetActiveSession(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session.ApplicationElement == IntPtr.Zero)
            {
                throw WebDriverResponseException.NoWindowsOpenForSession();
            }
            return session;
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

        #region P/Invoke for AXUIElement (macOS)

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementCopyAttributeValue(IntPtr element, string attribute, out IntPtr value);

        private static bool TryGetAttribute(IntPtr element, string attribute, out object? value)
        {
            int result = AXUIElementCopyAttributeValue(element, attribute, out var attributeValue);
            if (result == 0 && attributeValue != IntPtr.Zero)
            {
                value = Marshal.PtrToStructure(attributeValue, typeof(CGRect));
                return true;
            }

            value = null;
            return false;
        }

        #endregion
    }
}

using System.Text;
using System.Runtime.InteropServices;
using FlaUI.WebDriver.Models;
using FlaUI.WebDriver.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlaUI.WebDriver.Controllers
{
    [Route("session/{sessionId}/[controller]")]
    [ApiController]
    public class ElementController : ControllerBase
    {
        private readonly ILogger<ElementController> _logger;
        private readonly ISessionRepository _sessionRepository;
        private readonly IActionsDispatcher _actionsDispatcher;

        public ElementController(ILogger<ElementController> logger, ISessionRepository sessionRepository, IActionsDispatcher actionsDispatcher)
        {
            _logger = logger;
            _sessionRepository = sessionRepository;
            _actionsDispatcher = actionsDispatcher;
        }

        [HttpGet("active")]
        public async Task<ActionResult> GetActiveElement([FromRoute] string sessionId)
        {
            var session = GetActiveSession(sessionId);
            var element = session.GetOrAddKnownElement(GetFocusedElement(session.ApplicationElement));
            return await Task.FromResult(WebDriverResult.Success(new FindElementResponse()
            {
                ElementReference = element.ElementReference
            }));
        }

        [HttpGet("{elementId}/displayed")]
        public async Task<ActionResult> IsElementDisplayed([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);

            if (TryGetAttribute(element, "AXIsOffscreen", out var offscreenValue))
            {
                return await Task.FromResult(WebDriverResult.Success(!(bool)offscreenValue));
            }

            return await Task.FromResult(WebDriverResult.Success(true));
        }

        [HttpGet("{elementId}/enabled")]
        public async Task<ActionResult> IsElementEnabled([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);
            var isEnabled = TryGetAttribute(element, "AXEnabled", out var value) && (bool)value;

            return await Task.FromResult(WebDriverResult.Success(isEnabled));
        }

        [HttpGet("{elementId}/name")]
        public async Task<ActionResult> GetElementTagName([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);

            if (TryGetAttribute(element, "AXRole", out var role))
            {
                return await Task.FromResult(WebDriverResult.Success(role.ToString()));
            }

            return await Task.FromResult(WebDriverResult.Success("Unknown"));
        }

        [HttpPost("{elementId}/click")]
        public async Task<ActionResult> ElementClick([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);

            ScrollElementContainerIntoView(element);

            // Perform the AXUIElement "AXPress" action (equivalent to a click)
            var result = AXUIElementPerformAction(element, "AXPress");
            if (result != 0)
            {
                return ElementNotInteractable(elementId);
            }

            return WebDriverResult.Success();
        }

        [HttpPost("{elementId}/clear")]
        public async Task<ActionResult> ElementClear([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);

            // Clear text for textboxes
            if (TryGetAttribute(element, "AXValue", out _))
            {
                SetAttribute(element, "AXValue", "");
                return await Task.FromResult(WebDriverResult.Success());
            }

            return await Task.FromResult(WebDriverResult.BadRequest("Element is not a text input."));
        }

        [HttpGet("{elementId}/text")]
        public async Task<ActionResult> GetElementText([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);
            var text = GetElementText(element);

            return await Task.FromResult(WebDriverResult.Success(text));
        }

        private static string GetElementText(IntPtr element)
        {
            // Fetch the "AXValue" or "AXTitle" attribute to get the text of the element
            if (TryGetAttribute(element, "AXValue", out var value) ||
                TryGetAttribute(element, "AXTitle", out value))
            {
                return value?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        [HttpGet("{elementId}/rect")]
        public async Task<ActionResult> GetElementRect([FromRoute] string sessionId, [FromRoute] string elementId)
        {
            var session = GetSession(sessionId);
            var element = GetElement(session, elementId);
            
            if (TryGetAttribute(element, "AXFrame", out var frame))
            {
                var elementRect = (ElementRect)frame;
                return await Task.FromResult(WebDriverResult.Success(elementRect));
            }

            return WebDriverResult.BadRequest("Unable to retrieve element rect.");
        }

        private void ScrollElementContainerIntoView(IntPtr element)
        {
            try
            {
                // Use AXUIElement "AXShowMenu" or similar to scroll into view
                AXUIElementPerformAction(element, "AXShowMenu");
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Could not scroll element into view.");
            }
        }

        private static ActionResult ElementNotInteractable(string elementId)
        {
            return WebDriverResult.BadRequest(new ErrorResponse()
            {
                ErrorCode = "element not interactable",
                Message = $"Element with ID {elementId} is off screen or not interactable."
            });
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

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementSetAttributeValue(IntPtr element, string attribute, IntPtr value);

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern int AXUIElementPerformAction(IntPtr element, string action);

        private static bool TryGetAttribute(IntPtr element, string attribute, out object? value)
        {
            int result = AXUIElementCopyAttributeValue(element, attribute, out var attributeValue);
            if (result == 0 && attributeValue != IntPtr.Zero)
            {
                value = Marshal.PtrToStructure(attributeValue, typeof(object));
                return true;
            }

            value = null;
            return false;
        }

        private static void SetAttribute(IntPtr element, string attribute, string newValue)
        {
            var ptr = Marshal.StringToHGlobalAuto(newValue);
            AXUIElementSetAttributeValue(element, attribute, ptr);
            Marshal.FreeHGlobal(ptr);
        }

        #endregion
    }
}
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using FlaUI.WebDriver.Models;
using FlaUI.WebDriver.Services;

namespace FlaUI.WebDriver.Controllers
{
    [Route("session/{sessionId}")]
    [ApiController]
    public class FindElementsController : ControllerBase
    {
        private readonly ILogger<FindElementsController> _logger;
        private readonly ISessionRepository _sessionRepository;

        public FindElementsController(ILogger<FindElementsController> logger, ISessionRepository sessionRepository)
        {
            _logger = logger;
            _sessionRepository = sessionRepository;
        }

        [HttpPost("element")]
        public async Task<ActionResult> FindElement([FromRoute] string sessionId, [FromBody] FindElementRequest findElementRequest)
        {
            var session = GetActiveSession(sessionId);
            return await FindElementFrom(() => session.App == IntPtr.Zero ? session.ApplicationElement : session.CurrentWindow, findElementRequest, session);
        }

        [HttpPost("element/{elementId}/element")]
        public async Task<ActionResult> FindElementFromElement([FromRoute] string sessionId, [FromRoute] string elementId, [FromBody] FindElementRequest findElementRequest)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);
            return await FindElementFrom(() => element, findElementRequest, session);
        }

        [HttpPost("elements")]
        public async Task<ActionResult> FindElements([FromRoute] string sessionId, [FromBody] FindElementRequest findElementRequest)
        {
            var session = GetActiveSession(sessionId);
            return await FindElementsFrom(() => session.App == IntPtr.Zero ? session.ApplicationElement : session.CurrentWindow, findElementRequest, session);
        }

        [HttpPost("element/{elementId}/elements")]
        public async Task<ActionResult> FindElementsFromElement([FromRoute] string sessionId, [FromRoute] string elementId, [FromBody] FindElementRequest findElementRequest)
        {
            var session = GetActiveSession(sessionId);
            var element = GetElement(session, elementId);
            return await FindElementsFrom(() => element, findElementRequest, session);
        }

        private static async Task<ActionResult> FindElementFrom(Func<IntPtr> startNode, FindElementRequest findElementRequest, Session session)
        {
            IntPtr? element;
            if (findElementRequest.Using == "xpath") 
            { 
                // XPath-like search is not supported natively in macOS, fallback to attribute searching
                element = await Wait.Until(() => FindByXPath(startNode(), findElementRequest.Value), e => e != IntPtr.Zero, session.ImplicitWaitTimeout);
            }
            else 
            { 
                var condition = GetCondition(findElementRequest.Using, findElementRequest.Value);
                element = await Wait.Until(() => FindFirstDescendant(startNode(), condition), e => e != IntPtr.Zero, session.ImplicitWaitTimeout);
            }

            if (element == IntPtr.Zero)
            {
                return NoSuchElement(findElementRequest);
            }

            var knownElement = session.GetOrAddKnownElement(element.Value);
            return await Task.FromResult(WebDriverResult.Success(new FindElementResponse
            {
                ElementReference = knownElement.ElementReference,
            }));
        }

        private static async Task<ActionResult> FindElementsFrom(Func<IntPtr> startNode, FindElementRequest findElementRequest, Session session)
        {
            IntPtr[] elements;
            if (findElementRequest.Using == "xpath")
            {
                elements = await Wait.Until(() => FindAllByXPath(startNode(), findElementRequest.Value), e => e.Length > 0, session.ImplicitWaitTimeout);
            }
            else
            {
                var condition = GetCondition(findElementRequest.Using, findElementRequest.Value);
                elements = await Wait.Until(() => FindAllDescendants(startNode(), condition), e => e.Length > 0, session.ImplicitWaitTimeout);
            }

            var knownElements = elements.Select(session.GetOrAddKnownElement);
            return await Task.FromResult(WebDriverResult.Success(
                knownElements.Select(knownElement => new FindElementResponse()
                {
                    ElementReference = knownElement.ElementReference
                }).ToArray()
            ));
        }

        // Implement FindByXPath logic using AXUIElement if XPath is needed
        private static IntPtr FindByXPath(IntPtr rootElement, string xpath)
        {
            // Placeholder for potential XPath to macOS attribute logic
            return IntPtr.Zero;
        }

        // Implement descendant search based on attributes (AXUIElement)
        private static IntPtr FindFirstDescendant(IntPtr rootElement, string condition)
        {
            // Placeholder for a real descendant search based on macOS AXUIElement attributes
            return IntPtr.Zero;
        }

        private static IntPtr[] FindAllDescendants(IntPtr rootElement, string condition)
        {
            // Placeholder for finding all descendants by attributes
            return new IntPtr[0];
        }

        // Simple attribute-based condition search, no XPath support directly in macOS
        private static string GetCondition(string @using, string value)
        {
            switch (@using)
            {
                case "accessibility id":
                    return $"AXIdentifier={value}";
                case "name":
                    return $"AXTitle={value}";
                case "class name":
                    return $"AXRole={value}";
                case "tag name":
                    return $"AXRole={value}";
                default:
                    throw new NotSupportedException($"Search strategy '{@using}' is not supported.");
            }
        }

        private static ActionResult NoSuchElement(FindElementRequest findElementRequest)
        {
            return WebDriverResult.NotFound(new ErrorResponse()
            {
                ErrorCode = "no such element",
                Message = $"No element found with selector '{findElementRequest.Using}' and value '{findElementRequest.Value}'"
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

        #endregion
    }
}

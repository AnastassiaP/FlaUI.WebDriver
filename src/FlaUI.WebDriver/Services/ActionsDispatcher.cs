using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using FlaUI.WebDriver.Models;
using Microsoft.Extensions.Logging;

namespace FlaUI.WebDriver.Services
{
    public class ActionsDispatcher : IActionsDispatcher
    {
        public enum MouseButton
        {
            Left = 0,
            Middle = 1,
            Right = 2
        }
        private readonly ILogger<ActionsDispatcher> _logger;

        public ActionsDispatcher(ILogger<ActionsDispatcher> logger)
        {
            _logger = logger;
        }

        public async Task DispatchAction(Session session, Action action)
        {
            switch (action.Type)
            {
                case "pointer":
                    await DispatchPointerAction(session, action);
                    return;
                case "key":
                    await DispatchKeyAction(session, action);
                    return;
                case "wheel":
                    await DispatchWheelAction(session, action);
                    return;
                case "none":
                    await DispatchNullAction(session, action);
                    return;
                default:
                    throw WebDriverResponseException.UnsupportedOperation($"Action type {action.Type} not supported");
            }
        }

        public async Task DispatchActionsForString(
            Session session,
            string inputId,
            KeyInputSource source,
            string text)
        {
            var clusters = StringInfo.GetTextElementEnumerator(text);
            var currentTypeableText = new StringBuilder();

            while (clusters.MoveNext())
            {
                var cluster = clusters.GetTextElement();

                if (cluster == Keys.Null.ToString())
                {
                    await DispatchTypeableString(session, inputId, source, currentTypeableText.ToString());
                    currentTypeableText.Clear();
                    await ClearModifierKeyState(session, inputId);
                }
                else if (Keys.IsModifier(Keys.GetNormalizedKeyValue(cluster)))
                {
                    await DispatchTypeableString(session, inputId, source, currentTypeableText.ToString());
                    currentTypeableText.Clear();

                    var keyDownAction = new Action(
                        new ActionSequence 
                        { 
                            Id = inputId,
                            Type = "key" 
                        },
                        new ActionItem
                        {
                            Type = "keyDown",
                            Value = cluster
                        });

                    await DispatchAction(session, keyDownAction);

                    var undo = keyDownAction.Clone();
                    undo.SubType = "keyUp";

                    session.InputState.InputCancelList.Add(undo);
                }
                else if (Keys.IsTypeable(cluster))
                {
                    currentTypeableText.Append(cluster);
                }
                else
                {
                    await DispatchTypeableString(session, inputId, source, currentTypeableText.ToString());
                    currentTypeableText.Clear();
                }
            }

            if (currentTypeableText.Length > 0)
            {
                await DispatchTypeableString(session, inputId, source, currentTypeableText.ToString());
            }

            await ClearModifierKeyState(session, inputId);
        }

        public async Task DispatchReleaseActions(Session session, string inputId)
        {
            for (var i = session.InputState.InputCancelList.Count - 1; i >= 0; i--)
            {
                var cancelAction = session.InputState.InputCancelList[i];

                if (cancelAction.Id == inputId)
                {
                    await DispatchAction(session, cancelAction);
                    session.InputState.InputCancelList.RemoveAt(i);
                }
            }
        }

        private Task ClearModifierKeyState(Session session, string inputId) => DispatchReleaseActions(session, inputId);

        private async Task DispatchTypeableString(
            Session session,
            string inputId,
            KeyInputSource source,
            string text)
        {
            foreach (var c in text)
            {
                var isShifted = Keys.IsShiftedChar(c);

                if (isShifted != source.Shift)
                {
                    var action = new Action(
                        new ActionSequence 
                        { 
                            Id = inputId,
                            Type = "key" 
                        },
                        new ActionItem
                        {
                            Type = source.Shift ? "keyUp" : "keyDown",
                            Value = Keys.LeftShift.ToString(),
                        });
                    await DispatchAction(session, action);
                }

                var keyDownAction = new Action(
                    new ActionSequence 
                    {
                        Id = inputId,
                        Type = "key" 
                    },
                    new ActionItem
                    {
                        Type = "keyDown",
                        Value = c.ToString(),
                    });

                var keyUpAction = keyDownAction.Clone();
                keyUpAction.SubType = "keyUp";

                await DispatchAction(session, keyDownAction);
                await DispatchAction(session, keyUpAction);
            }
        }

        private static async Task DispatchNullAction(Session session, Action action)
        {
            switch (action.SubType)
            {
                case "pause":
                    await Task.Yield();
                    return;
                default:
                    throw WebDriverResponseException.InvalidArgument($"Null action subtype {action.SubType} unknown");
            }
        }

        private async Task DispatchKeyAction(Session session, Action action)
        {
            if (action.Value == null)
            {
                return;
            }

            var source = session.InputState.GetInputSource<KeyInputSource>(action.Id) ?? 
                throw WebDriverResponseException.UnknownError($"Input source for key action '{action.Id}' not found.");

            switch (action.SubType)
            {
                case "keyDown":
                    {
                        var key = Keys.GetNormalizedKeyValue(action.Value);
                        _logger.LogDebug("Dispatching key down action, key '{Value}' with ID '{Id}'", key, action.Id);
                        PressKey(key); // macOS specific key press
                        source.Pressed.Add(action.Value);
                        await Task.Yield();
                        return;
                    }
                case "keyUp":
                    {
                        var key = Keys.GetNormalizedKeyValue(action.Value);
                        _logger.LogDebug("Dispatching key up action, key '{Value}' with ID '{Id}'", key, action.Id);
                        ReleaseKey(key); // macOS specific key release
                        source.Pressed.Remove(action.Value);
                        await Task.Yield();
                        return;
                    }
                case "pause":
                    await Task.Yield();
                    return;
                default:
                    throw WebDriverResponseException.InvalidArgument($"Key action subtype {action.SubType} unknown");
            }
        }

        private async Task DispatchWheelAction(Session session, Action action)
        {
            _logger.LogDebug("Dispatching wheel scroll action, coordinates ({X},{Y}), delta ({DeltaX},{DeltaY}) with ID '{Id}'", action.X, action.Y, action.DeltaX, action.DeltaY, action.Id);
            if (action.X == null || action.Y == null || action.DeltaX == null || action.DeltaY == null)
            {
                throw WebDriverResponseException.InvalidArgument("For wheel scroll, X, Y, delta X, and delta Y are required");
            }

            MouseMoveTo(action.X.Value, action.Y.Value); // macOS specific mouse move
            ScrollWheel(action.DeltaX.Value, action.DeltaY.Value); // macOS specific scroll
            await Task.Yield();
        }

        private async Task DispatchPointerAction(Session session, Action action)
        {
            switch (action.SubType)
            {
                case "pointerMove":
                    _logger.LogDebug("Dispatching pointer move action, coordinates ({X},{Y})", action.X, action.Y);
                    var point = GetCoordinates(session, action);
                    MouseMoveTo(point.X, point.Y); // macOS specific mouse move
                    await Task.Yield();
                    return;
                case "pointerDown":
                    _logger.LogDebug("Dispatching pointer down action, button {Button}", action.Button);
                    MouseDown(GetMouseButton(action.Button)); // macOS specific mouse down
                    await Task.Yield();
                    return;
                case "pointerUp":
                    _logger.LogDebug("Dispatching pointer up action, button {Button}", action.Button);
                    MouseUp(GetMouseButton(action.Button)); // macOS specific mouse up
                    await Task.Yield();
                    return;
                case "pause":
                    await Task.Yield();
                    return;
                default:
                    throw WebDriverResponseException.UnsupportedOperation($"Pointer action subtype {action.SubType} not supported");
            }
        }

        private static System.Drawing.Point GetCoordinates(Session session, Action action)
        {
            if (action.X == null || action.Y == null)
            {
                throw WebDriverResponseException.InvalidArgument("For pointer move, X and Y are required");
            }
            return new Point(action.X.Value, action.Y.Value);
        }

        private static MouseButton GetMouseButton(int? button)
        {
            return button switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                _ => throw WebDriverResponseException.UnsupportedOperation($"Pointer button {button} not supported")
            };
        }

        #region P/Invoke for macOS (using CoreGraphics for keyboard and mouse events)

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CGEventPost(uint tap, IntPtr eventRef);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreateMouseEvent(IntPtr source, CGEventType mouseType, CGPoint mouseCursorPosition, CGMouseButton mouseButton);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreateScrollWheelEvent(IntPtr source, CGScrollEventUnit units, int wheelCount, int delta);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern CGPoint CGEventGetLocation(IntPtr eventRef);

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint
        {
            public double X;
            public double Y;

            public CGPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        private enum CGEventType : uint
        {
            KeyDown = 10,
            KeyUp = 11,
            LeftMouseDown = 1,
            LeftMouseUp = 2,
            RightMouseDown = 3,
            RightMouseUp = 4,
            MouseMoved = 5,
            LeftMouseDragged = 6,
            RightMouseDragged = 7,
            ScrollWheel = 22
        }

        private enum CGMouseButton : uint
        {
            Left = 0,
            Right = 1
        }

        private enum CGScrollEventUnit : uint
        {
            Pixel = 0,
            Line = 1
        }

        private static void PressKey(string key)
        {
            ushort keyCode = GetVirtualKeyCode(key); // This method should map the string key to a macOS key code.
            IntPtr keyDownEvent = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, true);
            CGEventPost(0, keyDownEvent);
            Marshal.Release(keyDownEvent);
        }

        private static void ReleaseKey(string key)
        {
            ushort keyCode = GetVirtualKeyCode(key); // This method should map the string key to a macOS key code.
            IntPtr keyUpEvent = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, false);
            CGEventPost(0, keyUpEvent);
            Marshal.Release(keyUpEvent);
        }

        private static void MouseMoveTo(double x, double y)
        {
            CGPoint newPosition = new CGPoint(x, y);
            IntPtr mouseMoveEvent = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, newPosition, CGMouseButton.Left);
            CGEventPost(0, mouseMoveEvent);
            Marshal.Release(mouseMoveEvent);
        }

        private static void MouseDown(MouseButton button)
        {
            CGPoint currentPosition = GetMousePosition();
            CGMouseButton mouseButton = GetCGMouseButton(button);
            IntPtr mouseDownEvent = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDown, currentPosition, mouseButton);
            CGEventPost(0, mouseDownEvent);
            Marshal.Release(mouseDownEvent);
        }

        private static void MouseUp(MouseButton button)
        {
            CGPoint currentPosition = GetMousePosition();
            CGMouseButton mouseButton = GetCGMouseButton(button);
            IntPtr mouseUpEvent = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseUp, currentPosition, mouseButton);
            CGEventPost(0, mouseUpEvent);
            Marshal.Release(mouseUpEvent);
        }

        private static void ScrollWheel(double deltaX, double deltaY)
        {
            IntPtr scrollEvent = CGEventCreateScrollWheelEvent(IntPtr.Zero, CGScrollEventUnit.Line, 2, (int)deltaY, (int)deltaX);
            CGEventPost(0, scrollEvent);
            Marshal.Release(scrollEvent);
        }

        private static CGPoint GetMousePosition()
        {
            IntPtr eventRef = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, new CGPoint(0, 0), CGMouseButton.Left);
            CGPoint mousePosition = CGEventGetLocation(eventRef);
            Marshal.Release(eventRef);
            return mousePosition;
        }

        private static CGMouseButton GetCGMouseButton(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => CGMouseButton.Left,
                MouseButton.Right => CGMouseButton.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(button), "Unsupported mouse button")
            };
        }

        private static ushort GetVirtualKeyCode(string key)
        {
            return key switch
            {
                "Space" => 0x31, // Example for the space key, more mappings are needed
                _ => throw new ArgumentOutOfRangeException(nameof(key), "Unsupported key")
            };
        }

        #endregion
    }
}

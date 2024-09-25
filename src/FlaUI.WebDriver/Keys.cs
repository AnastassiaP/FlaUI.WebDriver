using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace FlaUI.WebDriver
{
    internal class Keys
    {
        // Normalized key mapping for macOS based on WebDriver spec
        private static readonly Dictionary<char, string> s_normalizedKeys = new Dictionary<char, string>()
        {
            { '\uE000', "Unidentified" },
            { '\uE001', "Cancel" },
            { '\uE002', "Help" },
            { '\uE003', "Backspace" },
            { '\uE004', "Tab" },
            { '\uE005', "Clear" },
            { '\uE006', "Return" },
            { '\uE007', "Enter" },
            { '\uE008', "Shift" },
            { '\uE009', "Control" },
            { '\uE00A', "Alt" },
            { '\uE00B', "Pause" },
            { '\uE00C', "Escape" },
            { '\uE00D', " " },
            { '\uE00E', "PageUp" },
            { '\uE00F', "PageDown" },
            { '\uE010', "End" },
            { '\uE011', "Home" },
            { '\uE012', "ArrowLeft" },
            { '\uE013', "ArrowUp" },
            { '\uE014', "ArrowRight" },
            { '\uE015', "ArrowDown" },
            { '\uE016', "Insert" },
            { '\uE017', "Delete" },
            { '\uE018', ";" },
            { '\uE019', "=" },
            { '\uE01A', "0" },
            { '\uE01B', "1" },
            { '\uE01C', "2" },
            { '\uE01D', "3" },
            { '\uE01E', "4" },
            { '\uE01F', "5" },
            { '\uE020', "6" },
            { '\uE021', "7" },
            { '\uE022', "8" },
            { '\uE023', "9" },
            { '\uE024', "*" },
            { '\uE025', "+" },
            { '\uE026', "," },
            { '\uE027', "-" },
            { '\uE028', "." },
            { '\uE029', "/" },
            { '\uE031', "F1" },
            { '\uE032', "F2" },
            { '\uE033', "F3" },
            { '\uE034', "F4" },
            { '\uE035', "F5" },
            { '\uE036', "F6" },
            { '\uE037', "F7" },
            { '\uE038', "F8" },
            { '\uE039', "F9" },
            { '\uE03A', "F10" },
            { '\uE03B', "F11" },
            { '\uE03C', "F12" },
            { '\uE03D', "Meta" },
            { '\uE03E', "Command" },
            { '\uE040', "ZenkakuHankaku" },
        };

        // macOS key codes mapping
        private static readonly Dictionary<char, ushort> s_keyToMacOSCode = new()
        {
            { 'a', 0x00 }, // kVK_ANSI_A
            { 'b', 0x0B }, // kVK_ANSI_B
            { 'c', 0x08 }, // kVK_ANSI_C
            { 'd', 0x02 }, // kVK_ANSI_D
            { 'e', 0x0E }, // kVK_ANSI_E
            { 'f', 0x03 }, // kVK_ANSI_F
            { 'g', 0x05 }, // kVK_ANSI_G
            { 'h', 0x04 }, // kVK_ANSI_H
            { 'i', 0x22 }, // kVK_ANSI_I
            { 'j', 0x26 }, // kVK_ANSI_J
            { 'k', 0x28 }, // kVK_ANSI_K
            { 'l', 0x25 }, // kVK_ANSI_L
            { 'm', 0x2E }, // kVK_ANSI_M
            { 'n', 0x2D }, // kVK_ANSI_N
            { 'o', 0x1F }, // kVK_ANSI_O
            { 'p', 0x23 }, // kVK_ANSI_P
            { 'q', 0x0C }, // kVK_ANSI_Q
            { 'r', 0x0F }, // kVK_ANSI_R
            { 's', 0x01 }, // kVK_ANSI_S
            { 't', 0x11 }, // kVK_ANSI_T
            { 'u', 0x20 }, // kVK_ANSI_U
            { 'v', 0x09 }, // kVK_ANSI_V
            { 'w', 0x0D }, // kVK_ANSI_W
            { 'x', 0x07 }, // kVK_ANSI_X
            { 'y', 0x10 }, // kVK_ANSI_Y
            { 'z', 0x06 }, // kVK_ANSI_Z
            { '1', 0x12 }, // kVK_ANSI_1
            { '2', 0x13 }, // kVK_ANSI_2
            { '3', 0x14 }, // kVK_ANSI_3
            { '4', 0x15 }, // kVK_ANSI_4
            { '5', 0x17 }, // kVK_ANSI_5
            { '6', 0x16 }, // kVK_ANSI_6
            { '7', 0x1A }, // kVK_ANSI_7
            { '8', 0x1C }, // kVK_ANSI_8
            { '9', 0x19 }, // kVK_ANSI_9
            { '0', 0x1D }, // kVK_ANSI_0
            { '\n', 0x24 }, // kVK_Return
            { '\b', 0x33 }, // kVK_Delete
            { '\t', 0x30 }, // kVK_Tab
            { ' ', 0x31 }, // kVK_Space
            { '-', 0x1B }, // kVK_ANSI_Minus
            { '=', 0x18 }, // kVK_ANSI_Equal
            { '[', 0x21 }, // kVK_ANSI_LeftBracket
            { ']', 0x1E }, // kVK_ANSI_RightBracket
            { '\\', 0x2A }, // kVK_ANSI_Backslash
            { ';', 0x29 }, // kVK_ANSI_Semicolon
            { '\'', 0x27 }, // kVK_ANSI_Quote
            { ',', 0x2B }, // kVK_ANSI_Comma
            { '.', 0x2F }, // kVK_ANSI_Period
            { '/', 0x2C }, // kVK_ANSI_Slash
            { '`', 0x32 }, // kVK_ANSI_Grave
        };

        public const char Null = '\uE000';
        public const char Cancel = '\uE001';
        public const char Help = '\uE002';
        public const char Backspace = '\uE003';
        public const char Tab = '\uE004';
        public const char Clear = '\uE005';
        public const char Return = '\uE006';
        public const char Enter = '\uE007';
        public const char Shift = '\uE008';
        public const char LeftShift = '\uE008';
        public const char Control = '\uE009';
        public const char LeftControl = '\uE009';
        public const char Alt = '\uE00A';
        public const char LeftAlt = '\uE00A';
        public const char Pause = '\uE00B';
        public const char Escape = '\uE00C';
        public const char Space = '\uE00D';
        public const char PageUp = '\uE00E';
        public const char PageDown = '\uE00F';
        public const char End = '\uE010';
        public const char Home = '\uE011';
        public const char Left = '\uE012';
        public const char ArrowLeft = '\uE012';
        public const char Up = '\uE013';
        public const char ArrowUp = '\uE013';
        public const char Right = '\uE014';
        public const char ArrowRight = '\uE014';
        public const char Down = '\uE015';
        public const char ArrowDown = '\uE015';
        public const char Insert = '\uE016';
        public const char Delete = '\uE017';
        public const char Semicolon = '\uE018';
        public const char Equal = '\uE019';
        public const char NumberPad0 = '\uE01A';
        public const char NumberPad1 = '\uE01B';
        public const char NumberPad2 = '\uE01C';
        public const char NumberPad3 = '\uE01D';
        public const char NumberPad4 = '\uE01E';
        public const char NumberPad5 = '\uE01F';
        public const char NumberPad6 = '\uE020';
        public const char NumberPad7 = '\uE021';
        public const char NumberPad8 = '\uE022';
        public const char NumberPad9 = '\uE023';
        public const char Multiply = '\uE024';
        public const char Add = '\uE025';
        public const char Separator = '\uE026';
        public const char Subtract = '\uE027';
        public const char Decimal = '\uE028';
        public const char Divide = '\uE029';
        public const char F1 = '\uE031';
        public const char F2 = '\uE032';
        public const char F3 = '\uE033';
        public const char F4 = '\uE034';
        public const char F5 = '\uE035';
        public const char F6 = '\uE036';
        public const char F7 = '\uE037';
        public const char F8 = '\uE038';
        public const char F9 = '\uE039';
        public const char F10 = '\uE03A';
        public const char F11 = '\uE03B';
        public const char F12 = '\uE03C';
        public const char Meta = '\uE03D';
        public const char Command = '\uE03D';
        public const char ZenkakuHankaku = '\uE040';

        /// <summary>
        /// Gets a value indicating whether a key attribute value represents a modifier key.
        /// </summary>
        public static bool IsModifier(string key)
        {
            return key is "Alt" or "AltGraph" or "CapsLock" or "Control" or "Fn" or "FnLock" or
                   "Meta" or "NumLock" or "ScrollLock" or "Shift" or "Symbol" or "SymbolLock";
        }

        /// <summary>
        /// Gets a value indicating whether a character is shifted.
        /// </summary>
        internal static bool IsShiftedChar(char c) => char.IsUpper(c) || s_shiftedKeyToCode.ContainsKey(c);

        /// <summary>
        /// Gets a value indicating whether a graphene cluster is typeable.
        /// </summary>
        public static bool IsTypeable(string c)
        {
            return c.Length == 1 && (s_keyToMacOSCode.ContainsKey(c[0]) || s_shiftedKeyToCode.ContainsKey(c[0]));
        }

        /// <summary>
        /// Gets the macOS key code for a raw key.
        /// </summary>
        public static ushort? GetMacOSKeyCode(string key)
        {
            if (key.Length == 1)
            {
                var c = key[0];
                if (s_keyToMacOSCode.TryGetValue(c, out var code))
                {
                    return code;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a normalized key value for macOS.
        /// </summary>
        public static string GetNormalizedKeyValue(string key)
        {
            return key.Length == 1 && s_normalizedKeys.TryGetValue(key[0], out var value) ? value : key;
        }
    }
}

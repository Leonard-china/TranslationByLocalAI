using System;
using System.Runtime.InteropServices;

namespace TranslationByLocalAI
{
    internal static class NativeMethods
    {
        internal const int WH_MOUSE_LL = 14;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_LBUTTONUP = 0x0202;
        internal const uint INPUT_KEYBOARD = 1;
        internal const uint KEYEVENTF_KEYUP = 0x0002;
        internal const ushort VK_CONTROL = 0x11;
        internal const ushort VK_C = 0x43;

        internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSLLHOOKSTRUCT
        {
            public POINT Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint Type;
            public InputUnion Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;

            // INPUT is a tagged union. Including MOUSEINPUT here is required
            // so Marshal.SizeOf(INPUT) matches Win32's expected size on x64.
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int hookId,
            LowLevelMouseProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        internal static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessDPIAware();

        internal static uint SendCtrlC()
        {
            var inputs = new[]
            {
                CreateKeyInput(VK_CONTROL, 0),
                CreateKeyInput(VK_C, 0),
                CreateKeyInput(VK_C, KEYEVENTF_KEYUP),
                CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP)
            };
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateKeyInput(ushort key, uint flags)
        {
            var input = new INPUT();
            input.Type = INPUT_KEYBOARD;
            input.Union.Keyboard = new KEYBDINPUT
            {
                VirtualKey = key,
                ScanCode = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            };
            return input;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Hitote
{
    internal sealed class KeyboardService : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
        private const int WM_KEYUP = 0x101, WM_SYSKEYUP = 0x105;
        private const uint INJECTED = 0x10, KEYUP = 2;
        private static readonly UIntPtr Marker = new UIntPtr(0x4849544F);
        private readonly HookProc keyboardCallback, mouseCallback;
        private readonly HashSet<Keys> pressed = new HashSet<Keys>();
        private readonly HashSet<Keys> suppressed = new HashSet<Keys>();
        private readonly uint processId = (uint)Process.GetCurrentProcess().Id;
        private IntPtr keyboardHook, mouseHook, target;
        public readonly InputEngine Engine = new InputEngine();
        public bool Enabled { get; private set; }
        public string Error { get; private set; }

        public KeyboardService()
        {
            keyboardCallback = KeyboardHook;
            mouseCallback = MouseHook;
            IntPtr module = GetModuleHandle(null);
            keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardCallback, module, 0);
            if (keyboardHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseCallback, module, 0);
            if (mouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Dispose();
                throw new Win32Exception(error);
            }
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            Engine.Reset();
            target = GetForegroundWindow();
            Error = null;
        }

        public void CheckForeground()
        {
            IntPtr current = GetForegroundWindow();
            if (current == target) return;
            target = current;
            Engine.Reset();
        }

        private IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
        {
            if (code < 0) return CallNextHookEx(keyboardHook, code, message, data);
            var info = (KeyboardData)Marshal.PtrToStructure(data, typeof(KeyboardData));
            if (info.ExtraInfo == Marker) return CallNextHookEx(keyboardHook, code, message, data);
            if ((info.Flags & INJECTED) != 0)
            {
                Engine.Reset();
                return CallNextHookEx(keyboardHook, code, message, data);
            }
            int type = message.ToInt32();
            Keys key = (Keys)info.VirtualKey;
            if (type == WM_KEYUP || type == WM_SYSKEYUP)
            {
                pressed.Remove(key);
                if (suppressed.Remove(key)) return new IntPtr(1);
                return CallNextHookEx(keyboardHook, code, message, data);
            }
            if (type != WM_KEYDOWN && type != WM_SYSKEYDOWN) return CallNextHookEx(keyboardHook, code, message, data);

            bool repeat = !pressed.Add(key);
            try
            {
                CheckForeground();
                if (HasModifiers() || IsModifier(key))
                {
                    Engine.Reset();
                    if (suppressed.Contains(key)) return new IntPtr(1);
                    return CallNextHookEx(keyboardHook, code, message, data);
                }
                if (key == Keys.F2)
                {
                    if (!repeat) SetEnabled(!Enabled);
                    suppressed.Add(key);
                    return new IntPtr(1);
                }
                if (!Enabled)
                {
                    if (suppressed.Contains(key)) return new IntPtr(1);
                    return CallNextHookEx(keyboardHook, code, message, data);
                }
                uint owner;
                GetWindowThreadProcessId(target, out owner);
                if (target == IntPtr.Zero || owner == processId)
                {
                    Engine.Reset();
                    if (suppressed.Contains(key)) return new IntPtr(1);
                    return CallNextHookEx(keyboardHook, code, message, data);
                }

                // Don't turn key auto-repeat into unintended row/vowel combinations.
                if (repeat && key != Keys.R && key != Keys.Back)
                {
                    if (suppressed.Contains(key)) return new IntPtr(1);
                    return CallNextHookEx(keyboardHook, code, message, data);
                }
                InputResult result = Engine.Process(key);
                if (!result.Handled) return CallNextHookEx(keyboardHook, code, message, data);
                suppressed.Add(key);
                if (result.Outputs.Count > 0) Send(result.Outputs);
                return new IntPtr(1);
            }
            catch (Exception exception)
            {
                Enabled = false;
                Engine.Reset();
                Error = "送信できなかったためOFFにしました：" + exception.Message;
                if (suppressed.Contains(key)) return new IntPtr(1);
                return CallNextHookEx(keyboardHook, code, message, data);
            }
        }

        private IntPtr MouseHook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int type = message.ToInt32();
                // A click or wheel can move the caret without changing the foreground app.
                if (type == 0x201 || type == 0x204 || type == 0x207 || type == 0x20B || type == 0x20A || type == 0x20E)
                    Engine.Reset();
            }
            return CallNextHookEx(mouseHook, code, message, data);
        }

        private void Send(List<Output> outputs)
        {
            uint owner;
            uint thread = GetWindowThreadProcessId(target, out owner);
            IntPtr layout = GetKeyboardLayout(thread);
            var inputs = new List<NativeInput>();
            foreach (Output output in outputs)
            {
                if (output.Text == null) AddKey(inputs, (ushort)output.Key);
                else foreach (char character in output.Text)
                {
                    short mapping = VkKeyScanEx(character, layout);
                    if (mapping == -1) throw new InvalidOperationException("このキーボード配列では入力できない文字です。");
                    int shift = (mapping >> 8) & 0xff;
                    if ((shift & ~1) != 0) throw new InvalidOperationException("このキーボード配列には未対応です。");
                    if ((shift & 1) != 0) inputs.Add(KeyInput((ushort)Keys.ShiftKey, false));
                    AddKey(inputs, (ushort)(mapping & 0xff));
                    if ((shift & 1) != 0) inputs.Add(KeyInput((ushort)Keys.ShiftKey, true));
                }
            }
            if (GetForegroundWindow() != target) { Engine.Reset(); return; }
            NativeInput[] batch = inputs.ToArray();
            uint sent = SendInput((uint)batch.Length, batch, Marshal.SizeOf(typeof(NativeInput)));
            if (sent != batch.Length) throw new InvalidOperationException("入力先がキー送信を受け付けませんでした。");
        }

        private static NativeInput KeyInput(ushort key, bool up)
        {
            return new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput {
                VirtualKey = key, Flags = up ? KEYUP : 0, ExtraInfo = Marker
            } } };
        }
        private static void AddKey(List<NativeInput> list, ushort key) { list.Add(KeyInput(key, false)); list.Add(KeyInput(key, true)); }
        private static bool IsModifier(Keys key)
        {
            return key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.ControlKey ||
                key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.Menu || key == Keys.LMenu ||
                key == Keys.RMenu || key == Keys.LWin || key == Keys.RWin;
        }
        private static bool HasModifiers()
        {
            foreach (Keys key in new[] { Keys.ShiftKey, Keys.ControlKey, Keys.Menu, Keys.LWin, Keys.RWin })
                if ((GetAsyncKeyState((int)key) & 0x8000) != 0) return true;
            return false;
        }

        public void Dispose()
        {
            if (keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(keyboardHook); keyboardHook = IntPtr.Zero; }
            if (mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint VirtualKey, ScanCode, Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, ScanCode; public uint Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanEx(char character, IntPtr layout);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    }
}

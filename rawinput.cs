using System.Runtime.InteropServices;

namespace MouseTester.Raw
{
    /// <summary>Low-level RAWINPUT wrapper that also arrives when the
    /// application does NOT own the foreground window (RIDEV_INPUTSINK).</summary>
    internal sealed class RawMouseEventArgs : EventArgs
    {
        public short  Dx          { get; }
        public short  Dy          { get; }
        public ushort ButtonFlags { get; }
        public short  WheelDelta  { get; }
        public long   Timestamp   { get; }

        internal RawMouseEventArgs(short dx, short dy,
                                   ushort flags, short wheel, long ts)
        {
            Dx = dx; Dy = dy; ButtonFlags = flags; WheelDelta = wheel; Timestamp = ts;
        }
    }

    internal sealed class RawInputListener : NativeWindow, IDisposable
    {
        public event EventHandler<RawMouseEventArgs>? MouseInput;

        private const int  WM_INPUT       = 0x00FF;
        private const uint RID_INPUT      = 0x10000003;
        private const uint RIM_TYPEMOUSE  = 0;
        private const uint RIDEV_INPUTSINK = 0x00000100;

        private const ushort RI_MOUSE_WHEEL = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint   dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint   dwType;
            public uint   dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(4)]  public ushort usButtonFlags;
            [FieldOffset(6)]  public ushort usButtonData;
            [FieldOffset(12)] public int    lLastX;
            [FieldOffset(16)] public int    lLastY;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUT
        {
            [FieldOffset(0)]  public RAWINPUTHEADER header;
            [FieldOffset(24)] public RAWMOUSE       mouse;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput, uint uiCommand, IntPtr pData,
            ref uint pcbSize, uint cbSizeHeader);

        public RawInputListener(IntPtr handle)
        {
            AssignHandle(handle);

            var rid = new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,      // Generic desktop
                    usUsage     = 0x02,      // Mouse
                    dwFlags     = RIDEV_INPUTSINK,   // global stream
                    hwndTarget  = handle
                }
            };
            if (!RegisterRawInputDevices(rid, 1,
                    (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                throw new InvalidOperationException("RAWINPUT registration failed.");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT)
            {
                uint size = 0;
                GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero,
                                ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

                var buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (GetRawInputData(m.LParam, RID_INPUT, buffer,
                                        ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == size)
                    {
                        var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                        if (raw.header.dwType == RIM_TYPEMOUSE)
                        {
                            short wheel = 0;
                            if ((raw.mouse.usButtonFlags & RI_MOUSE_WHEEL) != 0)
                                wheel = unchecked((short)raw.mouse.usButtonData);

                            MouseInput?.Invoke(this,
                                new RawMouseEventArgs(
                                    (short)raw.mouse.lLastX,
                                    (short)raw.mouse.lLastY,
                                    raw.mouse.usButtonFlags,
                                    wheel,
                                    Environment.TickCount64));
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            base.WndProc(ref m);
        }

        public void Dispose() => ReleaseHandle();
    }
}

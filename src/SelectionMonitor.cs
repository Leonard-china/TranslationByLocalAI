using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class SelectionDetectedEventArgs : EventArgs
    {
        internal SelectionDetectedEventArgs(Point cursorPosition)
        {
            CursorPosition = cursorPosition;
        }

        internal Point CursorPosition { get; private set; }
    }

    internal sealed class SelectionMonitor : IDisposable
    {
        private const int ClipboardTimeoutMilliseconds = 500;
        private const int MaximumSelectionLength = 12000;

        private readonly Control _dispatcher;
        private readonly NativeMethods.LowLevelMouseProc _mouseProc;
        private readonly System.Windows.Forms.Timer _selectionTimer;
        private readonly uint _processId;
        private readonly int _dragThresholdX;
        private readonly int _dragThresholdY;
        private readonly int _doubleClickTime;
        private readonly int _doubleClickWidth;
        private readonly int _doubleClickHeight;
        private IntPtr _hook;
        private Point _mouseDownPoint;
        private int _lastClickTick;
        private Point _lastClickPoint;
        private bool _trackingExternalClick;
        private IntPtr _selectionSourceWindow;
        private Point _selectionPoint;
        private int _selectionGeneration;
        private int _captureGeneration;
        private bool _disposed;

        internal event EventHandler<SelectionDetectedEventArgs> SelectionDetected;
        internal event EventHandler SelectionCanceled;
        internal bool Enabled { get; set; }

        internal SelectionMonitor(Control dispatcher)
        {
            _dispatcher = dispatcher;
            _mouseProc = MouseHookCallback;
            _processId = (uint)Process.GetCurrentProcess().Id;
            _dragThresholdX = Math.Max(3, SystemInformation.DragSize.Width / 2);
            _dragThresholdY = Math.Max(3, SystemInformation.DragSize.Height / 2);
            _doubleClickTime = SystemInformation.DoubleClickTime;
            _doubleClickWidth = SystemInformation.DoubleClickSize.Width;
            _doubleClickHeight = SystemInformation.DoubleClickSize.Height;
            _selectionTimer = new System.Windows.Forms.Timer();
            _selectionTimer.Interval = 110;
            _selectionTimer.Tick += SelectionTimerTick;
            Enabled = true;
        }

        internal void Start()
        {
            if (_hook != IntPtr.Zero)
            {
                return;
            }

            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _mouseProc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hook == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "无法安装全局鼠标监听。");
            }
            AppLogger.Write("Global mouse hook installed.");
        }

        private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && Enabled)
                {
                    var data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                        lParam,
                        typeof(NativeMethods.MSLLHOOKSTRUCT));
                    var point = new Point(data.Point.X, data.Point.Y);
                    var message = wParam.ToInt32();

                    if (message == NativeMethods.WM_LBUTTONDOWN)
                    {
                        HandleLeftButtonDown(data.Point, point);
                    }
                    else if (message == NativeMethods.WM_LBUTTONUP)
                    {
                        HandleLeftButtonUp(point);
                    }
                }
            }
            catch
            {
                // A global hook must always return immediately. Diagnostics,
                // clipboard access and UI work are deliberately kept elsewhere.
            }

            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        private void HandleLeftButtonDown(NativeMethods.POINT nativePoint, Point point)
        {
            var clickedWindow = NativeMethods.WindowFromPoint(nativePoint);
            if (IsOwnWindow(clickedWindow))
            {
                _trackingExternalClick = false;
                return;
            }

            _trackingExternalClick = true;
            _mouseDownPoint = point;
            Interlocked.Increment(ref _captureGeneration);
            ScheduleSelectionCanceled();
        }

        private void HandleLeftButtonUp(Point point)
        {
            if (!_trackingExternalClick)
            {
                return;
            }
            _trackingExternalClick = false;

            var now = Environment.TickCount;
            var isDrag = Math.Abs(point.X - _mouseDownPoint.X) >= _dragThresholdX
                || Math.Abs(point.Y - _mouseDownPoint.Y) >= _dragThresholdY;
            var elapsed = unchecked(now - _lastClickTick);
            var isDoubleClick = elapsed >= 0
                && elapsed <= _doubleClickTime
                && Math.Abs(point.X - _lastClickPoint.X) <= _doubleClickWidth
                && Math.Abs(point.Y - _lastClickPoint.Y) <= _doubleClickHeight;

            _lastClickTick = now;
            _lastClickPoint = point;
            if (!isDrag && !isDoubleClick)
            {
                return;
            }

            var sourceWindow = NativeMethods.GetForegroundWindow();
            if (sourceWindow == IntPtr.Zero || IsOwnWindow(sourceWindow))
            {
                return;
            }

            _selectionSourceWindow = sourceWindow;
            _selectionPoint = point;
            _selectionGeneration = Volatile.Read(ref _captureGeneration);
            ScheduleSelectionDetected(_selectionGeneration);
        }

        private void ScheduleSelectionCanceled()
        {
            if (_dispatcher.IsDisposed)
            {
                return;
            }

            try
            {
                _dispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    _selectionTimer.Stop();
                    var handler = SelectionCanceled;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ScheduleSelectionDetected(int generation)
        {
            if (_dispatcher.IsDisposed)
            {
                return;
            }

            try
            {
                _dispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsCurrentCapture(generation))
                    {
                        return;
                    }
                    _selectionTimer.Stop();
                    _selectionTimer.Start();
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void SelectionTimerTick(object sender, EventArgs e)
        {
            _selectionTimer.Stop();
            var generation = Volatile.Read(ref _selectionGeneration);
            if (!Enabled || !IsCurrentCapture(generation))
            {
                return;
            }

            var handler = SelectionDetected;
            if (handler != null)
            {
                AppLogger.Write("Selection gesture detected; showing translation button.");
                handler(this, new SelectionDetectedEventArgs(_selectionPoint));
            }
        }

        internal Task<string> CaptureSelectionAsync()
        {
            var generation = Volatile.Read(ref _selectionGeneration);
            if (_disposed
                || !Enabled
                || generation != Volatile.Read(ref _captureGeneration))
            {
                return Task.FromResult<string>(null);
            }

            var sourceWindow = _selectionSourceWindow;
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = new Thread((ThreadStart)delegate
            {
                try
                {
                    completion.TrySetResult(
                        CaptureSelectionOnWorker(generation, sourceWindow));
                }
                catch (Exception ex)
                {
                    AppLogger.Write("Selection capture failed: " + ex);
                    completion.TrySetResult(null);
                }
            });
            worker.IsBackground = true;
            worker.Name = "TranslationByLocalAI.SelectionCapture";
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            return completion.Task;
        }

        private string CaptureSelectionOnWorker(int generation, IntPtr sourceWindow)
        {
            if (!IsCurrentCapture(generation)
                || sourceWindow == IntPtr.Zero
                || NativeMethods.GetForegroundWindow() != sourceWindow)
            {
                AppLogger.Write(
                    "Selection capture skipped because the source window is no longer active.");
                return null;
            }

            var text = CaptureSelectionFromForeground(
                sourceWindow,
                delegate { return IsCurrentCapture(generation); });
            if (string.IsNullOrWhiteSpace(text))
            {
                if (IsCurrentCapture(generation))
                {
                    AppLogger.Write("Selection capture returned no text.");
                }
                return null;
            }

            text = text.Trim();
            if (text.Length > MaximumSelectionLength)
            {
                text = text.Substring(0, MaximumSelectionLength);
            }
            AppLogger.Write("Selection captured on request, length=" + text.Length + ".");
            return IsCurrentCapture(generation) ? text : null;
        }

        private bool IsCurrentCapture(int generation)
        {
            return !_disposed
                && generation == Volatile.Read(ref _captureGeneration);
        }

        private static string CaptureSelectionFromForeground(
            IntPtr sourceWindow,
            Func<bool> isCurrent)
        {
            if (!isCurrent()
                || NativeMethods.GetForegroundWindow() != sourceWindow)
            {
                return null;
            }

            var originalSequence = NativeMethods.GetClipboardSequenceNumber();
            var sentInputs = NativeMethods.SendCtrlC();
            AppLogger.Write(
                "Copy shortcut sent, inputs="
                + sentInputs
                + ", nativeInputSize="
                + Marshal.SizeOf(typeof(NativeMethods.INPUT))
                + ".");

            if (sentInputs == 0)
            {
                return null;
            }

            return WaitForClipboardText(
                originalSequence,
                ClipboardTimeoutMilliseconds,
                isCurrent);
        }

        private static string WaitForClipboardText(
            uint originalSequence,
            int timeoutMilliseconds,
            Func<bool> isCurrent)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (!isCurrent())
                {
                    return null;
                }

                Thread.Sleep(15);
                if (NativeMethods.GetClipboardSequenceNumber() == originalSequence)
                {
                    continue;
                }

                string selected;
                if (TryGetClipboardText(out selected))
                {
                    return selected;
                }
            }
            return null;
        }

        private static bool TryGetClipboardText(out string text)
        {
            text = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (!NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    Thread.Sleep(10);
                    continue;
                }

                IntPtr memory = IntPtr.Zero;
                IntPtr pointer = IntPtr.Zero;
                try
                {
                    if (!NativeMethods.IsClipboardFormatAvailable(
                        NativeMethods.CF_UNICODETEXT))
                    {
                        return false;
                    }

                    memory = NativeMethods.GetClipboardData(
                        NativeMethods.CF_UNICODETEXT);
                    if (memory == IntPtr.Zero)
                    {
                        return false;
                    }

                    pointer = NativeMethods.GlobalLock(memory);
                    if (pointer == IntPtr.Zero)
                    {
                        return false;
                    }

                    var byteCount = NativeMethods.GlobalSize(memory).ToUInt64();
                    var characterCount = (int)Math.Min(
                        byteCount / sizeof(char),
                        (ulong)MaximumSelectionLength + 1UL);
                    if (characterCount <= 0)
                    {
                        return false;
                    }

                    text = Marshal.PtrToStringUni(pointer, characterCount);
                    if (text == null)
                    {
                        return false;
                    }

                    var terminator = text.IndexOf('\0');
                    if (terminator >= 0)
                    {
                        text = text.Substring(0, terminator);
                    }
                    return true;
                }
                finally
                {
                    if (pointer != IntPtr.Zero)
                    {
                        NativeMethods.GlobalUnlock(memory);
                    }
                    NativeMethods.CloseClipboard();
                }
            }
            return false;
        }

        private bool IsOwnWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }
            uint processId;
            NativeMethods.GetWindowThreadProcessId(window, out processId);
            return processId == _processId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Interlocked.Increment(ref _captureGeneration);
            _selectionTimer.Dispose();
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}

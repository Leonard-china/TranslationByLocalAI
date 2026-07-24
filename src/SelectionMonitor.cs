using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;

namespace TranslationByLocalAI
{
    internal sealed class TextSelectedEventArgs : EventArgs
    {
        internal TextSelectedEventArgs(string text, Point cursorPosition)
        {
            Text = text;
            CursorPosition = cursorPosition;
        }

        internal string Text { get; private set; }
        internal Point CursorPosition { get; private set; }
    }

    internal sealed class SelectionMonitor : IDisposable
    {
        private readonly Control _dispatcher;
        private readonly FloatingButtonForm _button;
        private readonly DesktopWidgetForm _desktopWidget;
        private readonly NativeMethods.LowLevelMouseProc _mouseProc;
        private readonly System.Windows.Forms.Timer _captureTimer;
        private IntPtr _hook;
        private Point _mouseDownPoint;
        private Point _mouseUpPoint;
        private IntPtr _sourceWindow;
        private int _lastClickTick;
        private Point _lastClickPoint;
        private int _captureGeneration;
        private bool _clickingButton;
        private bool _disposed;

        internal event EventHandler<TextSelectedEventArgs> TextSelected;
        internal bool Enabled { get; set; }

        internal SelectionMonitor(
            Control dispatcher,
            FloatingButtonForm button,
            DesktopWidgetForm desktopWidget)
        {
            _dispatcher = dispatcher;
            _button = button;
            _desktopWidget = desktopWidget;
            _mouseProc = MouseHookCallback;
            _captureTimer = new System.Windows.Forms.Timer();
            _captureTimer.Interval = 110;
            _captureTimer.Tick += CaptureTimerTick;
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
            if (code >= 0 && Enabled)
            {
                var data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                    lParam,
                    typeof(NativeMethods.MSLLHOOKSTRUCT));
                var point = new Point(data.Point.X, data.Point.Y);
                var message = wParam.ToInt32();

                if (message == NativeMethods.WM_LBUTTONDOWN)
                {
                    if (_desktopWidget.Visible && _desktopWidget.Bounds.Contains(point))
                    {
                        _clickingButton = true;
                        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
                    }
                    if (_button.Visible)
                    {
                        if (_button.Bounds.Contains(point))
                        {
                            _clickingButton = true;
                            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
                        }
                        _button.Hide();
                    }
                    _clickingButton = false;
                    _mouseDownPoint = point;
                    _sourceWindow = NativeMethods.GetForegroundWindow();
                }
                else if (message == NativeMethods.WM_LBUTTONUP)
                {
                    if (_clickingButton)
                    {
                        _clickingButton = false;
                        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
                    }

                    _mouseUpPoint = point;
                    var now = Environment.TickCount;
                    var isDrag = Math.Abs(point.X - _mouseDownPoint.X) >= Math.Max(3, SystemInformation.DragSize.Width / 2)
                        || Math.Abs(point.Y - _mouseDownPoint.Y) >= Math.Max(3, SystemInformation.DragSize.Height / 2);
                    var elapsed = unchecked(now - _lastClickTick);
                    var isDoubleClick = elapsed >= 0
                        && elapsed <= SystemInformation.DoubleClickTime
                        && Math.Abs(point.X - _lastClickPoint.X) <= SystemInformation.DoubleClickSize.Width
                        && Math.Abs(point.Y - _lastClickPoint.Y) <= SystemInformation.DoubleClickSize.Height;

                    _lastClickTick = now;
                    _lastClickPoint = point;

                    if ((isDrag || isDoubleClick) && !IsOwnWindow(_sourceWindow))
                    {
                        AppLogger.Write(isDrag ? "Mouse drag detected." : "Mouse double-click detected.");
                        ScheduleCapture();
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        private void ScheduleCapture()
        {
            if (_dispatcher.IsDisposed)
            {
                return;
            }

            Interlocked.Increment(ref _captureGeneration);
            try
            {
                _dispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    _captureTimer.Stop();
                    _captureTimer.Start();
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void CaptureTimerTick(object sender, EventArgs e)
        {
            _captureTimer.Stop();
            if (!Enabled || IsOwnWindow(_sourceWindow))
            {
                return;
            }

            var generation = Volatile.Read(ref _captureGeneration);
            var sourceWindow = _sourceWindow;
            var mouseUpPoint = _mouseUpPoint;
            var worker = new Thread((ThreadStart)delegate
            {
                CaptureSelectionOnWorker(generation, sourceWindow, mouseUpPoint);
            });
            worker.IsBackground = true;
            worker.Name = "TranslationByLocalAI.SelectionCapture";
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private void CaptureSelectionOnWorker(
            int generation,
            IntPtr sourceWindow,
            Point mouseUpPoint)
        {
            var text = TryCaptureSelectionViaAutomation(sourceWindow, mouseUpPoint);
            if (!string.IsNullOrWhiteSpace(text))
            {
                AppLogger.Write("Selection read through UI Automation.");
            }
            else if (IsCurrentCapture(generation))
            {
                text = CaptureSelectionFromForeground(
                    sourceWindow,
                    delegate { return IsCurrentCapture(generation); });
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                if (IsCurrentCapture(generation))
                {
                    AppLogger.Write("Selection capture returned no text.");
                }
                return;
            }

            text = text.Trim();
            if (text.Length > 12000)
            {
                text = text.Substring(0, 12000);
            }

            if (!IsCurrentCapture(generation) || _dispatcher.IsDisposed)
            {
                return;
            }

            try
            {
                _dispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsCurrentCapture(generation) || !Enabled)
                    {
                        return;
                    }

                    var handler = TextSelected;
                    if (handler != null)
                    {
                        AppLogger.Write("Selection captured, length=" + text.Length + ".");
                        handler(this, new TextSelectedEventArgs(text, mouseUpPoint));
                    }
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private bool IsCurrentCapture(int generation)
        {
            return !_disposed && generation == Volatile.Read(ref _captureGeneration);
        }

        private static string CaptureSelectionFromForeground(
            IntPtr sourceWindow,
            Func<bool> isCurrent)
        {
            IDataObject original = null;
            string originalText = null;
            try
            {
                if (!isCurrent())
                {
                    return null;
                }

                original = TryGetClipboardDataObject();
                if (original != null && original.GetDataPresent(DataFormats.UnicodeText))
                {
                    originalText = original.GetData(DataFormats.UnicodeText) as string;
                }

                if (!TryClearClipboard())
                {
                    AppLogger.Write("Clipboard could not be cleared.");
                    return null;
                }

                if (!isCurrent())
                {
                    return null;
                }

                var sequence = NativeMethods.GetClipboardSequenceNumber();
                NativeMethods.SetForegroundWindow(sourceWindow);
                Thread.Sleep(35);
                var sentInputs = NativeMethods.SendCtrlC();
                AppLogger.Write(
                    "Copy shortcut sent, inputs="
                    + sentInputs
                    + ", nativeInputSize="
                    + Marshal.SizeOf(typeof(NativeMethods.INPUT))
                    + ".");

                var selected = WaitForClipboardText(650, isCurrent);
                if (selected != null)
                {
                    AppLogger.Write(
                        "Clipboard selection captured after SendInput, sequenceChanged="
                        + (NativeMethods.GetClipboardSequenceNumber() != sequence)
                        + ".");
                    return selected;
                }

                if (!isCurrent())
                {
                    return null;
                }

                try
                {
                    NativeMethods.SetForegroundWindow(sourceWindow);
                    SendKeys.SendWait("^c");
                    AppLogger.Write("Copy shortcut retried with SendKeys.");
                }
                catch (Exception ex)
                {
                    AppLogger.Write("SendKeys copy failed: " + ex.GetType().Name + ".");
                }
                return WaitForClipboardText(850, isCurrent);
            }
            finally
            {
                RestoreClipboard(original, originalText);
            }
        }

        private static string TryCaptureSelectionViaAutomation(IntPtr sourceWindow, Point mousePoint)
        {
            try
            {
                uint sourceProcessId;
                NativeMethods.GetWindowThreadProcessId(sourceWindow, out sourceProcessId);
                if (sourceProcessId == 0)
                {
                    return null;
                }

                var focused = AutomationElement.FocusedElement;
                var text = TryElementAndAncestors(focused);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                AutomationElement atPointer = null;
                try
                {
                    atPointer = AutomationElement.FromPoint(
                        new System.Windows.Point(mousePoint.X, mousePoint.Y));
                }
                catch
                {
                }
                text = TryElementAndAncestors(atPointer);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                var root = AutomationElement.FromHandle(sourceWindow);
                text = TryGetSelectionText(root);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                var condition = new PropertyCondition(
                    AutomationElement.IsTextPatternAvailableProperty,
                    true);
                var textElements = root.FindAll(TreeScope.Descendants, condition);
                var count = Math.Min(textElements.Count, 400);
                for (var index = 0; index < count; index++)
                {
                    text = TryGetSelectionText(textElements[index]);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AppLogger.Write("Selection found by searching the source window automation tree.");
                        return text;
                    }
                }
                return null;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Write("UI Automation selection search failed: " + ex.GetType().Name + ".");
                return null;
            }
        }

        private static string TryElementAndAncestors(AutomationElement element)
        {
            var current = element;
            for (var depth = 0; current != null && depth < 14; depth++)
            {
                var text = TryGetSelectionText(current);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                try
                {
                    current = TreeWalker.RawViewWalker.GetParent(current);
                }
                catch
                {
                    break;
                }
            }
            return null;
        }

        private static string TryGetSelectionText(AutomationElement element)
        {
            if (element == null)
            {
                return null;
            }

            try
            {
                object patternObject;
                if (!element.TryGetCurrentPattern(TextPattern.Pattern, out patternObject))
                {
                    return null;
                }

                var pattern = patternObject as TextPattern;
                if (pattern == null)
                {
                    return null;
                }

                var ranges = pattern.GetSelection();
                if (ranges == null || ranges.Length == 0)
                {
                    return null;
                }

                var result = string.Empty;
                foreach (var range in ranges)
                {
                    var part = range.GetText(-1);
                    if (string.IsNullOrEmpty(part))
                    {
                        continue;
                    }
                    if (result.Length > 0)
                    {
                        result += Environment.NewLine;
                    }
                    result += part;
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static string WaitForClipboardText(
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
                Thread.Sleep(25);
                var selected = TryGetClipboardText();
                if (selected != null)
                {
                    return selected;
                }
            }
            return null;
        }

        private static IDataObject TryGetClipboardDataObject()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var source = Clipboard.GetDataObject();
                    if (source == null)
                    {
                        return null;
                    }

                    var snapshot = new DataObject();
                    foreach (var format in source.GetFormats(false))
                    {
                        try
                        {
                            var value = source.GetData(format, false);
                            if (value != null)
                            {
                                snapshot.SetData(format, false, value);
                            }
                        }
                        catch
                        {
                        }
                    }
                    return snapshot;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(15);
                }
            }
            return null;
        }

        private static bool TryClearClipboard()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.Clear();
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(15);
                }
            }
            return false;
        }

        private static string TryGetClipboardText()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        return Clipboard.GetText(TextDataFormat.UnicodeText);
                    }
                    if (Clipboard.ContainsText())
                    {
                        return Clipboard.GetText();
                    }
                    return null;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(15);
                }
            }
            return null;
        }

        private static void RestoreClipboard(IDataObject original, string originalText)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (original != null)
                    {
                        Clipboard.SetDataObject(original, true);
                    }
                    else if (originalText != null)
                    {
                        Clipboard.SetText(originalText);
                    }
                    else
                    {
                        Clipboard.Clear();
                    }
                    return;
                }
                catch
                {
                    Thread.Sleep(15);
                }
            }
        }

        private static bool IsOwnWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }
            uint processId;
            NativeMethods.GetWindowThreadProcessId(window, out processId);
            return processId == (uint)Process.GetCurrentProcess().Id;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Interlocked.Increment(ref _captureGeneration);
            _captureTimer.Dispose();
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}

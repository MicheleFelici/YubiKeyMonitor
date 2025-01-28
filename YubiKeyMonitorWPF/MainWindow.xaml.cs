using Microsoft.Win32;
using System;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace YubiKeyMonitorWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string VendorId = "1050";
        private readonly string[] _knownProductIds =
        {
            "0010", "0110", "0111", "0120",
            "0401", "0403", "0405", "0407", "0410"
        };

        private ManagementEventWatcher _insertWatcher;
        private ManagementEventWatcher _removeWatcher;

        private const int HWND_TOPMOST = -1;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        private static extern bool SetWindowPosNative(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        // ---------------------------------------------------------------------------------------
        // P/Invoke per registrare e gestire RawInput
        // ---------------------------------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        // NOTA: firma corretta per poter passare IntPtr come buffer
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize);

        // ---------------------------------------------------------------------------------------
        // Costanti e strutture per RawInput
        // ---------------------------------------------------------------------------------------
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RID_INPUT = 0x10000003;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDI_DEVICENAME = 0x20000007;
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(16)]
            public RAWHID hid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            public byte bRawData;
        }

        private bool _forceVisible = false;

        // ---------------------------------------------------------------------------------------
        // Gestori mouse e overlay (commentati)
        // ---------------------------------------------------------------------------------------
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            // AnimateOpacity(0.1);
            // IsHitTestVisible = false;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            // AnimateOpacity(0.8);
            // IsHitTestVisible = true;
        }

        private void AnimateOpacity(double targetOpacity)
        {
            //var animation = new DoubleAnimation
            //{
            //    To = targetOpacity,
            //    Duration = TimeSpan.FromSeconds(0.3),
            //    FillBehavior = FillBehavior.HoldEnd
            //};
            //BeginAnimation(OpacityProperty, animation);
        }

        private void OverlayImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Blocca il click-through durante l'animazione
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ForceTopMost();
        }

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += (s, e) => ForceTopMost();
            SetupApplication();
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
            RegisterForRawInput();
        }

        // ---------------------------------------------------------------------------------------
        // Registrazione per RawInput (tastiera generica)
        // ---------------------------------------------------------------------------------------
        private void RegisterForRawInput()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = HID_USAGE_PAGE_GENERIC;
            rid[0].usUsage = HID_USAGE_GENERIC_KEYBOARD;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = new WindowInteropHelper(this).Handle;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                Debug.WriteLine("Failed to register raw input devices.");
            }
        }

        // ---------------------------------------------------------------------------------------
        // Finestra di messaggi Win32 per catturare WM_INPUT
        // ---------------------------------------------------------------------------------------
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;
            if (msg == WM_INPUT)
            {
                HandleRawInput(lParam);
            }
            return IntPtr.Zero;
        }

        // ---------------------------------------------------------------------------------------
        // Lettura effettiva del RawInput
        // ---------------------------------------------------------------------------------------
        private void HandleRawInput(IntPtr lParam)
        {
            // 1) Scopriamo quanti byte servono
            uint dwSize = 0;
            GetRawInputData(
                lParam,
                RID_INPUT,
                IntPtr.Zero,
                ref dwSize,
                (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))
            );

            if (dwSize == 0)
                return;

            // 2) Allochiamo il buffer
            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                // 3) Recuperiamo effettivamente i dati
                uint result = GetRawInputData(
                    lParam,
                    RID_INPUT,
                    buffer,
                    ref dwSize,
                    (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))
                );

                if (result == dwSize)
                {
                    // 4) Convertiamo il buffer in un RAWINPUT
                    RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                    if (raw.header.dwType == RIM_TYPEKEYBOARD)
                    {
                        string deviceName = GetDeviceName(raw.header.hDevice);
                        if (IsYubiKeyDevice(deviceName))
                        {
                            Dispatcher.Invoke(() => TriggerBackgroundAnimation());
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private string GetDeviceName(IntPtr hDevice)
        {
            uint bufferSize = 0;
            // Prima chiamata per la dimensione
            if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref bufferSize) == 0 && bufferSize > 0)
            {
                IntPtr pData = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    // Seconda chiamata per ricavare la stringa
                    if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pData, ref bufferSize) > 0)
                    {
                        return Marshal.PtrToStringAnsi(pData) ?? string.Empty;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pData);
                }
            }
            return string.Empty;
        }

        private bool IsYubiKeyDevice(string deviceName)
        {
                return !string.IsNullOrEmpty(deviceName) &&
                   deviceName.Contains("VID_1050") &&
                   _knownProductIds.Any(pid => deviceName.Contains($"PID_{pid}"));
        }

        private void TriggerBackgroundAnimation()
        {
            var greenBrush = new SolidColorBrush(Colors.Green);
            OverlayBorder.Background = greenBrush;

            // Animazione del colore che ritorna al trasparente dopo 2 secondi
            var animation = new ColorAnimation
            {
                To = Colors.Transparent,
                Duration = TimeSpan.FromSeconds(2),
                FillBehavior = FillBehavior.Stop
            };

            animation.Completed += (s, e) =>
            {
                // Una volta completata l'animazione, resettiamo a trasparente
                OverlayBorder.Background = new SolidColorBrush(Colors.Transparent);
            };

            greenBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        // ---------------------------------------------------------------------------------------
        // Forza la finestra in primo piano
        // ---------------------------------------------------------------------------------------
        private void ForceTopMost()
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPosNative(
                handle,
                new IntPtr(HWND_TOPMOST),
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE);
        }

        // ---------------------------------------------------------------------------------------
        // Setup dell'applicazione: avvio, watchers, posizione, ecc.
        // ---------------------------------------------------------------------------------------
        private void SetupApplication()
        {
            SetStartupRegistry();
            InitializePosition();
            CheckYubiKeyPresence();
            SetupDeviceWatchers();
        }

        private void InitializePosition()
        {
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            var workingArea = primaryScreen.WorkingArea;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Left = workingArea.Right - Width - 10;
                Top = workingArea.Bottom - Height - 10;
            }));
        }

        private void CheckYubiKeyPresence()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"SELECT * FROM Win32_PnPEntity 
                      WHERE DeviceID LIKE 'USB\\VID_1050&PID_%'");

                var found = false;
                foreach (ManagementObject device in searcher.Get())
                {
                    var deviceId = device["DeviceID"]?.ToString() ?? "";
                    foreach (var pid in _knownProductIds)
                    {
                        if (deviceId.Contains($"PID_{pid}"))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                Dispatcher.Invoke(() =>
                {
                    Visibility = found ? Visibility.Visible : Visibility.Hidden;
                    if (found)
                    {
                        InitializePosition();
                        ForceTopMost();
                        Opacity = 0.8; // Resetta l'opacità quando riappare
                        IsHitTestVisible = true;
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() => Visibility = Visibility.Hidden);
            }
        }

        private void SetupDeviceWatchers()
        {
            try
            {
                // Controllo iniziale
                CheckYubiKeyPresence();

                // Watcher per l'inserimento
                var insertQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
                _insertWatcher = new ManagementEventWatcher(insertQuery);
                _insertWatcher.EventArrived += (s, e) => Dispatcher.Invoke(CheckYubiKeyPresence);
                _insertWatcher.Start();

                // Watcher per la rimozione
                var removeQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");
                _removeWatcher = new ManagementEventWatcher(removeQuery);
                _removeWatcher.EventArrived += (s, e) => Dispatcher.Invoke(CheckYubiKeyPresence);
                _removeWatcher.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore monitoraggio dispositivi: {ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStartupRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                var appPath = Process.GetCurrentProcess().MainModule.FileName;

                if (key?.GetValue("YubiKeyMonitorWPF") == null)
                {
                    key?.SetValue("YubiKeyMonitorWPF", appPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore impostazione avvio automatico: {ex.Message}");
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _insertWatcher?.Stop();
            _removeWatcher?.Stop();
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            _insertWatcher?.Dispose();
            _removeWatcher?.Dispose();
            base.OnClosed(e);
        }
    }
}

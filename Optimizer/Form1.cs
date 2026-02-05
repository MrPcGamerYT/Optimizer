using Guna.UI2.WinForms;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace Optimizer
{
    public partial class Optimizer : Form
    {
        private void SetAdminStatus(string text, Color color)
        {
            lblAdminStatus.Text = text;
            lblAdminStatus.ForeColor = color;
        }

        private Timer trayBlinkTimer;
        private bool trayBlinkState = false;
        private Icon trayIconNormal;
        private Icon trayIconAlert;


        private NotifyIcon trayIcon;
        private bool suppressMinimizeEvent = false;
        private bool allowExit = false;
        private ContextMenuStrip trayMenu;
        private bool bgAppBoostRunning = false;
        private Timer pingTimer;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        private float currentOverall = 0;
        private int targetOverall = 0;

        private int CalculateOverallCondition(int cpu, int ram, int drive)
        {
            // Weighting (realistic)
            int stress =
                (cpu * 40 / 100) +
                (ram * 35 / 100) +
                (drive * 25 / 100);

            int condition = 100 - stress;

            if (condition < 0) condition = 0;
            if (condition > 100) condition = 100;

            return condition;
        }


        // ===============================
        // NORMAL GAME MODE
        // ===============================
        private bool normalGameModeRunning = false;
        private string currentGame = null;

        // Known PC game executables
        private readonly string[] gameExecutables =
        {
    "csgo",
    "valorant",
    "fortnite",
    "gta5",
    "gta_sa",
    "gtaiv",
    "rdr2",
    "eldenring",
    "cyberpunk2077",
    "minecraft",
    "forzahorizon5",
    "callofduty",
    "mw2",
    "mw3",
    "bf2042",
    "bfv",
    "bf1",
    "apex",
    "pubg",
    "dota2",
    "leagueoflegends",
    "overwatch"
};

        private async void NormalGameModeLoop()
        {
            while (normalGameModeRunning)
            {
                bool found = false;

                foreach (string game in gameExecutables)
                {
                    Process[] p = Process.GetProcessesByName(game);
                    if (p.Length > 0)
                    {
                        found = true;

                        if (currentGame != game)
                        {
                            currentGame = game;
                            ApplyNormalGameBoost(p[0]);

                            this.Invoke((Action)(() =>
                            {
                                lblGameModeStatus.Text = $"Game Mode: {game.ToUpper()} Running 🎮";
                                lblGameModeStatus.ForeColor = Color.Lime;
                            }));
                        }
                        break;
                    }
                }

                if (!found && currentGame != null)
                {
                    currentGame = null;

                    this.Invoke((Action)(() =>
                    {
                        lblGameModeStatus.Text = "Game Mode: Waiting for Game…";
                        lblGameModeStatus.ForeColor = Color.Gold;
                    }));
                }

                await Task.Delay(2000);
            }
        }

        private void ApplyNormalGameBoost(Process game)
        {
            try
            {
                game.PriorityClass = ProcessPriorityClass.High;

                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.Equals(game.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.StartsWith("System"))
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }


        // ===============================
        // EMULATOR GAME MODE
        // ===============================
        private readonly string[] emulatorProcesses =
        {
    "HD-Player",        // BlueStacks / MSI App Player
    "dnplayer",         // LDPlayer
    "Nox",
    "MEmu",
    "AndroidEmulator"  // GameLoop
};

        private bool emulatorBoostRunning = false;


        private async Task EmulatorBoostLoopAsync()
        {
            while (emulatorBoostRunning)
            {
                foreach (string emu in emulatorProcesses)
                {
                    foreach (Process p in Process.GetProcessesByName(emu))
                    {
                        try
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            p.ProcessorAffinity =
                                (IntPtr)((1 << Environment.ProcessorCount) - 1);
                        }
                        catch { }
                    }
                }

                await Task.Delay(2000);
            }
        }

        private void UpdateEmulatorStatus()
        {
            if (!tgAdvancedGame.Checked || !emulatorBoostRunning)
                return; // ✅ already good, DO NOTHING

            foreach (string emu in emulatorProcesses)
            {
                if (Process.GetProcessesByName(emu).Length > 0)
                {
                    lblGameModeStatus.Text = $"Game Mode Applied On {emu}";
                    lblGameModeStatus.ForeColor = Color.Lime;
                    return;
                }
            }

            lblGameModeStatus.Text = "Waiting for Emulator…";
            lblGameModeStatus.ForeColor = Color.Orange;
        }



        // ===============================
        // GENERAL
        // ===============================
        private ToolTip tip;

        // ===============================
        // SMOOTHING VARIABLES
        // ===============================
        // Higher value = faster/snappier, Lower value = smoother/slower
        private const float smoothing = 0.12f; 

        private float currentCpu = 0;
        private int targetCpu = 0;

        private float currentRam = 0;
        private int targetRam = 0;

        private float currentDrive = 0;
        private int targetDriveUsage = 0;

        // ===============================
        // CPU & SYSTEM
        // ===============================
        private PerformanceCounter cpuCounter;
        private DriveInfo systemDrive;
        private Timer animationTimer;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);


        private void LoadSavedSettings()
        {
            suppressStartWithWindowsEvent = true;
            suppressMinimizeEvent = true;

            tgStartWithWindows.Checked = Properties.Settings.Default.StartWithWindows;
            tgMinimizeToTray.Checked = Properties.Settings.Default.MinimizeToTray;
            tgReduceAnimations.Checked = Properties.Settings.Default.ReduceAnimations;

            suppressStartWithWindowsEvent = false;
            suppressMinimizeEvent = false;
        }


        public Optimizer()
        {
            InitializeComponent();
            InitCounters();
            LoadSystemInfo();
            // ===============================
            // PING TIMER (1s)
            // ===============================
            pingTimer = new Timer();
            pingTimer.Interval = 1000; // 1 second
            pingTimer.Tick += PingTimer_Tick;
            pingTimer.Start();
            InitTray();
            tip = new ToolTip();
            systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));

            lblDriveCTitle.Text = $"{systemDrive.VolumeLabel} ({systemDrive.Name.TrimEnd('\\')})";

            // Main Data Fetch Timer (1 second)
            usageTimer.Interval = 1000;
            usageTimer.Tick += UsageTimer_Tick;
            usageTimer.Start();

            // High-Speed Animation Timer (16ms ~ 60 FPS)
            animationTimer = new Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
            tgStartWithWindows.Enabled = IsRunningAsAdmin();
            LoadStartWithWindowsState();
            if (IsRunningAsAdmin())
            {
                lblAdminStatus.Text = "Admin: YES ✔";
                lblAdminStatus.ForeColor = Color.Lime;
            }
            else
            {
                lblAdminStatus.Text = "Admin: NO ❌";
                lblAdminStatus.ForeColor = Color.OrangeRed;
            }

            trayIconNormal = this.Icon;
            trayIconAlert = Properties.Resources.Icon;
            LoadSavedSettings();
            trayBlinkTimer = new Timer();
            trayBlinkTimer.Interval = 500; // blink speed (ms)
            trayBlinkTimer.Tick += (s, e) =>
            {
                trayBlinkState = !trayBlinkState;
                trayIcon.Icon = trayBlinkState ? trayIconAlert : trayIconNormal;
            };
            this.MaximumSize = this.Size;
            this.MinimumSize = this.Size;
            lblVersion.Text = Application.ProductVersion; // ✅ SAFE HERE TOO
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MAXIMIZE = 0xF030;

            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_MAXIMIZE)
            {
                return; // ❌ block maximize
            }

            base.WndProc(ref m);
        }

        private void StartTrayBlink()
        {
            trayIcon.Visible = true;   // 🔥 REQUIRED
            trayBlinkTimer.Start();
        }


        private void StopTrayBlink()
        {
            trayBlinkTimer.Stop();
            trayIcon.Icon = trayIconNormal;
        }



        private void InitTray()
        {
            trayMenu = new ContextMenuStrip();

            // Restore
            trayMenu.Items.Add("Show Optimizer", null, (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;

                SetAdminStatus("Restored from Tray", Color.Lime);
            });

            trayMenu.Items.Add(new ToolStripSeparator());

            // Exit
            trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                allowExit = true;
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon = new NotifyIcon
            {
                Text = "Optimizer",
                Icon = this.Icon, // uses your app icon
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            // Double-click to restore
            trayIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
            };
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    trayIcon.Visible = false;
                }
            };

        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (tgMinimizeToTray.Checked && this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                trayIcon.Visible = true;

                trayIcon.ShowBalloonTip(
                    1000,
                    "Optimizer",
                    "Running in system tray",
                    ToolTipIcon.Info
                );
            }
        }


        private void PingTimer_Tick(object sender, EventArgs e)
        {
            Task.Run(() => UpdatePing());
        }


        private void InitCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                cpuCounter.NextValue();
            }
            catch
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
        }

        // FETCH DATA (Runs every 1s)
        private void UsageTimer_Tick(object sender, EventArgs e)
        {
            targetCpu = (int)Math.Min(100, cpuCounter.NextValue());
            targetRam = GetRamUsage();
            targetDriveUsage = GetDriveUsage();
            UpdateDriveTooltip();
            UpdateEmulatorStatus();
            targetOverall = CalculateOverallCondition(targetCpu, targetRam, targetDriveUsage);

        }

        // RENDER SMOOTH ANIMATION (Runs every 16ms)
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (tgReduceAnimations.Checked)
            {
                animationTimer.Interval = 40; // slower = less CPU
            }
            else
            {
                animationTimer.Interval = 16; // smooth
            }

            // 1. Smooth CPU
            currentCpu += (targetCpu - currentCpu) * smoothing;
            cpubar.Value = (int)currentCpu;
            cpuusage.Text = (int)currentCpu + "%";
            ApplyNeon(cpubar, (int)currentCpu);

            // 2. Smooth RAM
            currentRam += (targetRam - currentRam) * smoothing;
            rambar.Value = (int)currentRam;
            ramusage.Text = (int)currentRam + "%";
            ApplyNeon(rambar, (int)currentRam);

            // 3. Smooth Drive
            currentDrive += (targetDriveUsage - currentDrive) * smoothing;
            int dValue = Math.Max(0, Math.Min(100, (int)currentDrive));
            driveCBar.Value = dValue;
            lblDriveC.Text = dValue + "% Used";

            ApplyStorageNeon(driveCBar, dValue);
            UpdateStorageHealth(dValue);

            // 4. Smooth Overall Condition
            currentOverall += (targetOverall - currentOverall) * smoothing;
            int oValue = Math.Max(0, Math.Min(100, (int)currentOverall));

            overallBar.Value = oValue;
            lblOverallPercent.Text = oValue + "%";

            if (oValue >= 75)
            {
                lblOverallStatus.Text = "Overall PC Condition:EXCELLENT";
                overallBar.ProgressColor = Color.Lime;
                overallBar.ProgressColor2 = Color.Cyan;
            }
            else if (oValue >= 50)
            {
                lblOverallStatus.Text = "Overall PC Condition:GOOD";
                overallBar.ProgressColor = Color.Gold;
                overallBar.ProgressColor2 = Color.Orange;
            }
            else if (oValue >= 30)
            {
                lblOverallStatus.Text = "Overall PC Condition:STRESSED";
                overallBar.ProgressColor = Color.OrangeRed;
                overallBar.ProgressColor2 = Color.DarkOrange;
            }
            else
            {
                lblOverallStatus.Text = "Overall PC Condition:CRITICAL";
                overallBar.ProgressColor = Color.Red;
                overallBar.ProgressColor2 = Color.DarkRed;
            }

        }

        private int GetRamUsage()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus)) return (int)memStatus.dwMemoryLoad;
            return 0;
        }

        private int GetDriveUsage()
        {
            try
            {
                long used = systemDrive.TotalSize - systemDrive.TotalFreeSpace;
                return (int)(used * 100 / systemDrive.TotalSize);
            }
            catch { return 0; }
        }

        private void UpdateDriveTooltip()
        {
            try
            {
                double total = systemDrive.TotalSize / 1024d / 1024 / 1024;
                double free = systemDrive.TotalFreeSpace / 1024d / 1024 / 1024;
                double used = total - free;

                string text = $"{systemDrive.VolumeLabel} ({systemDrive.Name.TrimEnd('\\')})\n\n" +
                              $"Used: {used:F1} GB\n" +
                              $"Free: {free:F1} GB\n" +
                              $"Total: {total:F1} GB";

                tip.SetToolTip(driveCBar, text);
                tip.SetToolTip(lblDriveC, text);
            }
            catch { }
        }

        private void ApplyNeon(Guna2CircleProgressBar bar, int value)
        {
            if (value < 40) { bar.ProgressColor = Color.FromArgb(0, 255, 170); bar.ProgressColor2 = Color.FromArgb(0, 150, 255); }
            else if (value < 80) { bar.ProgressColor = Color.Gold; bar.ProgressColor2 = Color.Orange; }
            else { bar.ProgressColor = Color.Red; bar.ProgressColor2 = Color.DarkRed; }
        }

        private void ApplyStorageNeon(Guna2CircleProgressBar bar, int value)
        {
            if (value < 60) { bar.ProgressColor = Color.FromArgb(0, 255, 170); bar.ProgressColor2 = Color.FromArgb(0, 150, 255); }
            else if (value < 85) { bar.ProgressColor = Color.Gold; bar.ProgressColor2 = Color.Orange; }
            else { bar.ProgressColor = Color.Red; bar.ProgressColor2 = Color.DarkRed; }
        }

        private void UpdateStorageHealth(int usage)
        {
            if (usage < 70) { lblStorageHealth.Text = "Storage Health: Good"; lblStorageHealth.ForeColor = Color.Lime; }
            else if (usage < 90) { lblStorageHealth.Text = "Storage Health: Warning"; lblStorageHealth.ForeColor = Color.Gold; }
            else { lblStorageHealth.Text = "Storage Health: CRITICAL"; lblStorageHealth.ForeColor = Color.Red; }
        }

        private void LoadSystemInfo()
        {
            lblPCName.Text = "PC Name: " + Environment.MachineName;
            lblWindows.Text = "Windows: " + GetWindowsEdition();
            lblOS.Text = "OS Version: " + GetOSVersion();
            lblCPU.Text = "CPU: " + GetCPUName();
            lblGPU.Text = "GPU: " + GetGPUName();
            lblTotalRAM.Text = "Total RAM: " + GetTotalRam() + " GB";
            lblArch.Text = Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS";
        }

        private string GetCPUName()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                foreach (ManagementObject o in s.Get()) return o["Name"].ToString();
            return "Unknown";
        }

        private string GetGPUName()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                foreach (ManagementObject o in s.Get()) return o["Name"].ToString();
            return "Unknown";
        }

        private string GetTotalRam()
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                foreach (ManagementObject o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalPhysicalMemory"]) / 1024 / 1024 / 1024, 1).ToString();
            return "0";
        }

        private string GetWindowsEdition()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get()) return os["Caption"]?.ToString() ?? "Unknown";
            return "Unknown";
        }

        private string GetOSVersion()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Version FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get()) return os["Version"]?.ToString() ?? "Unknown";
            return "Unknown";
        }

        private void ShowPanel(Panel p)
        {
            Honepnl.Visible = Cleanerpnl.Visible = boostpnl.Visible = gamemodpnl.Visible = settingspnl.Visible = infopnl.Visible = false;
            p.Visible = true;
        }

        void CleanFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string file in Directory.GetFiles(path)) try { File.Delete(file); } catch { }
                foreach (string dir in Directory.GetDirectories(path)) try { Directory.Delete(dir, true); } catch { }
            }
            catch { }
        }

        [DllImport("shell32.dll")]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

        enum RecycleFlags { SHERB_NOCONFIRMATION = 0x00000001, SHERB_NOPROGRESSUI = 0x00000002, SHERB_NOSOUND = 0x00000004 }

        private void guna2Button1_Click(object s, EventArgs e) => ShowPanel(Honepnl);
        private void guna2Button2_Click(object s, EventArgs e) => ShowPanel(boostpnl);
        private void guna2Button3_Click(object s, EventArgs e) => ShowPanel(Cleanerpnl);
        private void guna2Button4_Click(object s, EventArgs e) => ShowPanel(gamemodpnl);
        private void guna2Button5_Click(object s, EventArgs e) => ShowPanel(settingspnl);
        private void guna2Button6_Click(object s, EventArgs e) => ShowPanel(infopnl);

        private void btnCleanNow_Click_1(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                bool anyChecked = chkTemp.Checked || chkWinTemp.Checked || chkPrefetch.Checked || chkBrowser.Checked || chkRecycle.Checked;
                try
                {
                    if (chkTemp.Checked) CleanFolder(Path.GetTempPath());
                    if (chkWinTemp.Checked) CleanFolder(@"C:\Windows\Temp");
                    if (chkPrefetch.Checked) CleanFolder(@"C:\Windows\Prefetch");
                    if (chkBrowser.Checked) CleanFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data\Default\Cache");
                    if (chkRecycle.Checked) SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
                }
                catch { }

                this.Invoke((Action)(() =>
                {
                    if (anyChecked) ShowCleanPopup("Clean Completed ✔", Color.Lime);
                    else ShowCleanPopup("Nothing Selected ❌", Color.OrangeRed);

                    // ✅ RESET CHECKBOXES
                    ResetCleanerCheckboxes();

                }));
            });
        }

        private async void ShowCleanPopup(string message, Color color)
        {
            lblCleanStatus.Text = message;
            lblCleanStatus.ForeColor = color;
            lblCleanStatus.Visible = true;
            await Task.Delay(2000);
            for (int i = 100; i >= 0; i -= 5)
            {
                lblCleanStatus.ForeColor = Color.FromArgb(i, color.R, color.G, color.B);
                await Task.Delay(30);
            }
            lblCleanStatus.Visible = false;
            lblCleanStatus.ForeColor = color;
        }

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        private void btnRamBoost_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        EmptyWorkingSet(p.Handle);
                    }
                    catch { }
                }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("RAM Boosted 🚀", Color.DeepSkyBlue);
                }));
            });
        }

        private void btnBgApps_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.ToLower().Contains("system") &&
                            !p.ProcessName.ToLower().Contains("explorer"))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("Background Apps Closed ✔", Color.Lime);
                }));
            });
        }

        private void btnHighPerf_Click(object sender, EventArgs e)
        {
            Process.Start("cmd.exe", "/c powercfg -setactive SCHEME_MIN");
            ShowBoostPopup("High Performance Enabled ⚡", Color.Orange);
        }

        private async void ShowBoostPopup(string message, Color color)
        {
            lblBoostStatus.Text = message;
            lblBoostStatus.ForeColor = color;
            lblBoostStatus.Visible = true;
            await Task.Delay(2000);
            for (int i = 100; i >= 0; i -= 5)
            {
                lblBoostStatus.ForeColor = Color.FromArgb(i, color.R, color.G, color.B);
                await Task.Delay(30);
            }
            lblBoostStatus.Visible = false;
            lblBoostStatus.ForeColor = color;
        }



        private void EnableAdvancedGameMode()
        {
            try
            {
                // 1. Ultimate Performance
                Process.Start("cmd.exe", "/c powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

                // 2. Disable CPU Throttling
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    1,
                    RegistryValueKind.DWord
                );

                // 3. Reduce visual effects
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    2,
                    RegistryValueKind.DWord
                );

                // 4. Kill background apps (safe)
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                            !p.ProcessName.StartsWith("System"))
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void DisableAdvancedGameMode()
        {
            try
            {
                // Balanced Power Plan
                Process.Start("cmd.exe", "/c powercfg -setactive SCHEME_BALANCED");

                // Enable CPU Throttling back
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                    "PowerThrottlingOff",
                    0,
                    RegistryValueKind.DWord
                );

                // Restore visuals
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    0,
                    RegistryValueKind.DWord
                );
            }
            catch { }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void guna2GradientTileButton1_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    IntPtr hwnd = GetForegroundWindow();
                    GetWindowThreadProcessId(hwnd, out int pid);

                    Process fg = Process.GetProcessById(pid);
                    fg.PriorityClass = ProcessPriorityClass.High;

                    foreach (Process p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.Id != fg.Id &&
                                !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                                !p.ProcessName.StartsWith("System"))
                            {
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("CPU Priority Boost Applied", Color.DeepSkyBlue);
                }));
            });
        }

        private void btnNetBoost_Click(object sender, EventArgs e)
        {
            // 🔒 HARD ADMIN CHECK FIRST
            if (!IsRunningAsAdmin())
            {
                ShowBoostPopup("Admin Rights Required ⚠", Color.Red);
                SetAdminStatus("Network Boost Failed (No Admin)", Color.Red);
                return;
            }

            try
            {
                // Network Throttling OFF
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex",
                    unchecked((int)0xFFFFFFFF),   // ✅ IMPORTANT
                    RegistryValueKind.DWord
                );

                // System responsiveness max
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness",
                    0,
                    RegistryValueKind.DWord
                );

                ShowBoostPopup("Network Boost Enabled 🚀", Color.Lime);
                SetAdminStatus("Network Boost: ENABLED", Color.Lime);
            }
            catch (Exception ex)
            {
                // ❌ REAL ERROR (not admin related)
                ShowBoostPopup("Network Boost Failed ❌", Color.OrangeRed);
                SetAdminStatus("Network Boost Error", Color.OrangeRed);

                // OPTIONAL: debug only
                Debug.WriteLine(ex.Message);
            }
        }


        private void btnQuickFlush_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                this.Invoke((Action)(() =>
                {
                    ShowBoostPopup("Quick Memory Flush Done", Color.Lime);
                }));
            });
        }

        private void ResetCleanerCheckboxes()
        {
            chkTemp.Checked = false;
            chkWinTemp.Checked = false;
            chkPrefetch.Checked = false;
            chkBrowser.Checked = false;
            chkRecycle.Checked = false;
        }

        private void tgNormalGame_CheckedChanged(object sender, EventArgs e)
        {
            // ❌ Turn off Advanced Emulator Mode if running
            if (tgNormalGame.Checked && tgAdvancedGame.Checked)
            {
                tgAdvancedGame.Checked = false;
            }

            if (tgNormalGame.Checked)
            {
                normalGameModeRunning = true;
                currentGame = null;
                trayIcon.Visible = true;
                UpdateTrayBlinkState();
                // 🔥 NOW IT WILL BLINK

                Task.Run(() => NormalGameModeLoop());

                lblGameModeStatus.Text = "Normal Game Mode: ENABLED";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                normalGameModeRunning = false;
                currentGame = null;
                UpdateTrayBlinkState();
                lblGameModeStatus.Text = "Normal Game Mode: DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }

        }

        private void tgAdvancedGame_CheckedChanged(object sender, EventArgs e)
        {
            // Prevent conflict with Normal Game Mode
            if (tgAdvancedGame.Checked && tgNormalGame.Checked)
            {
                tgNormalGame.Checked = false;
            }

            if (tgAdvancedGame.Checked)
            {
                emulatorBoostRunning = true;

                trayIcon.Visible = true;
                UpdateTrayBlinkState();                // 🔥 NOW IT WILL BLINK

                lblGameModeStatus.Text = "Waiting for Emulator…";
                lblGameModeStatus.ForeColor = Color.Gold;

                EnableAdvancedGameMode();

                // ✅ DO NOT AWAIT — run in background
                Task.Run(() => EmulatorBoostLoopAsync());
            }
            else
            {
                emulatorBoostRunning = false;

                UpdateTrayBlinkState();

                DisableAdvancedGameMode();

                lblGameModeStatus.Text = "Advanced Emulator Game Mode: DISABLED";
                lblGameModeStatus.ForeColor = Color.Orange;
            }
        }

        private void UpdateTrayBlinkState()
        {
            if (normalGameModeRunning || emulatorBoostRunning || bgAppBoostRunning)
            {
                StartTrayBlink();
            }
            else
            {
                StopTrayBlink();
            }
        }



        private void tgBgApps_CheckedChanged(object sender, EventArgs e)
        {
            if (tgBgApps.Checked)
            {
                bgAppBoostRunning = true;

                Task.Run(() => BackgroundAppsBoostLoop());

                lblGameModeStatus.Text = "Background Apps Disabled";
                lblGameModeStatus.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                bgAppBoostRunning = false;

                lblGameModeStatus.Text = "Background Apps Restored";
                lblGameModeStatus.ForeColor = Color.Orange;
            }
        }

        private void BackgroundAppsBoostLoop()
        {
            while (bgAppBoostRunning)
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        string name = p.ProcessName.ToLower();

                        // ❌ NEVER TOUCH SYSTEM / GAME / EMULATOR
                        if (name.Contains("system") ||
                            name.Contains("explorer") ||
                            name.Contains("svchost") ||
                            name.Contains("wininit") ||
                            name.Contains("winlogon"))
                            continue;

                        // Skip game & emulator processes
                        if (currentGame != null && name.Contains(currentGame.ToLower()))
                            continue;

                        foreach (string emu in emulatorProcesses)
                            if (name.Contains(emu.ToLower()))
                                goto SKIP;

                        p.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    catch { }

                SKIP:;
                }

                System.Threading.Thread.Sleep(3000); // every 3 sec
            }
        }

        private void UpdatePing()
        {
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send("8.8.8.8", 1000); // Google DNS

                if (reply.Status == IPStatus.Success)
                {
                    int ms = (int)reply.RoundtripTime;

                    this.Invoke((Action)(() =>
                    {
                        lblPing.Text = $"Ping: {ms} ms";
                        pingBar.Value = Math.Max(pingBar.Minimum,
                         Math.Min(ms, pingBar.Maximum));
                        ApplyPingColor(ms);
                    }));
                }
                else
                {
                    SetPingOffline();
                }
            }
            catch
            {
                SetPingOffline();
            }
        }

        private void SetPingOffline()
        {
            this.Invoke((Action)(() =>
            {
                lblPing.Text = "Ping: -- ms";
                pingBar.Value = 0;
                pingBar.ProgressColor = Color.Gray;
                pingBar.ProgressColor2 = Color.DarkGray;
            }));
        }

        private void ApplyPingColor(int ms)
        {
            if (ms < 60)
            {
                pingBar.ProgressColor = Color.Lime;
                pingBar.ProgressColor2 = Color.GreenYellow;
            }
            else if (ms < 120)
            {
                pingBar.ProgressColor = Color.Gold;
                pingBar.ProgressColor2 = Color.Orange;
            }
            else
            {
                pingBar.ProgressColor = Color.Red;
                pingBar.ProgressColor2 = Color.DarkRed;
            }
        }

        private bool suppressStartWithWindowsEvent = false;

        private void tgStartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressStartWithWindowsEvent)
                return;

            // 🔒 CHECK ADMIN FIRST
            if (tgStartWithWindows.Checked && !IsRunningAsAdmin())
            {
                suppressStartWithWindowsEvent = true;

                tgStartWithWindows.Checked = false;

                suppressStartWithWindowsEvent = false;

                SetAdminStatus(
                    "Admin Rights Required ⚠",
                    Color.Red
                );
                return;
            }

            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (tgStartWithWindows.Checked)
                    {
                        rk.SetValue("Optimizer", Application.ExecutablePath);

                        SetAdminStatus(
                            "Start With Windows: ENABLED",
                            Color.Lime
                        );
                    }
                    else
                    {
                        rk.DeleteValue("Optimizer", false);

                        SetAdminStatus(
                            "Start With Windows: DISABLED",
                            Color.Orange
                        );
                    }
                }
            }
            catch
            {
                SetAdminStatus(
                    "Permission Denied ⚠ Run as Admin",
                    Color.Red
                );
            }
            Properties.Settings.Default.StartWithWindows = tgStartWithWindows.Checked;
            Properties.Settings.Default.Save();
        }




        private bool IsRunningAsAdmin()
        {
            return new WindowsPrincipal(
                WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void btnRestoreDefaults_Click(object sender, EventArgs e)
        {
            tgStartWithWindows.Checked = false;
            tgMinimizeToTray.Checked = false;
            tgReduceAnimations.Checked = false;
            tgNormalGame.Checked = false;
            tgAdvancedGame.Checked = false;
            tgBgApps.Checked = false;

            Properties.Settings.Default.Reset();
            Properties.Settings.Default.Save();

            SetAdminStatus(
                "Settings Restored to Default",
                Color.DeepSkyBlue
            );
        }



        

        private void LoadStartWithWindowsState()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    bool enabled = rk?.GetValue("Optimizer") != null;

                    // Prevent event firing while syncing
                    suppressStartWithWindowsEvent = true;
                    tgStartWithWindows.Checked = enabled;
                    suppressStartWithWindowsEvent = false;

                    SetAdminStatus(
                        enabled ? "Start With Windows: ENABLED" : "Start With Windows: DISABLED",
                        enabled ? Color.Lime : Color.Orange
                    );
                }
            }
            catch
            {
                SetAdminStatus(
                    "Unable to Read Startup Status",
                    Color.Red
                );
            }
        }



        private void tgMinimizeToTray_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressMinimizeEvent)
                return;

            if (tgMinimizeToTray.Checked)
            {
                SetAdminStatus(
                    "Minimize to Tray: ENABLED",
                    Color.Lime
                );
            }
            else
            {
                trayIcon.Visible = false;

                SetAdminStatus(
                    "Minimize to Tray: DISABLED",
                    Color.Orange
                );
            }
            Properties.Settings.Default.MinimizeToTray = tgMinimizeToTray.Checked;
            Properties.Settings.Default.Save();


        }

        private void guna2ControlBox1_Click(object sender, EventArgs e)
        {
            if (tgMinimizeToTray.Checked)
            {
                this.Hide();
                trayIcon.Visible = true;
                return;
            }

            ExitApplication();
        }

        private void ExitApplication()
        {
            allowExit = true;
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (tgMinimizeToTray.Checked && !allowExit)
            {
                e.Cancel = true;

                this.Hide();
                trayIcon.Visible = true;

                trayIcon.ShowBalloonTip(
                    1000,
                    "Optimizer",
                    "Still running in system tray",
                    ToolTipIcon.Info
                );

                SetAdminStatus("Running in Tray", Color.DeepSkyBlue);
            }

            base.OnFormClosing(e);
        }

        private void guna2ToggleSwitch1_CheckedChanged(object sender, EventArgs e)
        {
            if (tgReduceAnimations.Checked)
            {
                SetAdminStatus("Animations Reduced",
                Color.Gold
                );
            }
            else
            {
                SetAdminStatus("Animations Restored",
                Color.DeepSkyBlue
                );
            }
            Properties.Settings.Default.ReduceAnimations = tgReduceAnimations.Checked;
            Properties.Settings.Default.Save();

        }

        private void label19_Click(object sender, EventArgs e)
        {

        }

        private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://www.youtube.com/@MR.PC_GAMER_YT",
                UseShellExecute = true
            });
        }

        private void guna2ImageButton2_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/XbqcMzwfQQ",
                UseShellExecute = true
            });
        }
    }
}
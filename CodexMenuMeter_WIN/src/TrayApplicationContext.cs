using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexMenuMeter
{
    internal static class TrayText
    {
        public static string Format(int? remaining)
        {
            return remaining.HasValue ? remaining.Value.ToString() : "--";
        }
    }

    internal sealed class TrayLayout
    {
        public readonly Rectangle DotBounds;
        public readonly Rectangle NumberBounds;

        private TrayLayout(Rectangle dotBounds, Rectangle numberBounds)
        {
            DotBounds = dotBounds;
            NumberBounds = numberBounds;
        }

        public static TrayLayout Calculate(int size, bool showDot)
        {
            int edge = Math.Max(1, size / 16);
            if (!showDot)
                return new TrayLayout(Rectangle.Empty,
                    new Rectangle(edge, edge, size - edge * 2, size - edge * 2));
            int dot = Math.Max(4, size * 7 / 24);
            return new TrayLayout(new Rectangle(edge, edge, dot, dot),
                new Rectangle(edge, dot + edge * 2, size - edge * 2, size - dot - edge * 3));
        }
    }

    internal static class MeterSettings
    {
        private const string SettingsKey = @"Software\CodexMenuMeter";

        public static bool ParseShowTaskStatusDot(object value)
        {
            return value is int && (int)value != 0;
        }

        public static bool ShowTaskStatusDot
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                        return key != null && ParseShowTaskStatusDot(key.GetValue("ShowTaskStatusDot"));
                }
                catch (SecurityException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    key.SetValue("ShowTaskStatusDot", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }
    }

    internal static class CodexProcessMonitor
    {
        public static bool IsOfficial(string processName, string executablePath)
        {
            return string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(executablePath)
                && executablePath.IndexOf("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0
                && executablePath.EndsWith("\\ChatGPT.exe", StringComparison.OrdinalIgnoreCase);
        }

        public static string FindOfficialPath()
        {
            foreach (Process process in Process.GetProcessesByName("ChatGPT"))
            {
                try
                {
                    string path = process.MainModule == null ? null : process.MainModule.FileName;
                    if (IsOfficial(process.ProcessName, path)) return path;
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            }
            return null;
        }

        public static string FindCliPath(string desktopPath)
        {
            return FindCliPath(desktopPath,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetEnvironmentVariable("PATH") ?? "");
        }

        internal static string FindCliPath(string desktopPath, string localAppData, string path)
        {
            string cacheRoot = Path.Combine(localAppData ?? "", "OpenAI", "Codex", "bin");
            try
            {
                if (Directory.Exists(cacheRoot))
                {
                    string cached = Directory.GetFiles(cacheRoot, "codex.exe", SearchOption.AllDirectories)
                        .OrderByDescending(delegate(string candidate) { return File.GetLastWriteTimeUtc(candidate); })
                        .FirstOrDefault();
                    if (cached != null) return cached;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (!string.IsNullOrEmpty(desktopPath))
            {
                string bundled = Path.Combine(Path.GetDirectoryName(desktopPath), "resources", "codex.exe");
                if (File.Exists(bundled)) return bundled;
            }
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string candidate = Path.Combine(directory.Trim().Trim('"'), "codex.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }

    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string SettingsKey = @"Software\CodexMenuMeter";
        private const string ValueName = "CodexMenuMeter";

        public static bool Enabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void EnsureDefault()
        {
            using (RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                if (settings.GetValue("StartupConfigured") == null)
                {
                    SetEnabled(true);
                    settings.SetValue("StartupConfigured", 1, RegistryValueKind.DWord);
                }
                else if (Enabled) SetEnabled(true); // Refresh the path after moving a build.
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    internal static class TrayDpi
    {
        private const int SmallIconMetric = 49;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        public static int IconSize()
        {
            int fallback = Math.Max(16, SystemInformation.SmallIconSize.Width);
            try
            {
                IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
                uint dpi = taskbar == IntPtr.Zero ? 0 : GetDpiForWindow(taskbar);
                int size = dpi == 0 ? 0 : GetSystemMetricsForDpi(SmallIconMetric, dpi);
                return size > 0 ? size : fallback;
            }
            catch (EntryPointNotFoundException) { return fallback; }
        }
    }

    internal static class TrayIconRenderer
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon Render(string text, bool showDot)
        {
            int size = TrayDpi.IconSize();
            TrayLayout layout = TrayLayout.Calculate(size, showDot);
            using (Bitmap bitmap = new Bitmap(size, size))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                if (showDot)
                    using (Brush dot = new SolidBrush(Color.FromArgb(128, 128, 128)))
                        graphics.FillEllipse(dot, layout.DotBounds);
                DrawNumber(graphics, text, layout.NumberBounds, size);

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        private static void DrawNumber(Graphics graphics, string text, Rectangle bounds, int iconSize)
        {
            bool light = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    light = key != null && Convert.ToInt32(key.GetValue("SystemUsesLightTheme", 0)) != 0;
            }
            catch { }

            using (GraphicsPath path = new GraphicsPath())
            using (FontFamily family = new FontFamily("Arial Narrow"))
            {
                path.AddString(text, family, (int)FontStyle.Bold, 100f, Point.Empty,
                    StringFormat.GenericTypographic);
                RectangleF source = path.GetBounds();
                float scale = Math.Min(bounds.Width / source.Width, bounds.Height / source.Height);
                float x = bounds.X + (bounds.Width - source.Width * scale) / 2f - source.X * scale;
                float y = bounds.Y + (bounds.Height - source.Height * scale) / 2f - source.Y * scale;
                using (Matrix transform = new Matrix(scale, 0, 0, scale, x, y)) path.Transform(transform);
                Color foreground = light ? Color.FromArgb(24, 24, 24) : Color.White;
                Color outline = light ? Color.White : Color.FromArgb(24, 24, 24);
                using (Pen pen = new Pen(outline, Math.Max(1f, iconSize / 24f)))
                using (Brush brush = new SolidBrush(foreground))
                {
                    pen.LineJoin = LineJoin.Round;
                    graphics.DrawPath(pen, path);
                    graphics.FillPath(brush, path);
                }
            }
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly CheckBox showDot = new CheckBox();
        private readonly CheckBox startup = new CheckBox();
        private readonly Action saved;

        public SettingsForm(Action saved)
        {
            this.saved = saved;
            Text = "CodexMenuMeter 设置";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;

            showDot.AutoSize = true;
            showDot.Text = "显示任务状态点（预留功能）";
            showDot.Checked = MeterSettings.ShowTaskStatusDot;
            startup.AutoSize = true;
            startup.Text = "随 Windows 登录启动";
            startup.Checked = StartupRegistration.Enabled;

            Label help = new Label();
            help.AutoSize = true;
            help.MaximumSize = new Size(420, 0);
            help.Text = "任务状态数据源尚未接入。开启后仅显示灰色预留点。";

            Button save = new Button();
            save.AutoSize = true;
            save.Text = "保存";
            save.Click += Save;
            Button cancel = new Button();
            cancel.AutoSize = true;
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false;
            panel.Padding = new Padding(18);
            panel.Controls.Add(showDot);
            panel.Controls.Add(startup);
            panel.Controls.Add(help);
            panel.Controls.Add(buttons);
            Controls.Add(panel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void Save(object sender, EventArgs eventArgs)
        {
            try
            {
                MeterSettings.ShowTaskStatusDot = showDot.Checked;
                StartupRegistration.SetEnabled(startup.Checked);
                if (saved != null) saved();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "无法保存设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly Timer timer = new Timer();
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private AppServerClient client;
        private string desktopPath;
        private QuotaWindow quota;
        private DateTime? quotaUpdatedAt;
        private DateTime nextQuotaRefresh = DateTime.MinValue;
        private Icon currentIcon;
        private string quotaError;
        private bool polling;
        private bool exiting;

        public TrayApplicationContext()
        {
            StartupRegistration.EnsureDefault();
            menu.Font = SystemFonts.MenuFont;
            menu.Renderer = new ToolStripSystemRenderer();
            menu.Opening += delegate { RebuildMenu(); };
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = false;
            timer.Interval = 2000;
            timer.Tick += OnTimerTick;
            timer.Start();
        }

        private async void OnTimerTick(object sender, EventArgs eventArgs)
        {
            await PollAsync(false);
        }

        private async Task PollAsync(bool forceQuota)
        {
            if (polling || exiting) return;
            polling = true;
            try
            {
                string found = CodexProcessMonitor.FindOfficialPath();
                if (found == null)
                {
                    Deactivate();
                    return;
                }
                if (client == null || !string.Equals(found, desktopPath, StringComparison.OrdinalIgnoreCase))
                    Activate(found);
                if (client == null)
                {
                    quotaError = "未找到 Codex CLI";
                    UpdateIcon();
                    return;
                }

                if (forceQuota || DateTime.UtcNow >= nextQuotaRefresh)
                {
                    nextQuotaRefresh = DateTime.UtcNow.AddSeconds(60);
                    try
                    {
                        quota = await client.ReadQuotaAsync();
                        quotaUpdatedAt = DateTime.UtcNow;
                        quotaError = null;
                    }
                    catch (Exception error)
                    {
                        quota = null;
                        quotaUpdatedAt = null;
                        quotaError = SafeMessage(error);
                    }
                }
                if (quotaUpdatedAt.HasValue && DateTime.UtcNow - quotaUpdatedAt.Value > TimeSpan.FromMinutes(5))
                    quota = null;
                UpdateIcon();
            }
            finally { polling = false; }
        }

        private void Activate(string path)
        {
            Deactivate();
            desktopPath = path;
            string cliPath = CodexProcessMonitor.FindCliPath(path);
            if (cliPath != null) client = new AppServerClient(cliPath);
            notifyIcon.Visible = true;
            nextQuotaRefresh = DateTime.MinValue;
            UpdateIcon();
        }

        private void Deactivate()
        {
            notifyIcon.Visible = false;
            if (client != null) client.Dispose();
            client = null;
            desktopPath = null;
            quota = null;
            quotaUpdatedAt = null;
            quotaError = null;
            if (currentIcon != null) currentIcon.Dispose();
            currentIcon = null;
        }

        private void UpdateIcon()
        {
            if (!notifyIcon.Visible) return;
            string text = TrayText.Format(quota == null ? (int?)null : quota.RemainingPercent);
            Icon next = TrayIconRenderer.Render(text, MeterSettings.ShowTaskStatusDot);
            notifyIcon.Icon = next;
            Icon old = currentIcon;
            currentIcon = next;
            if (old != null) old.Dispose();
            notifyIcon.Text = LimitTooltip(BuildTooltip());
        }

        private string BuildTooltip()
        {
            string quotaText = quota == null ? "额度不可用" : KindText(quota.Kind) + "额度剩余 " + quota.RemainingPercent + "%";
            return "Codex " + quotaText;
        }

        private void RebuildMenu()
        {
            menu.Items.Clear();
            ToolStripMenuItem header = Info("Codex 额度");
            header.Font = new Font(header.Font, FontStyle.Bold);
            menu.Items.Add(header);
            if (quota == null)
                menu.Items.Add(Info("额度：暂不可用" + ErrorSuffix(quotaError)));
            else
            {
                menu.Items.Add(Info("额度：" + KindText(quota.Kind) + "剩余 " + quota.RemainingPercent + "%"));
                menu.Items.Add(Info("重置：" + (quota.ResetsAt.HasValue
                    ? quota.ResetsAt.Value.ToLocalTime().ToString("yyyy/M/d HH:mm") : "未知")));
            }
            menu.Items.Add(Info("更新：" + RelativeTime(quotaUpdatedAt)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Action("打开 Codex", delegate { OpenCodex(); }));
            menu.Items.Add(Action("刷新", async delegate { nextQuotaRefresh = DateTime.MinValue; await PollAsync(true); }));
            menu.Items.Add(Action("设置…", delegate { ShowSettings(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Action("退出 CodexMenuMeter", delegate { ExitThread(); }));
        }

        private void ShowSettings()
        {
            using (SettingsForm settings = new SettingsForm(UpdateIcon)) settings.ShowDialog();
        }

        private void OpenCodex()
        {
            if (desktopPath == null) return;
            try { Process.Start(new ProcessStartInfo(desktopPath) { UseShellExecute = true }); }
            catch { }
        }

        protected override void ExitThreadCore()
        {
            exiting = true;
            timer.Stop();
            Deactivate();
            notifyIcon.Dispose();
            menu.Dispose();
            timer.Dispose();
            base.ExitThreadCore();
        }

        private static ToolStripMenuItem Info(string text)
        {
            return new ToolStripMenuItem(text) { Enabled = false };
        }

        private static ToolStripMenuItem Action(string text, EventHandler action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += action;
            return item;
        }

        private static string KindText(QuotaKind kind)
        {
            return kind == QuotaKind.FiveHour ? "5 小时" : "一周";
        }

        private static string RelativeTime(DateTime? time)
        {
            if (!time.HasValue) return "尚未更新";
            TimeSpan elapsed = DateTime.UtcNow - time.Value;
            if (elapsed.TotalMinutes < 1) return "刚刚";
            return (int)elapsed.TotalMinutes + " 分钟前";
        }

        private static string SafeMessage(Exception error)
        {
            string message = error == null ? null : error.Message;
            return string.IsNullOrWhiteSpace(message) ? "读取失败" : message.Replace("\r", " ").Replace("\n", " ");
        }

        private static string ErrorSuffix(string error)
        {
            return string.IsNullOrEmpty(error) ? "" : "（" + error + "）";
        }

        private static string LimitTooltip(string text)
        {
            return text.Length <= 120 ? text : text.Substring(0, 120);
        }
    }
}

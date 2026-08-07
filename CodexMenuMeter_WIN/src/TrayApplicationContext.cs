using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
                new Rectangle(dot + edge * 2, edge, size - dot - edge * 3, size - edge * 2));
        }
    }

    internal static class MeterSettings
    {
        public static bool ParseShowTaskStatusDot(object value)
        {
            return value is int && (int)value != 0;
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

    internal static class TrayIconRenderer
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon Render(string text, TaskColor color)
        {
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.Clear(Color.Transparent);
                using (Brush background = new SolidBrush(Background(color)))
                    graphics.FillRoundedRectangle(background, new RectangleF(1, 1, 62, 62), 12);

                string number = text.EndsWith("%", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
                float numberSize = number.Length >= 3 ? 29 : 35;
                using (Font numberFont = new Font("Arial Narrow", numberSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font percentFont = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush foreground = new SolidBrush(Color.White))
                {
                    RectangleF numberBounds = new RectangleF(2, 8, 51, 51);
                    StringFormat center = new StringFormat();
                    center.Alignment = StringAlignment.Center;
                    center.LineAlignment = StringAlignment.Center;
                    graphics.DrawString(number, numberFont, foreground, numberBounds, center);
                    center.Dispose();
                    graphics.DrawString("%", percentFont, foreground, new PointF(45, 5));
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        private static Color Background(TaskColor color)
        {
            switch (color)
            {
                case TaskColor.Red: return Color.FromArgb(197, 34, 31);
                case TaskColor.Yellow: return Color.FromArgb(217, 155, 0);
                case TaskColor.Green: return Color.FromArgb(24, 128, 56);
                default: return Color.FromArgb(95, 99, 104);
            }
        }

        private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.FillPath(brush, path);
            }
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly Timer timer = new Timer();
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly TaskTracker taskTracker = new TaskTracker();
        private AppServerClient client;
        private string desktopPath;
        private QuotaWindow quota;
        private DateTime? quotaUpdatedAt;
        private DateTime nextQuotaRefresh = DateTime.MinValue;
        private TaskAggregate taskState = new TaskAggregate(TaskColor.Gray, new List<TaskSummary>());
        private Icon currentIcon;
        private string quotaError;
        private string taskError;
        private bool polling;
        private bool exiting;

        public TrayApplicationContext()
        {
            StartupRegistration.EnsureDefault();
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
                    taskState = taskTracker.Update(new TaskSummary[0], false);
                    taskError = "未找到 Codex CLI";
                    UpdateIcon();
                    return;
                }

                taskState = taskTracker.Update(new TaskSummary[0], false);
                taskError = "当前 Codex 桌面版未共享任务状态";

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
            taskError = null;
            taskTracker.Reset();
            taskState = new TaskAggregate(TaskColor.Gray, new List<TaskSummary>());
            if (currentIcon != null) currentIcon.Dispose();
            currentIcon = null;
        }

        private void UpdateIcon()
        {
            if (!notifyIcon.Visible) return;
            string percent = TrayText.Format(quota == null ? (int?)null : quota.RemainingPercent);
            Icon next = TrayIconRenderer.Render(percent, taskState.Color);
            notifyIcon.Icon = next;
            Icon old = currentIcon;
            currentIcon = next;
            if (old != null) old.Dispose();
            notifyIcon.Text = LimitTooltip(BuildTooltip());
        }

        private string BuildTooltip()
        {
            string quotaText = quota == null ? "额度不可用" : KindText(quota.Kind) + "额度剩余 " + quota.RemainingPercent + "%";
            return "Codex " + quotaText + " · " + AggregateText(taskState);
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
            menu.Items.Add(Info("任务：" + AggregateText(taskState) + ErrorSuffix(taskError)));
            foreach (TaskSummary task in taskState.Tasks)
                menu.Items.Add(Info(task.Name + " — " + StateText(task.State) + " · " + Elapsed(task.ObservedStart)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Action("打开 Codex", delegate { OpenCodex(); }));
            menu.Items.Add(Action("刷新", async delegate { nextQuotaRefresh = DateTime.MinValue; await PollAsync(true); }));
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem startup = Action("随 Windows 登录启动", delegate {
                StartupRegistration.SetEnabled(!StartupRegistration.Enabled);
            });
            startup.Checked = StartupRegistration.Enabled;
            menu.Items.Add(startup);
            menu.Items.Add(Action("退出 CodexMenuMeter", delegate { ExitThread(); }));
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

        private static string AggregateText(TaskAggregate aggregate)
        {
            switch (aggregate.Color)
            {
                case TaskColor.Red: return aggregate.Tasks.Count + " 个任务需要处理";
                case TaskColor.Yellow: return aggregate.Tasks.Count + " 个任务运行中";
                case TaskColor.Green: return "当前无运行任务";
                default: return "状态未知";
            }
        }

        private static string StateText(TaskState state)
        {
            switch (state)
            {
                case TaskState.WaitingForApproval: return "等待授权";
                case TaskState.WaitingForInput: return "等待输入";
                case TaskState.SystemError: return "系统错误";
                default: return "运行中";
            }
        }

        private static string Elapsed(DateTime? start)
        {
            if (!start.HasValue) return "刚刚";
            TimeSpan elapsed = DateTime.UtcNow - start.Value;
            if (elapsed.TotalMinutes < 1) return "不到 1 分钟";
            if (elapsed.TotalHours < 1) return (int)elapsed.TotalMinutes + " 分钟";
            return (int)elapsed.TotalHours + " 小时 " + elapsed.Minutes + " 分钟";
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

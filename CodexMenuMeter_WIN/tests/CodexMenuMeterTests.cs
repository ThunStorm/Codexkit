using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace CodexMenuMeter
{
    internal static class Tests
    {
        private static int passed;

        public static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--live") return RunLive();
            try
            {
                AssertEqual(69, QuotaSelector.Remaining(31), "remaining percent");
                AssertEqual(100, QuotaSelector.Remaining(0), "zero used");
                AssertEqual(0, QuotaSelector.Remaining(100), "fully used");
                AssertEqual(null, QuotaSelector.Remaining(101), "invalid percent");
                AssertEqual(null, QuotaSelector.Remaining(double.NaN), "nan percent");
                AssertEqual(null, QuotaSelector.Remaining(double.PositiveInfinity), "infinite percent");

                QuotaWindow selected = QuotaSelector.Select(new[] {
                    new QuotaWindow(QuotaKind.Weekly, 29, 10080, null),
                    new QuotaWindow(QuotaKind.FiveHour, 31, 300, null)
                });
                AssertEqual(QuotaKind.FiveHour, selected.Kind, "five hour wins");
                AssertEqual(QuotaKind.Weekly, QuotaSelector.Select(new[] {
                    new QuotaWindow(QuotaKind.Weekly, 29, 10080, null)
                }).Kind, "weekly fallback");
                AssertEqual(null, QuotaSelector.Select(new[] {
                    new QuotaWindow(QuotaKind.Other, 29, 60, null)
                }), "unknown window ignored");

                TaskTracker tracker = new TaskTracker();
                AssertEqual(TaskColor.Yellow, tracker.Update(new[] {
                    new TaskSummary("1", "Build", TaskState.Active, null, DateTime.UtcNow)
                }, true).Color, "active is yellow");
                AssertEqual(TaskColor.Green, tracker.Update(new TaskSummary[0], true).Color,
                    "observed active then idle is green");
                AssertEqual(TaskColor.Gray, tracker.Update(new TaskSummary[0], false).Color,
                    "lost source is gray");

                tracker = new TaskTracker();
                AssertEqual(TaskColor.Red, tracker.Update(new[] {
                    new TaskSummary("1", "Run", TaskState.Active, null, DateTime.UtcNow),
                    new TaskSummary("2", "Approve", TaskState.WaitingForApproval, null, DateTime.UtcNow)
                }, true).Color, "red beats yellow");

                TaskSummary[] sixTasks = new TaskSummary[6];
                for (int i = 0; i < sixTasks.Length; i++)
                    sixTasks[i] = new TaskSummary(i.ToString(), i == 0 ? "" : "Task", TaskState.Active, null, DateTime.UtcNow);
                TaskAggregate capped = tracker.Update(sixTasks, true);
                AssertEqual(5, capped.Tasks.Count, "task rows capped");
                AssertEqual("未命名任务", capped.Tasks[0].Name, "empty name fallback");

                AssertEqual(true, AppServerResponseParser.HasAccount(
                    "{\"result\":{\"account\":{\"type\":\"chatgpt\"},\"requiresOpenaiAuth\":true}}"),
                    "signed in account with auth requirement");
                AssertEqual(false, AppServerResponseParser.HasAccount(
                    "{\"result\":{\"account\":null,\"requiresOpenaiAuth\":true}}"),
                    "missing account is signed out");

                QuotaWindow parsedQuota = AppServerResponseParser.ParseQuota(
                    "{\"result\":{\"rateLimits\":{" +
                    "\"primary\":{\"usedPercent\":29,\"windowDurationMins\":10080}," +
                    "\"secondary\":{\"usedPercent\":31,\"windowDurationMins\":300,\"resetsAt\":1786167266}}}}" );
                AssertEqual(QuotaKind.FiveHour, parsedQuota.Kind, "parsed five hour wins");
                AssertEqual(69, parsedQuota.RemainingPercent, "parsed remaining percent");
                AssertEqual(1786167266D, parsedQuota.ResetsAt.Value.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                    "numeric reset time");

                TaskSummary[] parsedTasks = AppServerResponseParser.ParseTasks(
                    "{\"result\":{\"data\":[" +
                    "{\"id\":\"a\",\"name\":\"Fix build\",\"updatedAt\":1780000000," +
                    "\"status\":{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}}," +
                    "{\"id\":\"b\",\"name\":null,\"updatedAt\":1780000001," +
                    "\"status\":{\"type\":\"active\",\"activeFlags\":[]}}," +
                    "{\"id\":\"c\",\"updatedAt\":1780000002," +
                    "\"status\":{\"type\":\"idle\"}}]}}");
                AssertEqual(3, parsedTasks.Length, "thread list parsed");
                AssertEqual(TaskState.WaitingForApproval, parsedTasks[0].State, "approval maps red");
                AssertEqual(TaskState.Active, parsedTasks[1].State, "active maps yellow");
                AssertEqual("未命名任务", parsedTasks[1].Name, "missing official name fallback");
                AssertEqual(TaskState.Idle, parsedTasks[2].State, "idle stays idle");

                AssertEqual(0, AppServerResponseParser.ParseTasks(
                    "{\"method\":\"unknown/notification\",\"params\":{}}").Length,
                    "unknown notification ignored");

                AssertTrue(CodexProcessMonitor.IsOfficial("ChatGPT",
                    @"C:\Program Files\WindowsApps\OpenAI.Codex_26.803.5235.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe"),
                    "official desktop process");
                AssertFalse(CodexProcessMonitor.IsOfficial("codex",
                    @"C:\Program Files\WindowsApps\OpenAI.Codex_26.803.5235.0_x64__2p2nqsd0c76g0\app\resources\codex.exe"),
                    "CLI is excluded");
                AssertFalse(CodexProcessMonitor.IsOfficial("ChatGPT", @"C:\Tools\ChatGPT.exe"),
                    "unrelated ChatGPT process excluded");
                AssertEqual("69", TrayText.Format(69), "quota omits percent sign");
                AssertEqual("--", TrayText.Format(null), "unknown quota omits percent sign");
                AssertFalse(MeterSettings.ParseShowTaskStatusDot(null), "status dot defaults off");
                AssertTrue(MeterSettings.ParseShowTaskStatusDot(1), "status dot persisted on");

                TrayLayout plain = TrayLayout.Calculate(24, false);
                AssertEqual(Rectangle.Empty, plain.DotBounds, "hidden dot has no bounds");
                AssertTrue(Contains(new Rectangle(0, 0, 24, 24), plain.NumberBounds), "plain number fits icon");
                AssertEqual("16,20,24,32,40,48,64", string.Join(",", TrayDpi.SupportedSizes), "supported icon frames");
                AssertEqual(16, TrayDpi.ClosestSupportedSize(17), "17px selects 16px frame");
                AssertEqual(24, TrayDpi.ClosestSupportedSize(23), "23px selects 24px frame");
                AssertEqual(40, TrayDpi.ClosestSupportedSize(36), "tie selects larger frame");

                TrayLayout percent = TrayLayout.Calculate(16, false);
                AssertTrue(percent.PercentBounds.Width >= 6, "percent remains readable at 16px");
                AssertFalse(percent.NumberBounds.IntersectsWith(percent.PercentBounds), "number avoids percent");
                AssertTrue(Contains(new Rectangle(0, 0, 16, 16), percent.PercentBounds), "percent fits icon");
                TrayLayout dotted = TrayLayout.Calculate(24, true);
                AssertTrue(dotted.DotBounds.Width > 0, "shown dot reserves pixels");
                AssertTrue(Contains(new Rectangle(0, 0, 24, 24), dotted.NumberBounds), "dotted number fits icon");
                AssertTrue(Contains(new Rectangle(0, 0, 24, 24), dotted.PercentBounds), "dotted percent fits icon");
                AssertFalse(dotted.DotBounds.IntersectsWith(dotted.NumberBounds), "dot and number do not overlap");
                AssertFalse(dotted.DotBounds.IntersectsWith(dotted.PercentBounds), "dot and percent do not overlap");
                AssertFalse(dotted.NumberBounds.IntersectsWith(dotted.PercentBounds), "number and percent do not overlap");

                using (System.Drawing.Icon rendered = TrayIconRenderer.Render("39", false))
                using (Bitmap renderedBitmap = rendered.ToBitmap())
                {
                    TrayLayout renderedLayout = TrayLayout.Calculate(renderedBitmap.Width, false);
                    AssertTrue(HasVisiblePixel(renderedBitmap, renderedLayout.NumberBounds), "number draws visible pixels");
                    Rectangle percentInterior = new Rectangle(renderedLayout.PercentBounds.X + 1,
                        renderedLayout.PercentBounds.Y + 1,
                        renderedLayout.PercentBounds.Width - 2,
                        renderedLayout.PercentBounds.Height - 2);
                    AssertTrue(HasVisiblePixel(renderedBitmap, percentInterior), "percent draws visible interior pixels");
                }
                foreach (int iconSize in TrayDpi.SupportedSizes)
                    using (System.Drawing.Icon rendered = TrayIconRenderer.Render("39", false, iconSize))
                        AssertEqual(iconSize, rendered.Width, iconSize + "px renderer frame");

                TaskTracker timedTracker = new TaskTracker();
                TaskAggregate timedFirst = timedTracker.Update(new[] {
                    new TaskSummary("timed", "Timed", TaskState.Active, null, DateTime.UtcNow)
                }, true);
                AssertTrue(timedFirst.Tasks[0].ObservedStart.HasValue, "active task gets observed start");
                DateTime? firstObserved = timedFirst.Tasks[0].ObservedStart;
                TaskAggregate timedSecond = timedTracker.Update(new[] {
                    new TaskSummary("timed", "Timed", TaskState.Active, null, DateTime.UtcNow.AddSeconds(1))
                }, true);
                AssertEqual(firstObserved, timedSecond.Tasks[0].ObservedStart, "observed start remains stable");

                string cliTestRoot = Path.Combine(Path.GetTempPath(), "CodexMenuMeterTests-" + Guid.NewGuid().ToString("N"));
                try
                {
                    string localAppData = Path.Combine(cliTestRoot, "Local");
                    string cachedCli = Path.Combine(localAppData, "OpenAI", "Codex", "bin", "current", "codex.exe");
                    string desktopCli = Path.Combine(cliTestRoot, "WindowsApps", "OpenAI.Codex_1", "app", "resources", "codex.exe");
                    Directory.CreateDirectory(Path.GetDirectoryName(cachedCli));
                    Directory.CreateDirectory(Path.GetDirectoryName(desktopCli));
                    File.WriteAllText(cachedCli, "cached");
                    File.WriteAllText(desktopCli, "packaged");
                    AssertEqual(cachedCli, CodexProcessMonitor.FindCliPath(
                        Path.Combine(cliTestRoot, "WindowsApps", "OpenAI.Codex_1", "app", "ChatGPT.exe"),
                        localAppData,
                        ""), "user-local CLI beats packaged CLI");
                }
                finally
                {
                    if (Directory.Exists(cliTestRoot)) Directory.Delete(cliTestRoot, true);
                }

                Console.WriteLine("PASS " + passed + " tests");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL " + error.Message);
                return 1;
            }
        }

        private static int RunLive()
        {
            string desktopPath = CodexProcessMonitor.FindOfficialPath();
            string cliPath = CodexProcessMonitor.FindCliPath(desktopPath);
            if (desktopPath == null || cliPath == null)
            {
                Console.Error.WriteLine("LIVE FAIL: official Codex desktop or CLI not found");
                return 1;
            }

            using (AppServerClient client = new AppServerClient(cliPath))
            {
                try
                {
                    QuotaWindow quota = client.ReadQuotaAsync().GetAwaiter().GetResult();
                    Console.WriteLine("LIVE quota: {0}% remaining ({1} minutes)",
                        quota.RemainingPercent, quota.DurationMinutes);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("LIVE quota unavailable: " + error.Message);
                    return 1;
                }

                try
                {
                    IList<TaskSummary> tasks = client.ReadTasksAsync().GetAwaiter().GetResult();
                    Console.WriteLine("LIVE independent app-server active tasks: " + tasks.Count);
                }
                catch (Exception error)
                {
                    Console.WriteLine("LIVE task status unavailable: " + error.Message);
                }
            }
            return 0;
        }

        private static void AssertEqual(object expected, object actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + ": expected " + Format(expected) + ", got " + Format(actual));
            passed++;
        }

        private static void AssertTrue(bool actual, string name)
        {
            AssertEqual(true, actual, name);
        }

        private static void AssertFalse(bool actual, string name)
        {
            AssertEqual(false, actual, name);
        }

        private static bool Contains(Rectangle outer, Rectangle inner)
        {
            return inner.Left >= outer.Left && inner.Top >= outer.Top
                && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
        }

        private static bool HasVisiblePixel(Bitmap bitmap, Rectangle bounds)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
                for (int x = bounds.Left; x < bounds.Right; x++)
                    if (bitmap.GetPixel(x, y).A != 0) return true;
            return false;
        }

        private static string Format(object value)
        {
            return value == null ? "<null>" : value.ToString();
        }
    }
}

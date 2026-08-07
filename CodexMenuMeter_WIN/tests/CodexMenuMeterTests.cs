using System;

namespace CodexMenuMeter
{
    internal static class Tests
    {
        private static int passed;

        public static int Main()
        {
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

                Console.WriteLine("PASS " + passed + " tests");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL " + error.Message);
                return 1;
            }
        }

        private static void AssertEqual(object expected, object actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + ": expected " + Format(expected) + ", got " + Format(actual));
            passed++;
        }

        private static string Format(object value)
        {
            return value == null ? "<null>" : value.ToString();
        }
    }
}

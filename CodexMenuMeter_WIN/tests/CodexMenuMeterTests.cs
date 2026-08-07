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

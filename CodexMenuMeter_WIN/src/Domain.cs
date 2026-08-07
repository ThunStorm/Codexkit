using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexMenuMeter
{
    internal enum QuotaKind { FiveHour, Weekly, Other }

    internal sealed class QuotaWindow
    {
        public readonly QuotaKind Kind;
        public readonly double UsedPercent;
        public readonly int DurationMinutes;
        public readonly DateTime? ResetsAt;

        public QuotaWindow(QuotaKind kind, double usedPercent, int durationMinutes, DateTime? resetsAt)
        {
            Kind = kind;
            UsedPercent = usedPercent;
            DurationMinutes = durationMinutes;
            ResetsAt = resetsAt;
        }

        public int RemainingPercent { get { return QuotaSelector.Remaining(UsedPercent).Value; } }
    }

    internal static class QuotaSelector
    {
        public static QuotaKind KindFor(int minutes)
        {
            if (minutes >= 240 && minutes <= 360) return QuotaKind.FiveHour;
            if (minutes >= 9000 && minutes <= 11000) return QuotaKind.Weekly;
            return QuotaKind.Other;
        }

        public static int? Remaining(double usedPercent)
        {
            if (double.IsNaN(usedPercent) || double.IsInfinity(usedPercent) || usedPercent < 0 || usedPercent > 100)
                return null;
            return (int)Math.Round(100 - usedPercent, MidpointRounding.AwayFromZero);
        }

        public static QuotaWindow Select(IEnumerable<QuotaWindow> windows)
        {
            if (windows == null) return null;
            List<QuotaWindow> valid = windows.Where(delegate(QuotaWindow window) {
                return window != null && Remaining(window.UsedPercent).HasValue;
            }).ToList();
            return valid.FirstOrDefault(delegate(QuotaWindow window) { return window.Kind == QuotaKind.FiveHour; })
                ?? valid.FirstOrDefault(delegate(QuotaWindow window) { return window.Kind == QuotaKind.Weekly; });
        }
    }

    internal enum TaskState { Active, WaitingForApproval, WaitingForInput, SystemError, Idle }
    internal enum TaskColor { Gray, Yellow, Green, Red }

    internal sealed class TaskSummary
    {
        public readonly string Id;
        public readonly string Name;
        public readonly TaskState State;
        public readonly DateTime? ObservedStart;
        public readonly DateTime UpdatedAt;

        public TaskSummary(string id, string name, TaskState state, DateTime? observedStart, DateTime updatedAt)
        {
            Id = id ?? "";
            Name = string.IsNullOrWhiteSpace(name) ? "未命名任务" : name.Trim();
            State = state;
            ObservedStart = observedStart;
            UpdatedAt = updatedAt;
        }
    }

    internal sealed class TaskAggregate
    {
        public readonly TaskColor Color;
        public readonly IList<TaskSummary> Tasks;

        public TaskAggregate(TaskColor color, IList<TaskSummary> tasks)
        {
            Color = color;
            Tasks = tasks;
        }
    }

    internal sealed class TaskTracker
    {
        private bool sawActive;
        private readonly Dictionary<string, DateTime> observedStarts = new Dictionary<string, DateTime>();

        public TaskAggregate Update(IEnumerable<TaskSummary> tasks, bool sourceAvailable)
        {
            if (!sourceAvailable)
            {
                Reset();
                return new TaskAggregate(TaskColor.Gray, new List<TaskSummary>());
            }

            List<TaskSummary> current = (tasks ?? Enumerable.Empty<TaskSummary>())
                .Where(delegate(TaskSummary task) { return task != null && task.State != TaskState.Idle; })
                .ToList();
            HashSet<string> currentIds = new HashSet<string>(current.Select(delegate(TaskSummary task) { return task.Id; }));
            foreach (string endedId in observedStarts.Keys.Where(delegate(string id) { return !currentIds.Contains(id); }).ToList())
                observedStarts.Remove(endedId);
            DateTime now = DateTime.UtcNow;
            current = current.Select(delegate(TaskSummary task) {
                DateTime start;
                if (!observedStarts.TryGetValue(task.Id, out start))
                {
                    start = task.ObservedStart ?? now;
                    observedStarts[task.Id] = start;
                }
                return new TaskSummary(task.Id, task.Name, task.State, start, task.UpdatedAt);
            }).ToList();

            List<TaskSummary> active = current
                .OrderBy(delegate(TaskSummary task) { return Priority(task.State); })
                .ThenByDescending(delegate(TaskSummary task) { return task.UpdatedAt; })
                .Take(5)
                .ToList();

            bool hasRed = active.Any(delegate(TaskSummary task) {
                return task.State == TaskState.WaitingForApproval
                    || task.State == TaskState.WaitingForInput
                    || task.State == TaskState.SystemError;
            });
            if (active.Count > 0)
            {
                sawActive = true;
                return new TaskAggregate(hasRed ? TaskColor.Red : TaskColor.Yellow, active);
            }
            return new TaskAggregate(sawActive ? TaskColor.Green : TaskColor.Gray, active);
        }

        public void Reset()
        {
            sawActive = false;
            observedStarts.Clear();
        }

        private static int Priority(TaskState state)
        {
            return state == TaskState.WaitingForApproval || state == TaskState.WaitingForInput || state == TaskState.SystemError ? 0 : 1;
        }
    }
}

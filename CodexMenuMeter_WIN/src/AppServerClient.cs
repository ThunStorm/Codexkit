using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexMenuMeter
{
    internal static class AppServerResponseParser
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static bool HasAccount(string json)
        {
            IDictionary<string, object> result = Result(json);
            object account;
            return result != null && result.TryGetValue("account", out account) && account != null;
        }

        public static QuotaWindow ParseQuota(string json)
        {
            IDictionary<string, object> result = Result(json);
            if (result == null) return null;

            List<QuotaWindow> windows = new List<QuotaWindow>();
            object direct;
            if (result.TryGetValue("rateLimits", out direct)) AddSnapshot(windows, AsObject(direct));

            object bucketsValue;
            IDictionary<string, object> buckets;
            if (result.TryGetValue("rateLimitsByLimitId", out bucketsValue)
                && (buckets = AsObject(bucketsValue)) != null)
            {
                foreach (object snapshot in buckets.Values) AddSnapshot(windows, AsObject(snapshot));
            }
            return QuotaSelector.Select(windows);
        }

        public static TaskSummary[] ParseTasks(string json)
        {
            IDictionary<string, object> result = Result(json);
            if (result == null) return new TaskSummary[0];
            object dataValue;
            if (!result.TryGetValue("data", out dataValue)) return new TaskSummary[0];

            List<TaskSummary> tasks = new List<TaskSummary>();
            foreach (object value in AsArray(dataValue))
            {
                IDictionary<string, object> thread = AsObject(value);
                if (thread == null) continue;
                IDictionary<string, object> status = GetObject(thread, "status");
                string type = GetString(status, "type");
                TaskState state = TaskState.Idle;
                if (string.Equals(type, "systemError", StringComparison.OrdinalIgnoreCase))
                    state = TaskState.SystemError;
                else if (string.Equals(type, "active", StringComparison.OrdinalIgnoreCase))
                    state = MapActiveFlags(status);

                tasks.Add(new TaskSummary(
                    GetString(thread, "id"),
                    GetString(thread, "name"),
                    state,
                    null,
                    GetDate(thread, "updatedAt") ?? DateTime.UtcNow));
            }
            return tasks.ToArray();
        }

        public static string ErrorMessage(string json)
        {
            IDictionary<string, object> root = ParseObject(json);
            IDictionary<string, object> error = root == null ? null : GetObject(root, "error");
            return GetString(error, "message");
        }

        private static TaskState MapActiveFlags(IDictionary<string, object> status)
        {
            object flagsValue;
            if (status != null && status.TryGetValue("activeFlags", out flagsValue))
            {
                foreach (object flagValue in AsArray(flagsValue))
                {
                    string flag = Convert.ToString(flagValue, CultureInfo.InvariantCulture) ?? "";
                    if (flag.IndexOf("approval", StringComparison.OrdinalIgnoreCase) >= 0)
                        return TaskState.WaitingForApproval;
                    if (flag.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0)
                        return TaskState.WaitingForInput;
                }
            }
            return TaskState.Active;
        }

        private static void AddSnapshot(ICollection<QuotaWindow> windows, IDictionary<string, object> snapshot)
        {
            if (snapshot == null) return;
            object primary;
            object secondary;
            if (snapshot.TryGetValue("primary", out primary)) AddWindow(windows, AsObject(primary));
            if (snapshot.TryGetValue("secondary", out secondary)) AddWindow(windows, AsObject(secondary));
        }

        private static void AddWindow(ICollection<QuotaWindow> windows, IDictionary<string, object> raw)
        {
            if (raw == null) return;
            double? used = GetDouble(raw, "usedPercent");
            int? duration = GetInt(raw, "windowDurationMins");
            if (!used.HasValue || !duration.HasValue || !QuotaSelector.Remaining(used.Value).HasValue) return;
            windows.Add(new QuotaWindow(
                QuotaSelector.KindFor(duration.Value), used.Value, duration.Value, GetDate(raw, "resetsAt")));
        }

        private static IDictionary<string, object> Result(string json)
        {
            IDictionary<string, object> root = ParseObject(json);
            return root == null ? null : GetObject(root, "result");
        }

        private static IDictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return Json.DeserializeObject(json) as IDictionary<string, object>; }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        private static IDictionary<string, object> AsObject(object value)
        {
            return value as IDictionary<string, object>;
        }

        private static IEnumerable<object> AsArray(object value)
        {
            object[] array = value as object[];
            if (array != null) return array;
            ArrayList list = value as ArrayList;
            return list == null ? Enumerable.Empty<object>() : list.Cast<object>();
        }

        private static IDictionary<string, object> GetObject(IDictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? AsObject(value) : null;
        }

        private static string GetString(IDictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        private static double? GetDouble(IDictionary<string, object> source, string key)
        {
            object value;
            double parsed;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return null;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed) ? parsed : (double?)null;
        }

        private static int? GetInt(IDictionary<string, object> source, string key)
        {
            double? value = GetDouble(source, key);
            return value.HasValue ? Convert.ToInt32(value.Value) : (int?)null;
        }

        private static DateTime? GetDate(IDictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return null;
            double seconds;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out seconds)) return Epoch.AddSeconds(seconds);
            DateTime parsed;
            return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
        }
    }

    internal sealed class AppServerClient : IDisposable
    {
        private readonly string executablePath;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly object sync = new object();
        private readonly object writeSync = new object();
        private readonly Dictionary<int, TaskCompletionSource<string>> pending = new Dictionary<int, TaskCompletionSource<string>>();
        private readonly SemaphoreSlim initializeLock = new SemaphoreSlim(1, 1);
        private Process process;
        private StreamWriter input;
        private int nextId;
        private bool initialized;
        private bool disposed;

        public AppServerClient(string executablePath)
        {
            this.executablePath = executablePath;
        }

        public Task StartAsync()
        {
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException("AppServerClient");
                if (process != null && !process.HasExited) return Task.FromResult(0);
                if (input != null) input.Dispose();
                if (process != null) process.Dispose();
                input = null;
                process = null;

                ProcessStartInfo start = new ProcessStartInfo(executablePath, "app-server --listen stdio://");
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardInput = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
                string[] sensitive = { "OPENAI_API_KEY", "CODEX_API_KEY", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN" };
                foreach (string name in sensitive) start.EnvironmentVariables.Remove(name);

                process = new Process();
                process.StartInfo = start;
                process.EnableRaisingEvents = true;
                process.Exited += delegate { FailPending(new IOException("Codex app-server exited.")); };
                process.ErrorDataReceived += delegate { };
                if (!process.Start()) throw new IOException("Unable to start Codex app-server.");
                input = process.StandardInput;
                input.AutoFlush = true;
                process.BeginErrorReadLine();
                Task.Run((Func<Task>)ReadLoopAsync);
                return Task.FromResult(0);
            }
        }

        public async Task InitializeAsync()
        {
            await initializeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (initialized) return;
                await StartAsync().ConfigureAwait(false);
                await RequestAsync("initialize", new {
                    clientInfo = new { name = "CodexMenuMeter", version = "0.1.0" },
                    capabilities = new { experimentalApi = false }
                }, 12000).ConfigureAwait(false);
                Send(new Dictionary<string, object> { { "method", "initialized" } });
                initialized = true;
            }
            finally { initializeLock.Release(); }
        }

        public async Task<string> RequestAsync(string method, object parameters, int timeoutMilliseconds)
        {
            await StartAsync().ConfigureAwait(false);
            int id = Interlocked.Increment(ref nextId);
            TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
            lock (sync) pending[id] = completion;

            Dictionary<string, object> message = new Dictionary<string, object>();
            message["id"] = id;
            message["method"] = method;
            if (parameters != null) message["params"] = parameters;
            try { Send(message); }
            catch
            {
                lock (sync) pending.Remove(id);
                throw;
            }

            Task winner = await Task.WhenAny(completion.Task, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false);
            if (winner != completion.Task)
            {
                lock (sync) pending.Remove(id);
                throw new TimeoutException("Codex app-server request timed out: " + method);
            }
            string response = await completion.Task.ConfigureAwait(false);
            string error = AppServerResponseParser.ErrorMessage(response);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            return response;
        }

        public async Task<QuotaWindow> ReadQuotaAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            string account = await RequestAsync("account/read", new { refreshToken = false }, 12000).ConfigureAwait(false);
            if (!AppServerResponseParser.HasAccount(account)) throw new InvalidOperationException("Codex 尚未登录");
            string response = await RequestAsync("account/rateLimits/read", null, 12000).ConfigureAwait(false);
            QuotaWindow window = AppServerResponseParser.ParseQuota(response);
            if (window == null) throw new InvalidOperationException("没有可显示的额度窗口");
            return window;
        }

        public async Task<IList<TaskSummary>> ReadTasksAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            string[] sources = { "appServer" };
            string response = await RequestAsync("thread/list", new {
                limit = 100,
                sortKey = "updated_at",
                sortDirection = "desc",
                sourceKinds = sources
            }, 12000).ConfigureAwait(false);
            return AppServerResponseParser.ParseTasks(response)
                .Where(delegate(TaskSummary task) { return task.State != TaskState.Idle; })
                .ToList();
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!disposed)
                {
                    string line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    IDictionary<string, object> envelope;
                    try { envelope = json.DeserializeObject(line) as IDictionary<string, object>; }
                    catch { continue; }
                    object idValue;
                    int id;
                    if (envelope == null || !envelope.TryGetValue("id", out idValue)
                        || !int.TryParse(Convert.ToString(idValue, CultureInfo.InvariantCulture), out id)) continue;
                    TaskCompletionSource<string> completion = null;
                    lock (sync)
                    {
                        if (pending.TryGetValue(id, out completion)) pending.Remove(id);
                    }
                    if (completion != null) completion.TrySetResult(line);
                }
            }
            catch (Exception error) { FailPending(error); }
        }

        private void Send(object message)
        {
            string line = json.Serialize(message);
            lock (writeSync)
            {
                if (input == null) throw new IOException("Codex app-server is not connected.");
                input.WriteLine(line);
            }
        }

        private void FailPending(Exception error)
        {
            TaskCompletionSource<string>[] completions;
            lock (sync)
            {
                completions = pending.Values.ToArray();
                pending.Clear();
                initialized = false;
            }
            foreach (TaskCompletionSource<string> completion in completions) completion.TrySetException(error);
        }

        public void Dispose()
        {
            disposed = true;
            FailPending(new ObjectDisposedException("AppServerClient"));
            lock (sync)
            {
                if (input != null) input.Dispose();
                input = null;
                if (process != null)
                {
                    try { if (!process.HasExited) process.Kill(); }
                    catch { }
                    process.Dispose();
                    process = null;
                }
            }
            initializeLock.Dispose();
        }
    }
}

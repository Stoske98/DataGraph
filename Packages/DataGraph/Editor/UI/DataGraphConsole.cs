using System;
using System.Collections.Generic;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Centralized logging system for DataGraph operations.
    /// Collects log entries grouped by graph name.
    /// Parse Runner displays these as collapsible groups.
    /// </summary>
    internal sealed class DataGraphConsole
    {
        private readonly List<GraphLogGroup> _groups = new();
        private readonly object _lock = new();

        /// <summary>
        /// All log groups, one per parsed graph.
        /// </summary>
        public IReadOnlyList<GraphLogGroup> Groups => _groups;

        /// <summary>
        /// Total error count across all groups.
        /// </summary>
        public int TotalErrors
        {
            get
            {
                int count = 0;
                foreach (var g in _groups) count += g.ErrorCount;
                return count;
            }
        }

        /// <summary>
        /// Total warning count across all groups.
        /// </summary>
        public int TotalWarnings
        {
            get
            {
                int count = 0;
                foreach (var g in _groups) count += g.WarningCount;
                return count;
            }
        }

        /// <summary>
        /// Total info count across all groups.
        /// </summary>
        public int TotalInfos
        {
            get
            {
                int count = 0;
                foreach (var g in _groups) count += g.InfoCount;
                return count;
            }
        }

        /// <summary>
        /// Creates a new log group for a graph parse operation.
        /// </summary>
        public GraphLogGroup BeginGroup(string graphName)
        {
            var group = new GraphLogGroup(graphName);
            lock (_lock)
            {
                _groups.Add(group);
            }
            return group;
        }

        /// <summary>
        /// Clears all log groups.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _groups.Clear();
            }
        }
    }

    /// <summary>
    /// A group of log entries for a single graph parse operation.
    /// Tracks severity counts and success/failure status.
    /// </summary>
    internal sealed class GraphLogGroup
    {
        private readonly List<ConsoleLogEntry> _entries = new();

        public GraphLogGroup(string graphName)
        {
            GraphName = graphName;
            StartTime = DateTime.Now;
        }

        public string GraphName { get; }
        public DateTime StartTime { get; }
        public DateTime EndTime { get; private set; }
        public bool IsComplete { get; private set; }
        public bool Success { get; private set; }

        public IReadOnlyList<ConsoleLogEntry> Entries => _entries;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }

        /// <summary>
        /// UI state — whether this group is expanded in the console.
        /// Auto-expanded if has errors.
        /// </summary>
        public bool IsExpanded { get; set; }

        public void LogInfo(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Info, message, DateTime.Now));
            InfoCount++;
        }

        public void LogWarning(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Warning, message, DateTime.Now));
            WarningCount++;
        }

        public void LogError(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Error, message, DateTime.Now));
            ErrorCount++;
        }

        public void LogSuccess(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Success, message, DateTime.Now));
        }

        /// <summary>
        /// Marks this group as complete.
        /// </summary>
        public void Complete(bool success)
        {
            IsComplete = true;
            Success = success;
            EndTime = DateTime.Now;
            IsExpanded = !success;
        }
    }

    /// <summary>
    /// A single console log entry with severity, message, and timestamp.
    /// </summary>
    internal readonly struct ConsoleLogEntry
    {
        public ConsoleLogEntry(LogSeverity severity, string message, DateTime timestamp)
        {
            Severity = severity;
            Message = message;
            Timestamp = timestamp;
        }

        public LogSeverity Severity { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }
    }

    /// <summary>
    /// Severity levels for console log entries.
    /// </summary>
    internal enum LogSeverity
    {
        Info,
        Warning,
        Error,
        Success
    }
}

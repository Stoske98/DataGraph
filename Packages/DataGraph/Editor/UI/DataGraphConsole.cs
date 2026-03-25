using System;
using System.Collections.Generic;
using UnityEngine;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Centralized logging system for DataGraph operations.
    /// Collects log entries grouped by graph name.
    /// Serializable to survive Unity recompilation.
    /// </summary>
    [Serializable]
    internal sealed class DataGraphConsole
    {
        [SerializeField] private List<GraphLogGroup> _groups = new();

        /// <summary>
        /// All log groups, one per parsed graph.
        /// </summary>
        public IReadOnlyList<GraphLogGroup> Groups => _groups;

        public int TotalErrors
        {
            get
            {
                int count = 0;
                foreach (var g in _groups) count += g.ErrorCount;
                return count;
            }
        }

        public int TotalWarnings
        {
            get
            {
                int count = 0;
                foreach (var g in _groups) count += g.WarningCount;
                return count;
            }
        }

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
            _groups.Add(group);
            return group;
        }

        /// <summary>
        /// Clears all log groups.
        /// </summary>
        public void Clear()
        {
            _groups.Clear();
        }
    }

    /// <summary>
    /// A group of log entries for a single graph parse operation.
    /// Serializable for persistence across recompilation.
    /// </summary>
    [Serializable]
    internal sealed class GraphLogGroup
    {
        [SerializeField] private List<ConsoleLogEntry> _entries = new();
        [SerializeField] private string _graphName;
        [SerializeField] private long _startTimeTicks;
        [SerializeField] private long _endTimeTicks;
        [SerializeField] private bool _isComplete;
        [SerializeField] private bool _success;
        [SerializeField] private int _errorCount;
        [SerializeField] private int _warningCount;
        [SerializeField] private int _infoCount;
        [SerializeField] private bool _isExpanded;

        public GraphLogGroup() { }

        public GraphLogGroup(string graphName)
        {
            _graphName = graphName;
            _startTimeTicks = DateTime.Now.Ticks;
        }

        public string GraphName => _graphName;
        public DateTime StartTime => new DateTime(_startTimeTicks);
        public DateTime EndTime => new DateTime(_endTimeTicks);
        public bool IsComplete => _isComplete;
        public bool Success => _success;
        public IReadOnlyList<ConsoleLogEntry> Entries => _entries;
        public int ErrorCount => _errorCount;
        public int WarningCount => _warningCount;
        public int InfoCount => _infoCount;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => _isExpanded = value;
        }

        public void LogInfo(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Info, message, DateTime.Now));
            _infoCount++;
        }

        public void LogWarning(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Warning, message, DateTime.Now));
            _warningCount++;
        }

        public void LogError(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Error, message, DateTime.Now));
            _errorCount++;
        }

        public void LogSuccess(string message)
        {
            _entries.Add(new ConsoleLogEntry(LogSeverity.Success, message, DateTime.Now));
        }

        public void Complete(bool success)
        {
            _isComplete = true;
            _success = success;
            _endTimeTicks = DateTime.Now.Ticks;
            _isExpanded = !success;
        }
    }

    /// <summary>
    /// A single console log entry. Serializable for persistence.
    /// </summary>
    [Serializable]
    internal struct ConsoleLogEntry
    {
        [SerializeField] private LogSeverity _severity;
        [SerializeField] private string _message;
        [SerializeField] private long _timestampTicks;

        public ConsoleLogEntry(LogSeverity severity, string message, DateTime timestamp)
        {
            _severity = severity;
            _message = message;
            _timestampTicks = timestamp.Ticks;
        }

        public LogSeverity Severity => _severity;
        public string Message => _message;
        public DateTime Timestamp => new DateTime(_timestampTicks);
    }

    internal enum LogSeverity
    {
        Info,
        Warning,
        Error,
        Success
    }
}

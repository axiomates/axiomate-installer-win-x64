using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AxiomateInstaller.Services;

/// <summary>
/// Append-only log writer. Each install run writes one file under
/// %TEMP%\axiomate-installer\install-{installerVersion}-{timestamp}.log.
/// Also keeps an in-memory buffer for the progress page UI.
/// </summary>
public sealed class Logger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    private readonly List<string> _buffer = new();

    public string LogFilePath { get; }
    public event Action<string>? OnLine;

    public Logger(string installerVersion)
    {
        string dir = Path.Combine(Path.GetTempPath(), "axiomate-installer");
        Directory.CreateDirectory(dir);
        string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        LogFilePath = Path.Combine(dir, $"install-{installerVersion}-{ts}.log");
        _writer = new StreamWriter(LogFilePath, append: true, new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        WriteLine($"=== Axiomate installer v{installerVersion} log started at {DateTime.Now:O} ===");
    }

    public void Info(string msg)  => WriteLine($"[INFO ] {msg}");
    public void Warn(string msg)  => WriteLine($"[WARN ] {msg}");
    public void Error(string msg) => WriteLine($"[ERROR] {msg}");
    public void Error(string msg, Exception ex) => WriteLine($"[ERROR] {msg}: {ex}");

    public void WriteLine(string line)
    {
        lock (_gate)
        {
            string stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";
            try { _writer.WriteLine(stamped); } catch { /* disk full etc. */ }
            _buffer.Add(stamped);
        }
        OnLine?.Invoke(line);
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate) { return _buffer.ToArray(); }
    }

    public void Dispose()
    {
        try { _writer.Flush(); _writer.Dispose(); } catch { }
    }
}

using System;
using System.IO;

namespace Rivet;

public class LogWriter : IDisposable
{
    private StreamWriter? _writer;
    private string _lastDate = "";

    public void Info(string msg) => Write("INFO", msg);
    public void Warn(string msg) => Write("WARN", msg);
    public void Error(string msg) => Write("ERROR", msg);

    private void Write(string level, string msg)
    {
        var now = DateTime.UtcNow;
        var date = now.ToString("yyyy-MM-dd");
        if (date != _lastDate)
        {
            _writer?.Dispose();
            var path = $"rivet-{date}.log";
            _writer = new StreamWriter(path, append: true) { AutoFlush = true };
            _lastDate = date;
        }

        var line = $"[{now:HH:mm:ss}] [{level}] {msg}";
        _writer?.WriteLine(line);
        Console.WriteLine(line);
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }
}

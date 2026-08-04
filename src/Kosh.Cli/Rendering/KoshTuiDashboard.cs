using System.Collections.Concurrent;
using System.Text;
using Kosh.Core.Runtime;
using Terminal.Gui;

namespace Kosh.Cli.Rendering;

public sealed class KoshTuiDashboard : Window
{
    private readonly Label _statusLabel;
    private readonly Label _filterLabel;
    private readonly FrameView _logFrame;
    private readonly TextView _logTextView;
    private readonly TextField _cmdInput;
    private readonly FrameView _cmdFrame;
    private readonly StatusBar _statusBar;

    private readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses = new();
    private readonly BoundedLogBuffer _logBuffer = new(5000);
    private readonly List<string> _orderedServices = new();
    
    private string _activeFilter = "all";
    private bool _isCmdActive = false;
    private volatile bool _hasPendingLogUpdate = false;
    private volatile bool _hasPendingStatusUpdate = false;

    private static readonly Color TerminalDefaultBg = Color.Black;

    public KoshTuiDashboard(string projectName)
    {
        Title = $" 🚀 kosh │ {projectName} ";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        ApplyNativeTerminalTheme();

        // 1. Header Frame (Height 4 to hold overview + log view shortcuts)
        var headerFrame = new FrameView("Status Overview")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 4,
            ColorScheme = GetHeaderColorScheme()
        };

        _statusLabel = new Label(" Initializing services...")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1
        };

        _filterLabel = new Label(" LOG VIEW: [A]ll")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1
        };

        headerFrame.Add(_statusLabel, _filterLabel);

        // 2. Log Frame (Middle)
        _logFrame = new FrameView("Logs [View: all] (Mouse wheel to scroll)")
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
            ColorScheme = GetLogColorScheme()
        };

        _logTextView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
            ColorScheme = GetLogColorScheme()
        };
        _logFrame.Add(_logTextView);

        // 3. Command Bar (Floating above status bar, hidden by default)
        _cmdFrame = new FrameView("Command Line (Esc to cancel, Tab to autocomplete)")
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3,
            Visible = false,
            ColorScheme = GetHeaderColorScheme()
        };

        _cmdInput = new TextField("")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            ColorScheme = GetLogColorScheme()
        };

        _cmdInput.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                ExecuteCommand(_cmdInput.Text.ToString() ?? "");
                CloseCmdInput();
                e.Handled = true;
            }
            else if (e.KeyEvent.Key == Key.Esc)
            {
                CloseCmdInput();
                e.Handled = true;
            }
            else if (e.KeyEvent.Key == Key.Tab)
            {
                AutoCompleteCommand();
                e.Handled = true;
            }
        };
        _cmdFrame.Add(_cmdInput);

        // 4. Status Bar
        _statusBar = new StatusBar(new[]
        {
            new StatusItem(Key.Q, "~Q~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.Unknown, "~:~ Command (v <service>)", () => OpenCmdInput()),
            new StatusItem(Key.A, "~A~ All Logs", () => SetLogFilter("all")),
            new StatusItem(Key.C, "~C~ Clear Logs", ConfirmAndClearLogs)
        })
        {
            ColorScheme = GetStatusBarColorScheme()
        };

        Add(headerFrame, _logFrame, _cmdFrame, _statusBar);

        // 5. Periodic UI Render Loop (20 FPS / 50ms)
        // Prevents event queue flooding from background threads and guarantees 100% responsive keyboard input!
        Application.MainLoop?.AddTimeout(TimeSpan.FromMilliseconds(50), (_) =>
        {
            if (_hasPendingStatusUpdate)
            {
                _hasPendingStatusUpdate = false;
                RefreshHeader();
            }

            if (_hasPendingLogUpdate && !_isCmdActive)
            {
                _hasPendingLogUpdate = false;
                RenderLogs();
            }

            return true;
        });
    }

    private void ApplyNativeTerminalTheme()
    {
        ColorScheme = new ColorScheme
        {
            Normal = Terminal.Gui.Attribute.Make(Color.White, TerminalDefaultBg),
            Focus = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg),
            HotNormal = Terminal.Gui.Attribute.Make(Color.BrightMagenta, TerminalDefaultBg),
            HotFocus = Terminal.Gui.Attribute.Make(Color.BrightMagenta, TerminalDefaultBg)
        };

        Colors.Base = ColorScheme;
    }

    private static ColorScheme GetHeaderColorScheme() => new()
    {
        Normal = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg),
        Focus = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg),
        HotNormal = Terminal.Gui.Attribute.Make(Color.BrightYellow, TerminalDefaultBg),
        HotFocus = Terminal.Gui.Attribute.Make(Color.BrightYellow, TerminalDefaultBg)
    };

    private static ColorScheme GetLogColorScheme() => new()
    {
        Normal = Terminal.Gui.Attribute.Make(Color.Gray, TerminalDefaultBg),
        Focus = Terminal.Gui.Attribute.Make(Color.White, TerminalDefaultBg),
        HotNormal = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg),
        HotFocus = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg)
    };

    private static ColorScheme GetStatusBarColorScheme() => new()
    {
        Normal = Terminal.Gui.Attribute.Make(Color.BrightCyan, TerminalDefaultBg),
        Focus = Terminal.Gui.Attribute.Make(Color.White, TerminalDefaultBg),
        HotNormal = Terminal.Gui.Attribute.Make(Color.Gray, TerminalDefaultBg),
        HotFocus = Terminal.Gui.Attribute.Make(Color.White, TerminalDefaultBg)
    };

    public void UpdateServiceStatus(ServiceRuntime runtime)
    {
        _serviceStatuses[runtime.Definition.Name] = runtime.Status;

        lock (_orderedServices)
        {
            if (!_orderedServices.Contains(runtime.Definition.Name, StringComparer.OrdinalIgnoreCase))
            {
                _orderedServices.Add(runtime.Definition.Name);
                _orderedServices.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        _hasPendingStatusUpdate = true;
    }

    public void AppendLog(string serviceName, string message, bool isError = false)
    {
        var entry = new LogEntry(serviceName, message, isError, DateTime.Now);
        _logBuffer.Add(entry);

        lock (_orderedServices)
        {
            if (!_orderedServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
            {
                _orderedServices.Add(serviceName);
                _orderedServices.Sort(StringComparer.OrdinalIgnoreCase);
                _hasPendingStatusUpdate = true;
            }
        }

        _hasPendingLogUpdate = true;
    }

    public void SetLogFilter(string serviceName)
    {
        _activeFilter = string.IsNullOrWhiteSpace(serviceName) ? "all" : serviceName.Trim();
        _logFrame.Title = $"Logs [View: {_activeFilter}] (Mouse wheel to scroll)";
        RefreshHeader();
        RenderLogs();
    }

    private void RenderLogs()
    {
        var filteredEntries = _logBuffer.GetLogs(_activeFilter);
        var sb = new StringBuilder();

        foreach (var entry in filteredEntries)
        {
            var prefix = entry.IsError ? $"[{entry.ServiceName}] [ERR]" : $"[{entry.ServiceName}]";
            sb.AppendLine($"{prefix} {entry.Message}");
        }

        _logTextView.Text = sb.ToString();
        _logTextView.CursorPosition = new Point(0, Math.Max(0, _logTextView.Lines - 1));
    }

    private void RefreshHeader()
    {
        var total = _serviceStatuses.Count;
        var running = _serviceStatuses.Values.Count(s => s == ServiceStatus.Running || s == ServiceStatus.Ready);
        var stopped = _serviceStatuses.Values.Count(s => s == ServiceStatus.Stopped);
        var failed = _serviceStatuses.Values.Count(s => s == ServiceStatus.Failed);

        _statusLabel.Text = $" Total: {total} │ 🟢 Live: {running} │ 🔴 Errors: {failed} │ ⏹ Stopped: {stopped}";

        // Build filter line with service shortcuts
        var filterSb = new StringBuilder(" LOG VIEW: ");
        if (_activeFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            filterSb.Append("[A:all] ");
        else
            filterSb.Append(" A:all  ");

        List<string> snapshot;
        lock (_orderedServices)
        {
            snapshot = _orderedServices.ToList();
        }

        for (int i = 0; i < snapshot.Count && i < 9; i++)
        {
            var sName = snapshot[i];
            var num = i + 1;
            if (_activeFilter.Equals(sName, StringComparison.OrdinalIgnoreCase))
                filterSb.Append($"[{num}:{sName}] ");
            else
                filterSb.Append($" {num}:{sName}  ");
        }

        _filterLabel.Text = filterSb.ToString();
    }

    private void OpenCmdInput(string initialPrefix = "")
    {
        _isCmdActive = true;
        _cmdFrame.Visible = true;
        _cmdInput.Text = initialPrefix;
        _cmdInput.CursorPosition = initialPrefix.Length;
        _cmdInput.SetFocus();
    }

    private void CloseCmdInput()
    {
        _isCmdActive = false;
        _cmdFrame.Visible = false;
        _logTextView.SetFocus();
        RenderLogs();
    }

    private void ExecuteCommand(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith(":"))
            cmd = cmd[1..].Trim();

        if (string.IsNullOrWhiteSpace(cmd))
            return;

        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var action = parts[0].ToLowerInvariant();

        if (action is "v" or "view")
        {
            var target = parts.Length > 1 ? parts[1] : "all";
            SetLogFilter(target);
        }
        else if (action is "clear" or "c")
        {
            ConfirmAndClearLogs();
        }
        else if (action is "quit" or "q" or "exit")
        {
            Application.RequestStop();
        }
    }

    private void AutoCompleteCommand()
    {
        var raw = _cmdInput.Text.ToString() ?? "";
        var trimmed = raw.TrimStart(':').TrimStart();

        List<string> snapshot;
        lock (_orderedServices)
        {
            snapshot = _orderedServices.ToList();
        }

        if (trimmed.StartsWith("v ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("view ", StringComparison.OrdinalIgnoreCase))
        {
            var spaceIdx = trimmed.IndexOf(' ');
            var prefix = trimmed[(spaceIdx + 1)..];

            var match = snapshot.FirstOrDefault(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                       ?? (prefix.StartsWith("a", StringComparison.OrdinalIgnoreCase) ? "all" : null);

            if (match != null)
            {
                var cmdName = trimmed[..spaceIdx];
                _cmdInput.Text = $"{cmdName} {match}";
                _cmdInput.CursorPosition = _cmdInput.Text.Length;
            }
        }
        else if (!string.IsNullOrWhiteSpace(trimmed))
        {
            var match = snapshot.FirstOrDefault(s => s.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                       ?? (trimmed.StartsWith("a", StringComparison.OrdinalIgnoreCase) ? "all" : null);

            if (match != null)
            {
                _cmdInput.Text = $"v {match}";
                _cmdInput.CursorPosition = _cmdInput.Text.Length;
            }
        }
    }

    public bool HandleRootKeyEvent(KeyEvent keyEvent)
    {
        if (_isCmdActive)
        {
            if (keyEvent.Key == Key.Esc)
            {
                CloseCmdInput();
                return true;
            }
            if (keyEvent.Key == Key.Enter)
            {
                ExecuteCommand(_cmdInput.Text.ToString() ?? "");
                CloseCmdInput();
                return true;
            }
            if (keyEvent.Key == Key.Tab)
            {
                AutoCompleteCommand();
                return true;
            }

            // Pass key directly to _cmdInput - bypasses focus stack & eliminates dropped keys completely
            _cmdInput.ProcessKey(keyEvent);
            return true;
        }

        var k = keyEvent.Key;
        var val = (char)keyEvent.KeyValue;

        // Command mode hotkeys: : or v
        if (val == ':' || k == (Key)':')
        {
            OpenCmdInput("");
            return true;
        }

        if (val == 'v' || val == 'V' || k == (Key)'v' || k == (Key)'V')
        {
            OpenCmdInput("v ");
            return true;
        }

        // Direct view All hotkey
        if (val == 'a' || val == 'A' || val == '0' || k == Key.A)
        {
            SetLogFilter("all");
            return true;
        }

        // Direct number shortcuts 1-9 for services
        if (val >= '1' && val <= '9')
        {
            int index = val - '1';
            List<string> snapshot;
            lock (_orderedServices)
            {
                snapshot = _orderedServices.ToList();
            }

            if (index < snapshot.Count)
            {
                SetLogFilter(snapshot[index]);
                return true;
            }
        }

        // Q for quit
        if (val == 'q' || val == 'Q' || k == Key.Q || k == (Key.Q | Key.ShiftMask) ||
            k == (Key.Q | Key.CtrlMask) || k == (Key.C | Key.CtrlMask))
        {
            Application.RequestStop();
            return true;
        }

        // C for clear
        if (val == 'c' || val == 'C' || k == Key.C || k == (Key.C | Key.ShiftMask))
        {
            ConfirmAndClearLogs();
            return true;
        }

        return false;
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        if (HandleRootKeyEvent(keyEvent))
            return true;

        return base.ProcessHotKey(keyEvent);
    }

    public void ConfirmAndClearLogs()
    {
        var result = MessageBox.Query("Clear Logs", "Are you sure you want to clear all logs?", "Yes", "No");
        if (result == 0)
        {
            ClearLogs();
        }
    }

    public void ClearLogs()
    {
        _logBuffer.Clear();
        _logTextView.Text = string.Empty;
    }
}

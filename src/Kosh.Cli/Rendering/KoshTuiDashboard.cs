using System.Collections.Concurrent;
using System.Text;
using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;
using Terminal.Gui;

namespace Kosh.Cli.Rendering;

public sealed class KoshTuiDashboard : Window
{
    private readonly FrameView _headerFrame;
    private readonly HeaderView _headerView;
    private readonly FrameView _logFrame;
    private readonly LogView _logView;
    private readonly TextField _cmdInput;
    private readonly FrameView _cmdFrame;
    private readonly StatusBar _statusBar;

    private readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses = new();
    private readonly BoundedLogBuffer _logBuffer = new(5000);
    private readonly List<string> _orderedServices = new();
    
    private string _activeFilter = "all";
    private string? _activeSearchQuery = null;
    private string? _explicitSearchService = null;

    private bool _isCmdActive = false;
    private volatile bool _hasPendingLogUpdate = false;
    private volatile bool _hasPendingStatusUpdate = false;

    private readonly Label _ghostLabel;
    private bool _ignoreNextTextChange = false;
    private List<string> _cycleSuggestions = new();
    private int _cycleIndex = 0;

    private static readonly Color TerminalDefaultBg = Color.Black;
    private int _lastConsoleWidth = Console.WindowWidth;
    private int _lastConsoleHeight = Console.WindowHeight;

    private readonly ConcurrentDictionary<string, ServiceId> _serviceNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISupervisor? _supervisor;

    public KoshTuiDashboard(string projectName, ISupervisor? supervisor = null)
    {
        _supervisor = supervisor;
        Title = $" 🚀 kosh │ {projectName} ";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        ApplyNativeTerminalTheme();

        _headerView = new HeaderView(_serviceStatuses, _orderedServices)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // 1. Header Frame
        _headerFrame = new FrameView("Status Overview")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
            ColorScheme = GetHeaderColorScheme()
        };

        _headerFrame.Add(_headerView);

        // 2. Log Frame (Middle)
        _logFrame = new FrameView("Logs [View: all] (Mouse wheel / Touchpad to scroll)")
        {
            X = 0,
            Y = Pos.Bottom(_headerFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = GetLogColorScheme()
        };

        _logView = new LogView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        _logFrame.Add(_logView);

        // 3. Command Bar (Floating above status bar, hidden by default)
        _cmdFrame = new FrameView("Command Line (v <service>, f <query>, f <service> <query>, Esc to cancel)")
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
                HandleTabCycle();
                e.Handled = true;
            }
        };

        _ghostLabel = new Label("")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme 
            { 
                Normal = Terminal.Gui.Attribute.Make(Color.DarkGray, TerminalDefaultBg) 
            },
            CanFocus = false
        };

        _cmdInput.TextChanged += (_) =>
        {
            if (_ignoreNextTextChange)
            {
                _ignoreNextTextChange = false;
                return;
            }
            UpdateSuggestions(_cmdInput.Text.ToString() ?? "");
        };

        _cmdFrame.Add(_cmdInput, _ghostLabel);

        // 4. Status Bar
        _statusBar = new StatusBar(new[]
        {
            new StatusItem(Key.Q, "~Q~ Quit", ConfirmAndQuit),
            new StatusItem(Key.Unknown, "~:~ Command (v/f)", () => OpenCmdInput()),
            new StatusItem(Key.H, "~H~ Help", ShowHelpDialog),
            new StatusItem(Key.C, "~C~ Clear Logs", ConfirmAndClearLogs)
        })
        {
            ColorScheme = GetStatusBarColorScheme()
        };

        Add(_headerFrame, _logFrame, _cmdFrame, _statusBar);

        // 5. Periodic UI Render Loop (20 FPS / 50ms)
        Application.MainLoop?.AddTimeout(TimeSpan.FromMilliseconds(50), (_) =>
        {
            int currentW = Console.WindowWidth;
            int currentH = Console.WindowHeight;

            if (currentW != _lastConsoleWidth || currentH != _lastConsoleHeight)
            {
                _lastConsoleWidth = currentW;
                _lastConsoleHeight = currentH;

                RebuildLayout();
            }
            else
            {
                if (_hasPendingStatusUpdate)
                {
                    _hasPendingStatusUpdate = false;
                    RefreshHeader();
                }

                if (_hasPendingLogUpdate && !_isCmdActive)
                {
                    _hasPendingLogUpdate = false;
                    RenderLogs(scrollToBottom: false);
                }
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
        _serviceNameToId[runtime.Definition.Name] = runtime.Definition.Id;

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
        _activeSearchQuery = null;
        _explicitSearchService = null;
        _activeFilter = string.IsNullOrWhiteSpace(serviceName) ? "all" : serviceName.Trim();
        RefreshHeader();
        RenderLogs(scrollToBottom: true);
    }

    private void RenderLogs(bool scrollToBottom = false)
    {
        List<LogEntry> entries;
        var targetService = _explicitSearchService ?? _activeFilter;

        if (_activeSearchQuery != null)
        {
            entries = _logBuffer.SearchLogs(targetService, _activeSearchQuery);
            _logFrame.Title = $"Logs [Search: \"{_activeSearchQuery}\" in {targetService}] ({entries.Count} matches) - Press ESC/ENTER to exit search";
        }
        else
        {
            entries = _logBuffer.GetLogs(_activeFilter);
            _logFrame.Title = $"Logs [View: {_activeFilter}] (Touchpad/Mouse wheel to scroll)";
        }

        var flatLines = new List<FlatLogLine>();
        foreach (var entry in entries)
        {
            var lines = entry.Message.Split('\n');
            flatLines.Add(new FlatLogLine
            {
                ServiceName = entry.ServiceName,
                Message = lines[0],
                IsError = entry.IsError,
                IsContinuation = false
            });
            for (int i = 1; i < lines.Length; i++)
            {
                flatLines.Add(new FlatLogLine
                {
                    ServiceName = entry.ServiceName,
                    Message = lines[i],
                    IsError = entry.IsError,
                    IsContinuation = true
                });
            }
        }

        _logView.SetLines(flatLines, scrollToBottom);
    }

    private void RefreshHeader()
    {
        RebuildLayout();
    }

    private void RebuildLayout()
    {
        var neededHeight = _headerView.GetNeededHeight() + 2;
        _headerFrame.Height = neededHeight;
        _logFrame.Y = Pos.Bottom(_headerFrame);
        _logFrame.Height = Dim.Fill(1);

        RemoveAll();
        Add(_headerFrame, _logFrame, _cmdFrame, _statusBar);

        if (Application.Top != null)
        {
            Application.Top.LayoutSubviews();
        }
        LayoutSubviews();
        _headerView.SetNeedsDisplay();
        _logView.SetNeedsDisplay();
        SetNeedsDisplay();
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
        _ghostLabel.Text = "";
        _logView.SetFocus();
        RenderLogs(scrollToBottom: true);
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
            _activeSearchQuery = null;
            _explicitSearchService = null;
            var target = parts.Length > 1 ? parts[1] : "all";
            SetLogFilter(target);
        }
        else if (action is "f" or "find")
        {
            if (parts.Length == 1)
            {
                _activeSearchQuery = null;
                _explicitSearchService = null;
                RenderLogs(scrollToBottom: true);
                RefreshHeader();
                return;
            }

            var secondToken = parts[1];
            bool isSecondTokenService;
            lock (_orderedServices)
            {
                isSecondTokenService = _orderedServices.Contains(secondToken, StringComparer.OrdinalIgnoreCase);
            }

            if (isSecondTokenService && parts.Length > 2)
            {
                _explicitSearchService = secondToken;
                _activeSearchQuery = string.Join(' ', parts.Skip(2));
            }
            else
            {
                _explicitSearchService = null;
                _activeSearchQuery = string.Join(' ', parts.Skip(1));
            }

            RenderLogs(scrollToBottom: true);
            RefreshHeader();
        }
        else if (action is "start" or "s")
        {
            if (parts.Length > 1)
                StartServiceByName(parts[1]);
        }
        else if (action is "stop" or "st")
        {
            if (parts.Length > 1)
                StopServiceByName(parts[1]);
        }
        else if (action is "help" or "h")
        {
            ShowHelpDialog();
        }
        else if (action is "clear" or "c")
        {
            ConfirmAndClearLogs();
        }
        else if (action is "quit" or "q" or "exit")
        {
            ConfirmAndQuit();
        }
    }

    private void HandleTabCycle()
    {
        if (_cycleSuggestions.Count > 0)
        {
            var currentInput = _cmdInput.Text.ToString() ?? "";
            
            // If we are currently showing the active cycle item, move to the next one
            if (currentInput == _cycleSuggestions[_cycleIndex])
            {
                _cycleIndex = (_cycleIndex + 1) % _cycleSuggestions.Count;
            }

            var chosen = _cycleSuggestions[_cycleIndex];
            _ignoreNextTextChange = true;
            _cmdInput.Text = chosen;
            _cmdInput.CursorPosition = chosen.Length;
            _ghostLabel.Text = "";
        }
    }

    private void UpdateSuggestions(string rawInput)
    {
        _cycleSuggestions.Clear();
        _cycleIndex = 0;

        var input = rawInput.TrimStart(':').TrimStart();
        if (string.IsNullOrWhiteSpace(input))
        {
            _ghostLabel.Text = "";
            return;
        }

        var lower = input.ToLowerInvariant();
        var options = new List<string>();

        string[] cmds = { "view ", "find ", "start ", "s ", "stop ", "st ", "help", "clear", "quit" };
        foreach (var c in cmds)
            if (c.StartsWith(lower)) options.Add(c);

        if (lower.StartsWith("s ") || lower.StartsWith("start "))
        {
            var prefix = lower.StartsWith("s ") ? "s " : "start ";
            var servicePart = lower.Substring(prefix.Length);

            lock (_orderedServices)
            {
                foreach (var s in _orderedServices)
                {
                    if (_serviceStatuses.TryGetValue(s, out var status))
                    {
                        if (status is ServiceStatus.NotStarted or ServiceStatus.Stopped or ServiceStatus.Failed)
                        {
                            if (s.ToLowerInvariant().StartsWith(servicePart))
                                options.Add(prefix + s);
                        }
                    }
                }
            }
        }
        else if (lower.StartsWith("st ") || lower.StartsWith("stop "))
        {
            var prefix = lower.StartsWith("st ") ? "st " : "stop ";
            var servicePart = lower.Substring(prefix.Length);

            lock (_orderedServices)
            {
                foreach (var s in _orderedServices)
                {
                    if (_serviceStatuses.TryGetValue(s, out var status))
                    {
                        if (status is ServiceStatus.Running or ServiceStatus.Ready or ServiceStatus.Starting)
                        {
                            if (s.ToLowerInvariant().StartsWith(servicePart))
                                options.Add(prefix + s);
                        }
                    }
                }
            }
        }

        if (lower.StartsWith("v ") || lower.StartsWith("view "))
        {
            var prefix = lower.StartsWith("v ") ? "v " : "view ";
            var servicePart = lower.Substring(prefix.Length);

            List<string> snapshot;
            lock (_orderedServices) { snapshot = _orderedServices.ToList(); }

            if ("all".StartsWith(servicePart) && servicePart.Length > 0)
                options.Add(prefix + "all");

            foreach (var s in snapshot)
                if (s.ToLowerInvariant().StartsWith(servicePart))
                    options.Add(prefix + s);
        }
        else if (lower.StartsWith("f ") || lower.StartsWith("find "))
        {
            var prefix = lower.StartsWith("f ") ? "f " : "find ";
            var servicePart = lower.Substring(prefix.Length);

            if (!servicePart.Contains(" "))
            {
                List<string> snapshot;
                lock (_orderedServices) { snapshot = _orderedServices.ToList(); }
                foreach (var s in snapshot)
                    if (s.ToLowerInvariant().StartsWith(servicePart))
                        options.Add(prefix + s + " ");
            }
        }

        if (options.Count > 0)
        {
            _cycleSuggestions = options;
            _cycleIndex = 0;

            var suggestion = _cycleSuggestions[0];
            var typedPrefixLength = rawInput.Length;
            
            // If suggestion is longer than what's typed, show the rest
            if (suggestion.Length > input.Length)
            {
                var remaining = suggestion.Substring(input.Length);
                _ghostLabel.Text = remaining;
                _ghostLabel.X = typedPrefixLength;
            }
            else
            {
                _ghostLabel.Text = "";
            }
        }
        else
        {
            _ghostLabel.Text = "";
        }
    }

    public bool HandleRootKeyEvent(KeyEvent keyEvent)
    {
        uint k = (uint)keyEvent.Key;
        char ch = (char)(k & 0xFFFF);

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
                HandleTabCycle();
                return true;
            }

            // Direct route to _cmdInput - guarantees 0 dropped characters!
            _cmdInput.ProcessKey(keyEvent);
            return true;
        }

        // If in search mode, Esc or Enter exits search mode and restores live logs!
        if (_activeSearchQuery != null)
        {
            if (keyEvent.Key == Key.Esc || keyEvent.Key == Key.Enter)
            {
                _activeSearchQuery = null;
                _explicitSearchService = null;
                RefreshHeader();
                RenderLogs(scrollToBottom: true);
                return true;
            }
        }

        // Handle Ctrl shortcuts first (e.g. Ctrl+C, Ctrl+Q)
        if (keyEvent.IsCtrl)
        {
            if (keyEvent.Key == (Key.Q | Key.CtrlMask) || keyEvent.Key == (Key.C | Key.CtrlMask))
            {
                ConfirmAndQuit();
                return true;
            }
            return false;
        }

        // Command mode hotkeys: : or v or f
        if (ch == ':')
        {
            OpenCmdInput("");
            return true;
        }

        if (ch == 'v' || ch == 'V')
        {
            OpenCmdInput("v ");
            return true;
        }

        if (ch == 'f' || ch == 'F')
        {
            OpenCmdInput("f ");
            return true;
        }

        // H for help
        if (ch == 'h' || ch == 'H')
        {
            ShowHelpDialog();
            return true;
        }

        // Q for quit
        if (ch == 'q' || ch == 'Q')
        {
            ConfirmAndQuit();
            return true;
        }

        // C for clear
        if (ch == 'c' || ch == 'C')
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

    public void ConfirmAndQuit()
    {
        var result = MessageBox.Query("Quit Kosh", "Are you sure you want to stop all services and exit?", "Yes", "No");
        if (result == 0)
        {
            Application.RequestStop();
        }
    }

    public void ConfirmAndClearLogs()
    {
        var result = MessageBox.Query("Clear Logs", "Are you sure you want to clear all logs?", "Yes", "No");
        if (result == 0)
        {
            ClearLogs();
        }
    }

    private async void StartServiceByName(string name)
    {
        if (_supervisor == null) return;
        if (_serviceNameToId.TryGetValue(name, out var id))
        {
            await _supervisor.StartServiceAsync(id, CancellationToken.None);
        }
    }

    private async void StopServiceByName(string name)
    {
        if (_supervisor == null) return;
        if (_serviceNameToId.TryGetValue(name, out var id))
        {
            await _supervisor.StopServiceAsync(id, CancellationToken.None);
        }
    }

    public void ShowHelpDialog()
    {
        var dialog = new Dialog(" 💡 KOSH CLI HELP ", 72, 22);

        var helpText =
            "────────────────────────── COMMANDS ──────────────────────────\n" +
            string.Format("  {0,-25} {1}\n", "view <svc|all>", "Filter logs by service name") +
            string.Format("  {0,-25} {1}\n", "find <query>", "Search all logs for keyword") +
            string.Format("  {0,-25} {1}\n", "find <svc> <query>", "Search specific service logs") +
            string.Format("  {0,-25} {1}\n", "start <svc>", "Start stopped/not-started service") +
            string.Format("  {0,-25} {1}\n", "stop <svc>", "Stop running service") +
            string.Format("  {0,-25} {1}\n", "clear", "Clear log buffer") +
            string.Format("  {0,-25} {1}\n\n", "quit", "Exit Kosh CLI") +
            "───────────────────── SERVICE STATUS ICONS ───────────────────\n" +
            "  ● Running      ✔ Ready          ✖ Failed\n" +
            "  ▲ Starting     ■ Stopped        ○ Not Started\n\n" +
            "───────────────────── KEYBOARD SHORTCUTS ─────────────────────\n" +
            string.Format("  {0,-16} {1}\n", "H", "Open this Help dialog") +
            string.Format("  {0,-16} {1}\n", "Shift + Drag", "Native terminal text selection") +
            string.Format("  {0,-16} {1}\n", "Tab", "Cycle command suggestions") +
            string.Format("  {0,-16} {1}", "C / Q", "Clear log buffer / Quit Kosh CLI");

        var label = new Label(helpText)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 2,
            TextAlignment = TextAlignment.Left
        };

        var closeBtn = new Button("Close");
        closeBtn.Clicked += () => Application.RequestStop();
        dialog.AddButton(closeBtn);

        dialog.Add(label);
        Application.Run(dialog);
    }

    public void ClearLogs()
    {
        _activeSearchQuery = null;
        _explicitSearchService = null;
        _logBuffer.Clear();
        RenderLogs(scrollToBottom: true);
    }
}

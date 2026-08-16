using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using Kosh.Core.Definitions;
using Kosh.Core.Runtime;
using Kosh.Core.Supervisor;
using Kosh.Core.ValueObjects;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Kosh.Cli.Rendering;

public sealed class KoshTuiDashboard : Window
{
    private readonly IApplication _app;
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
    private bool _updateScheduled = false;

    private readonly Label _ghostLabel;
    private bool _ignoreNextTextChange = false;
    private List<string> _cycleSuggestions = new();
    private int _cycleIndex = 0;

    private bool _isShowingDialog = false;

    private static readonly Color TerminalDefaultBg = Color.None;

    private readonly ConcurrentDictionary<string, ServiceId> _serviceNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISupervisor? _supervisor;
    
    private readonly ConfigDefinition _config;
    private readonly Dictionary<string, string> _serviceGroups = new();
    private readonly ConcurrentDictionary<string, ServiceDefinition> _serviceDefs = new();
    private readonly ConcurrentDictionary<string, ServiceRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);

    public KoshTuiDashboard(IApplication app, ConfigDefinition config, ISupervisor? supervisor = null)
    {
        _app = app;
        _supervisor = supervisor;
        _config = config;
        Title = $" 🚀 kosh │ {config.ProjectName} ";

        foreach (var group in config.ServiceGroups)
        {
            foreach (var service in group.Services)
            {
                _serviceGroups[service.Name] = group.Name;
                _serviceDefs[service.Name] = service;
            }
        }
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDown += (sender, key) => 
        {
            if (HandleRootKeyEvent(key))
            {
                key.Handled = true;
            }
        };

        ApplyNativeTerminalTheme();

        _headerView = new HeaderView(_serviceStatuses, _orderedServices, _serviceDefs, _serviceGroups, _runtimes)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _headerView.HeightNeededChanged += () => RebuildLayout();

        _headerFrame = new FrameView
        {
            Title = "Status Overview",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3
        };
        _headerFrame.SetScheme(GetHeaderColorScheme());
        _headerFrame.Add(_headerView);

        _logFrame = new FrameView
        {
            Title = "Logs [View: all]",
            X = 0,
            Y = Pos.Bottom(_headerFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };
        _logFrame.SetScheme(GetLogColorScheme());

        _logView = new LogView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        _logFrame.Add(_logView);

        _cmdFrame = new FrameView
        {
            Title = "Command Line (v <service>, f <query>, f <service> <query>, Esc to cancel)",
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3,
            Visible = false
        };
        _cmdFrame.SetScheme(GetHeaderColorScheme());

        _cmdInput = new TextField
        {
            Text = "",
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };
        _cmdInput.SetScheme(GetLogColorScheme());

        _cmdInput.KeyDown += (sender, key) =>
        {
            if (key == Key.Enter)
            {
                ExecuteCommand(_cmdInput.Text ?? "");
                CloseCmdInput();
                key.Handled = true;
            }
            else if (key == Key.Esc)
            {
                CloseCmdInput();
                key.Handled = true;
            }
            else if (key == Key.Tab)
            {
                HandleTabCycle();
                key.Handled = true;
            }
        };

        _ghostLabel = new Label
        {
            Text = "",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            CanFocus = false
        };
        _ghostLabel.SetScheme(new Scheme { Normal = new Attribute(Color.DarkGray, TerminalDefaultBg) });

        _cmdInput.TextChanged += (sender, args) =>
        {
            if (_ignoreNextTextChange)
            {
                _ignoreNextTextChange = false;
                return;
            }
            UpdateSuggestions(_cmdInput.Text ?? "");
        };

        _cmdFrame.Add(_cmdInput, _ghostLabel);

        _statusBar = new StatusBar();
        _statusBar.SetScheme(GetStatusBarColorScheme());
        _statusBar.AddShortcutAt(0, new Shortcut { Key = Key.C, Text = "Clear Logs" });
        _statusBar.AddShortcutAt(1, new Shortcut { Key = Key.S, Text = "Expand Services" });
        _statusBar.AddShortcutAt(2, new Shortcut { Key = Key.Q, Text = "Quit" });
        _statusBar.AddShortcutAt(3, new Shortcut { Key = Key.H, Text = "Help" });

        Add(_headerFrame, _logFrame, _cmdFrame, _statusBar);

        _app.AddTimeout(TimeSpan.FromSeconds(2), () =>
        {
            if (_headerView.IsExpanded)
            {
                RefreshHeader();
            }
            return true;
        });
    }

    private void ScheduleUpdate()
    {
        if (_updateScheduled) return;
        _updateScheduled = true;
        
        Task.Delay(50).ContinueWith(_ =>
        {
            _app.Invoke(() =>
            {
                _updateScheduled = false;
                RefreshHeader();
                if (!_isCmdActive) RenderLogs(scrollToBottom: false);
            });
        });
    }

    private void ApplyNativeTerminalTheme()
    {
        SetScheme(new Scheme
        {
            Normal = new Attribute(Color.White, TerminalDefaultBg),
            Focus = new Attribute(Color.BrightCyan, TerminalDefaultBg),
            HotNormal = new Attribute(Color.BrightMagenta, TerminalDefaultBg),
            HotFocus = new Attribute(Color.BrightMagenta, TerminalDefaultBg)
        });
    }

    private static Scheme GetHeaderColorScheme() => new()
    {
        Normal = new Attribute(Color.BrightCyan, TerminalDefaultBg),
        Focus = new Attribute(Color.BrightCyan, TerminalDefaultBg),
        HotNormal = new Attribute(Color.BrightYellow, TerminalDefaultBg),
        HotFocus = new Attribute(Color.BrightYellow, TerminalDefaultBg)
    };

    private static Scheme GetLogColorScheme() => new()
    {
        Normal = new Attribute(Color.Gray, TerminalDefaultBg),
        Focus = new Attribute(Color.White, TerminalDefaultBg),
        HotNormal = new Attribute(Color.BrightCyan, TerminalDefaultBg),
        HotFocus = new Attribute(Color.BrightCyan, TerminalDefaultBg)
    };

    private static Scheme GetStatusBarColorScheme() => new()
    {
        Normal = new Attribute(Color.BrightCyan, TerminalDefaultBg),
        Focus = new Attribute(Color.White, TerminalDefaultBg),
        HotNormal = new Attribute(Color.Gray, TerminalDefaultBg),
        HotFocus = new Attribute(Color.White, TerminalDefaultBg)
    };

    public void UpdateServiceStatus(ServiceRuntime runtime)
    {
        _runtimes[runtime.Definition.Name] = runtime;
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

        ScheduleUpdate();
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
            }
        }

        ScheduleUpdate();
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
            var svcPart = _explicitSearchService != null ? $"{_explicitSearchService} " : "";
            _logFrame.Title = $"Logs [View: find {svcPart}{_activeSearchQuery}] ({entries.Count} matches)";
        }
        else
        {
            entries = _logBuffer.GetLogs(_activeFilter);
            _logFrame.Title = $"Logs [View: {_activeFilter}] ({entries.Count} logs)";
        }

        _logView.SetRawEntries(entries, scrollToBottom);
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

        SetNeedsDraw();
    }

    private void OpenCmdInput(string initialPrefix = "")
    {
        _isCmdActive = true;
        _cmdFrame.Visible = true;
        _cmdInput.Text = initialPrefix;
        _cmdInput.InsertionPoint = initialPrefix.Length;
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
                RefreshHeader();
                RenderLogs(scrollToBottom: true);
                return;
            }

            var fullQuery = cmd.Substring(action.Length).Trim();
            var firstWord = parts[1];

            bool isFirstWordAService;
            lock (_orderedServices)
            {
                isFirstWordAService = firstWord.Equals("all", StringComparison.OrdinalIgnoreCase) || 
                                      _orderedServices.Contains(firstWord, StringComparer.OrdinalIgnoreCase);
            }

            if (isFirstWordAService && parts.Length >= 3)
            {
                _explicitSearchService = firstWord.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : firstWord;
                _activeSearchQuery = fullQuery.Substring(firstWord.Length).Trim();
            }
            else
            {
                _explicitSearchService = null;
                _activeSearchQuery = fullQuery;
            }
            
            RefreshHeader();
            RenderLogs(scrollToBottom: true);
        }
        else if (action is "start" or "s")
        {
            if (parts.Length > 1)
            {
                var serviceName = parts[1];
                var argsOverride = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : null;
                StartServiceByName(serviceName, argsOverride);
            }
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
            _cycleIndex = (_cycleIndex + 1) % _cycleSuggestions.Count;
            var selected = _cycleSuggestions[_cycleIndex];

            _ignoreNextTextChange = true;
            _cmdInput.Text = selected;
            _cmdInput.InsertionPoint = selected.Length;
            _ghostLabel.Text = "";
        }
    }

    private void UpdateSuggestions(string rawInput)
    {
        var input = rawInput.TrimStart(':');
        var lower = input.ToLowerInvariant();
        var options = new List<string>();

        string[] cmds = { "view ", "find ", "start ", "s ", "stop ", "st ", "help", "clear", "quit" };
        foreach (var c in cmds)
            if (c.StartsWith(lower)) options.Add(c);

        if (input.StartsWith("view ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("v ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("start ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("s ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("stop ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("st ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var prefix = parts[0] + " ";
            var servicePart = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";

            if (input.StartsWith("view ", StringComparison.OrdinalIgnoreCase) || input.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
            {
                if ("all".StartsWith(servicePart)) options.Add(prefix + "all");
            }

            if (input.StartsWith("start ", StringComparison.OrdinalIgnoreCase) || input.StartsWith("s ", StringComparison.OrdinalIgnoreCase))
            {
                List<string> snapshot;
                lock (_orderedServices) { snapshot = _orderedServices.ToList(); }
                foreach (var s in snapshot)
                {
                    if (_serviceStatuses.TryGetValue(s, out var status) && status is ServiceStatus.Stopped or ServiceStatus.Failed or ServiceStatus.NotStarted)
                    {
                        if (s.ToLowerInvariant().StartsWith(servicePart))
                            options.Add(prefix + s + " ");
                    }
                }
            }
            else if (input.StartsWith("stop ", StringComparison.OrdinalIgnoreCase) || input.StartsWith("st ", StringComparison.OrdinalIgnoreCase))
            {
                List<string> snapshot;
                lock (_orderedServices) { snapshot = _orderedServices.ToList(); }
                foreach (var s in snapshot)
                {
                    if (_serviceStatuses.TryGetValue(s, out var status) && status is ServiceStatus.Running or ServiceStatus.Ready or ServiceStatus.Starting)
                    {
                        if (s.ToLowerInvariant().StartsWith(servicePart))
                            options.Add(prefix + s + " ");
                    }
                }
            }
            else
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

    public bool HandleRootKeyEvent(Key key)
    {
        if (key == Key.C.WithCtrl || key == Key.Q.WithCtrl)
        {
            ConfirmAndQuit();
            return true;
        }

        if (_isCmdActive)
        {
            if (key == Key.Esc)
            {
                CloseCmdInput();
                return true;
            }
            if (key == Key.Enter)
            {
                ExecuteCommand(_cmdInput.Text ?? "");
                CloseCmdInput();
                return true;
            }
            if (key == Key.Tab)
            {
                HandleTabCycle();
                return true;
            }

            return false;
        }

        if (_activeSearchQuery != null)
        {
            if (key == Key.Esc || key == Key.Enter)
            {
                _activeSearchQuery = null;
                _explicitSearchService = null;
                RefreshHeader();
                RenderLogs(scrollToBottom: true);
                return true;
            }
        }

        if (key == Key.CursorUp)
        {
            _logView.ScrollUp(1);
            return true;
        }

        if (key == Key.CursorDown)
        {
            _logView.ScrollDown(1);
            return true;
        }

        if (key == Key.PageUp)
        {
            _logView.ScrollUp(Math.Max(1, _logView.Viewport.Height));
            return true;
        }

        if (key == Key.PageDown)
        {
            _logView.ScrollDown(Math.Max(1, _logView.Viewport.Height));
            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        uint k = (uint)key;
        char ch = (char)(k & 0xFFFF);

        if (ch == ':')
        {
            OpenCmdInput("");
            return true;
        }

        if (ch == 'h' || ch == 'H')
        {
            ShowHelpDialog();
            return true;
        }

        if (ch == 's' || ch == 'S')
        {
            _headerView.IsExpanded = !_headerView.IsExpanded;
            _headerFrame.Title = _headerView.IsExpanded ? "Status Overview (Expanded)" : "Status Overview";
            
            _statusBar.RemoveShortcut(1);
            _statusBar.AddShortcutAt(1, new Shortcut { Key = Key.S, Text = _headerView.IsExpanded ? "Compact Services" : "Expand Services" });

            RefreshHeader();
            return true;
        }

        if (ch == 'q' || ch == 'Q')
        {
            ConfirmAndQuit();
            return true;
        }

        if (ch == 'c' || ch == 'C')
        {
            ConfirmAndClearLogs();
            return true;
        }

        return false;
    }

    public void ConfirmAndQuit()
    {
        if (_isShowingDialog) return;
        _isShowingDialog = true;
        try
        {
            var dialog = new Dialog
            {
                Title = " 🛑 Quit Kosh ",
                Width = 60,
                Height = 8
            };

            var label = new Label
            {
                Text = "Are you sure you want to stop all services and exit?",
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                TextAlignment = Alignment.Center
            };

            bool confirmed = false;

            var yesBtn = new Button { Text = "Yes", IsDefault = true };
            yesBtn.Accepting += (s, e) =>
            {
                confirmed = true;
                _app.RequestStop(dialog);
            };

            var noBtn = new Button { Text = "No" };
            noBtn.Accepting += (s, e) =>
            {
                confirmed = false;
                _app.RequestStop(dialog);
            };

            dialog.KeyDown += (s, key) =>
            {
                uint k = (uint)key;
                char ch = (char)(k & 0xFFFF);
                if (ch == 'y' || ch == 'Y' || key == Key.Y)
                {
                    confirmed = true;
                    _app.RequestStop(dialog);
                }
                else if (ch == 'n' || ch == 'N' || key == Key.N || key == Key.Esc)
                {
                    confirmed = false;
                    _app.RequestStop(dialog);
                }
            };

            dialog.AddButton(yesBtn);
            dialog.AddButton(noBtn);
            dialog.Add(label);

            _app.Run(dialog);

            if (confirmed)
            {
                _app.RequestStop();
            }
        }
        finally
        {
            _isShowingDialog = false;
        }
    }

    public void ConfirmAndClearLogs()
    {
        if (_isShowingDialog) return;
        _isShowingDialog = true;
        try
        {
            var dialog = new Dialog
            {
                Title = " 🧹 Clear Logs ",
                Width = 56,
                Height = 8
            };

            var label = new Label
            {
                Text = "Are you sure you want to clear all logs?",
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                TextAlignment = Alignment.Center
            };

            bool confirmed = false;

            var yesBtn = new Button { Text = "Yes", IsDefault = true };
            yesBtn.Accepting += (s, e) =>
            {
                confirmed = true;
                _app.RequestStop(dialog);
            };

            var noBtn = new Button { Text = "No" };
            noBtn.Accepting += (s, e) =>
            {
                confirmed = false;
                _app.RequestStop(dialog);
            };

            dialog.KeyDown += (s, key) =>
            {
                uint k = (uint)key;
                char ch = (char)(k & 0xFFFF);
                if (ch == 'y' || ch == 'Y' || key == Key.Y)
                {
                    confirmed = true;
                    _app.RequestStop(dialog);
                }
                else if (ch == 'n' || ch == 'N' || key == Key.N || key == Key.Esc)
                {
                    confirmed = false;
                    _app.RequestStop(dialog);
                }
            };

            dialog.AddButton(yesBtn);
            dialog.AddButton(noBtn);
            dialog.Add(label);

            _app.Run(dialog);

            if (confirmed)
            {
                ClearLogs();
            }
        }
        finally
        {
            _isShowingDialog = false;
        }
    }

    private async void StartServiceByName(string name, string? argsOverride = null)
    {
        if (_supervisor == null) return;
        if (_serviceNameToId.TryGetValue(name, out var id))
        {
            await _supervisor.StartServiceAsync(id, CancellationToken.None, argsOverride);
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
        if (_isShowingDialog) return;
        _isShowingDialog = true;
        try
        {
            var dialog = new Dialog
            {
                Title = " 💡 KOSH CLI HELP ",
                Width = 76,
                Height = 28
            };

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
                string.Format("  {0,-24} {1}\n", ":", "Open command input prompt") +
                string.Format("  {0,-24} {1}\n", "Up / Down / PgUp / PgDn", "Scroll logs up and down") +
                string.Format("  {0,-24} {1}\n", "Mouse Wheel / Touchpad", "Scroll logs smoothly") +
                string.Format("  {0,-24} {1}\n", "Tab", "Cycle command suggestions") +
                string.Format("  {0,-24} {1}\n", "Shift + Drag", "Native terminal text selection") +
                string.Format("  {0,-24} {1}\n", "S", "Compact/Expand Service View") +
                string.Format("  {0,-24} {1}\n", "H", "Open this Help dialog") +
                string.Format("  {0,-24} {1}\n", "C", "Clear log buffer") +
                string.Format("  {0,-24} {1}", "Q", "Quit Kosh CLI");

            var label = new Label
            {
                Text = helpText,
                X = 1,
                Y = 0,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 2,
                TextAlignment = Alignment.Start
            };

            var closeBtn = new Button { Text = "Close", IsDefault = true };
            closeBtn.Accepting += (s, e) => _app.RequestStop(dialog);

            dialog.KeyDown += (s, key) =>
            {
                if (key == Key.Esc || key == Key.Enter)
                {
                    _app.RequestStop(dialog);
                }
            };

            dialog.AddButton(closeBtn);
            dialog.Add(label);
            _app.Run(dialog);
        }
        finally
        {
            _isShowingDialog = false;
        }
    }

    public void ClearLogs()
    {
        _activeSearchQuery = null;
        _explicitSearchService = null;
        _logBuffer.Clear();
        RenderLogs(scrollToBottom: true);
    }
}

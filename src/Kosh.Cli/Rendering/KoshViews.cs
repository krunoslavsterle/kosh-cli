using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using Kosh.Core.Definitions;
using Kosh.Core.Runtime;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;
using DrawContext = Terminal.Gui.ViewBase.DrawContext;
using View = Terminal.Gui.ViewBase.View;

namespace Kosh.Cli.Rendering;

internal static class ServiceColorManager
{
    private static readonly ConcurrentDictionary<string, Attribute> _colorMap = new();

    private static uint GetDeterministicHash(string str)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in str.ToLowerInvariant())
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash;
        }
    }

    private static Color HslToColor(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        byte r = (byte)Math.Clamp((r1 + m) * 255f, 0f, 255f);
        byte g = (byte)Math.Clamp((g1 + m) * 255f, 0f, 255f);
        byte b = (byte)Math.Clamp((b1 + m) * 255f, 0f, 255f);

        return new Color(r, g, b);
    }

    public static Attribute GetColor(string serviceName)
    {
        return _colorMap.GetOrAdd(serviceName, name =>
        {
            var hash = GetDeterministicHash(name);
            float hue = hash % 360;
            var color = HslToColor(hue, 0.85f, 0.65f);
            return new Attribute(color, Color.None);
        });
    }

    public static Attribute GetStatusColor(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Running => new Attribute(Color.BrightGreen, Color.None),
            ServiceStatus.Ready => new Attribute(Color.BrightCyan, Color.None),
            ServiceStatus.Stopped => new Attribute(Color.Gray, Color.None),
            ServiceStatus.Failed => new Attribute(Color.BrightRed, Color.None),
            ServiceStatus.Starting => new Attribute(Color.BrightYellow, Color.None),
            ServiceStatus.NotStarted => new Attribute(Color.DarkGray, Color.None),
            _ => new Attribute(Color.Gray, Color.None)
        };
    }

    public static string GetStatusIcon(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Running => "●",
            ServiceStatus.Ready => "✔",
            ServiceStatus.Failed => "✖",
            ServiceStatus.Starting => "▲",
            ServiceStatus.Stopped => "■",
            ServiceStatus.NotStarted => "○",
            _ => "⚪"
        };
    }
}

internal struct FlatLogLine
{
    public LogEntry Entry;
    public int StartIndex;
    public int Length;
    public bool IsContinuation;
}

internal sealed class HeaderView : View
{
    private readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses;
    private readonly List<string> _orderedServices;
    private readonly ConcurrentDictionary<string, ServiceDefinition> _serviceDefs;
    private readonly Dictionary<string, string> _serviceGroups;
    private readonly ConcurrentDictionary<string, ServiceRuntime> _runtimes;
    
    private int _lastNeededHeight = -1;
    public bool IsExpanded { get; set; } = false;

    public event Action? HeightNeededChanged;

    public int GetAvailableWidth()
    {
        if (Viewport.Width > 0) return Viewport.Width;
        if (SuperView != null && SuperView.Viewport.Width > 2) return SuperView.Viewport.Width - 2;
        return Math.Max(10, Console.WindowWidth - 4);
    }

    public int GetNeededHeight()
    {
        int availableWidth = GetAvailableWidth();

        List<string> snapshot;
        lock (_orderedServices) { snapshot = _orderedServices.ToList(); }
        if (snapshot.Count == 0) return 1;

        if (IsExpanded)
        {
            return 2 + snapshot.Count;
        }

        int currentX = 1;
        int currentY = 0;

        for (int i = 0; i < snapshot.Count; i++)
        {
            var service = snapshot[i];
            if (_serviceStatuses.TryGetValue(service, out var status))
            {
                var textLength = service.Length + (i < snapshot.Count - 1 ? 5 : 2);
                if (currentX + textLength > availableWidth && currentX > 1)
                {
                    currentY++;
                    currentX = 1;
                }
                currentX += textLength;
            }
        }

        return currentY + 1;
    }

    public HeaderView(ConcurrentDictionary<string, ServiceStatus> statuses, List<string> ordered, 
                      ConcurrentDictionary<string, ServiceDefinition> defs, Dictionary<string, string> groups,
                      ConcurrentDictionary<string, ServiceRuntime> runtimes)
    {
        _serviceStatuses = statuses;
        _orderedServices = ordered;
        _serviceDefs = defs;
        _serviceGroups = groups;
        _runtimes = runtimes;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);

        List<string> snapshot;
        lock (_orderedServices) { snapshot = _orderedServices.ToList(); }

        var normalAttr = new Attribute(Color.White, Color.None);
        var dividerAttr = new Attribute(Color.DarkGray, Color.None);

        if (IsExpanded)
        {
            var headerAttr = new Attribute(Color.BrightYellow, Color.None);

            string header = string.Format("{0,-20} {1,-16} {2,-20} {3,-8} {4,-12}", 
                "Service", "Status", "Group", "Port", "ManualStart");

            SetAttribute(headerAttr);
            AddStr(1, 0, header);

            SetAttribute(dividerAttr);
            AddStr(1, 1, new string('─', header.Length));

            int cY = 2;
            foreach (var service in snapshot)
            {
                if (_serviceStatuses.TryGetValue(service, out var status))
                {
                    _serviceDefs.TryGetValue(service, out var def);
                    _serviceGroups.TryGetValue(service, out var groupName);
                    
                    groupName ??= "-";
                    string port = "-";
                    if (def != null)
                    {
                        if (def.Environment.TryGetValue("PORT", out var p)) port = p;
                        else if (def.Environment.TryGetValue("ASPNETCORE_URLS", out var urls))
                        {
                            var match = Regex.Match(urls, @"http[s]?://.*:(\d+)");
                            if (match.Success) port = match.Groups[1].Value;
                        }
                        
                        if (port == "-" && !string.IsNullOrWhiteSpace(def.Args))
                        {
                            var match = Regex.Match(def.Args, @"http[s]?://.*:(\d+)");
                            if (match.Success) port = match.Groups[1].Value;
                            else
                            {
                                var portMatch = Regex.Match(def.Args, @"--port\s+(\d+)");
                                if (portMatch.Success) port = portMatch.Groups[1].Value;
                            }
                        }
                    }
                    string manualStart = def != null ? def.ManualStart.ToString().ToLower() : "-";
                    
                    SetAttribute(ServiceColorManager.GetColor(service));
                    AddStr(1, cY, string.Format("{0,-20}", service.Length > 20 ? service.Substring(0, 20) : service));

                    var icon = ServiceColorManager.GetStatusIcon(status);
                    SetAttribute(ServiceColorManager.GetStatusColor(status));
                    AddStr(22, cY, string.Format("{0,-16}", $"{icon} {status.ToString().ToLower()}"));

                    SetAttribute(normalAttr);
                    AddStr(39, cY, string.Format("{0,-20}", groupName.Length > 20 ? groupName.Substring(0, 20) : groupName));
                    AddStr(60, cY, string.Format("{0,-8}", port.Length > 8 ? port.Substring(0, 8) : port));
                    AddStr(69, cY, string.Format("{0,-12}", manualStart));
                    cY++;
                }
            }

            int h = cY;
            if (h != _lastNeededHeight)
            {
                _lastNeededHeight = h;
                HeightNeededChanged?.Invoke();
            }
            return true;
        }

        var horizontalOffset = 1;
        var currentX = horizontalOffset;
        var currentY = 0;
        var maxAvailableWidth = GetAvailableWidth();

        for (int i = 0; i < snapshot.Count; i++)
        {
            var service = snapshot[i];
            if (_serviceStatuses.TryGetValue(service, out var status))
            {
                var icon = ServiceColorManager.GetStatusIcon(status);
                var textLength = service.Length + (i < snapshot.Count - 1 ? 5 : 2);
                if (currentX + textLength > maxAvailableWidth && currentX > horizontalOffset)
                {
                    currentY++;
                    currentX = horizontalOffset;
                }

                SetAttribute(ServiceColorManager.GetColor(service));
                AddStr(currentX, currentY, service);
                currentX += service.Length;

                SetAttribute(normalAttr);
                AddStr(currentX, currentY, " ");
                currentX += 1;

                SetAttribute(ServiceColorManager.GetStatusColor(status));
                AddStr(currentX, currentY, icon);
                currentX += icon.Length;

                if (i < snapshot.Count - 1)
                {
                    SetAttribute(dividerAttr);
                    AddStr(currentX, currentY, " │ ");
                    currentX += 3;
                }
            }
        }

        int neededHeight = currentY + 1;
        if (neededHeight != _lastNeededHeight)
        {
            _lastNeededHeight = neededHeight;
            HeightNeededChanged?.Invoke();
        }

        return true;
    }
}

internal sealed class LogView : View
{
    public List<LogEntry> RawEntries { get; set; } = new();
    private List<FlatLogLine> _flatLines = new();
    
    private int _topRow = 0;
    private bool _autoScroll = true;
    private int _lastViewportWidth = -1;

    public LogView()
    {
        CanFocus = true;

        KeyDown += (sender, key) =>
        {
            if (key == Key.CursorUp) { ScrollUp(); }
            else if (key == Key.CursorDown) { ScrollDown(); }
            else if (key == Key.PageUp) { ScrollUp(Viewport.Height); }
            else if (key == Key.PageDown) { ScrollDown(Viewport.Height); }
        };

        MouseEvent += (sender, mouse) =>
        {
            if (mouse.Flags.HasFlag(MouseFlags.WheeledUp))
            {
                ScrollUp(3);
                mouse.Handled = true;
            }
            else if (mouse.Flags.HasFlag(MouseFlags.WheeledDown))
            {
                ScrollDown(3);
                mouse.Handled = true;
            }
        };
    }

    public void SetRawEntries(List<LogEntry> entries, bool forceScrollToBottom = false)
    {
        RawEntries = entries;
        RebuildFlatLines(forceScrollToBottom);
    }

    private void RebuildFlatLines(bool forceScrollToBottom = false)
    {
        int vw = Math.Max(20, Viewport.Width);
        _lastViewportWidth = vw;

        var newFlatLines = new List<FlatLogLine>();

        foreach (var entry in RawEntries)
        {
            var msg = entry.Message;
            if (string.IsNullOrEmpty(msg))
            {
                newFlatLines.Add(new FlatLogLine { Entry = entry, StartIndex = 0, Length = 0, IsContinuation = false });
                continue;
            }

            int lineStart = 0;
            bool isFirstLineOfMessage = true;

            while (lineStart <= msg.Length)
            {
                int lineEnd = msg.IndexOf('\n', lineStart);
                if (lineEnd == -1) lineEnd = msg.Length;

                int lineLen = lineEnd - lineStart;
                
                if (lineLen == 0 && lineStart < msg.Length)
                {
                    newFlatLines.Add(new FlatLogLine { Entry = entry, StartIndex = lineStart, Length = 0, IsContinuation = !isFirstLineOfMessage });
                    lineStart = lineEnd + 1;
                    isFirstLineOfMessage = false;
                    continue;
                }
                
                int offset = 0;
                bool isFirstChunk = isFirstLineOfMessage;

                while (offset < lineLen || (offset == 0 && lineLen == 0))
                {
                    int prefixLen = entry.ServiceName.Length + 3 + (isFirstChunk && entry.IsError ? 6 : 2);
                    int currentMaxLen = Math.Max(10, vw - prefixLen);

                    int lengthToTake = Math.Min(currentMaxLen, lineLen - offset);

                    newFlatLines.Add(new FlatLogLine
                    {
                        Entry = entry,
                        StartIndex = lineStart + offset,
                        Length = lengthToTake,
                        IsContinuation = !isFirstChunk
                    });

                    offset += lengthToTake;
                    isFirstChunk = false;
                    
                    if (lengthToTake == 0) break;
                }

                if (lineEnd == msg.Length) break;
                lineStart = lineEnd + 1;
                isFirstLineOfMessage = false;
            }
        }

        _flatLines = newFlatLines;
        var maxTop = Math.Max(0, _flatLines.Count - Viewport.Height);

        if (forceScrollToBottom || _autoScroll)
        {
            _autoScroll = true;
            _topRow = maxTop;
        }
        else
        {
            _topRow = Math.Min(_topRow, maxTop);
        }

        SetNeedsDraw();
    }

    public void ScrollUp(int lines = 1)
    {
        _autoScroll = false;
        _topRow = Math.Max(0, _topRow - lines);
        SetNeedsDraw();
    }

    public void ScrollDown(int lines = 1)
    {
        var maxTop = Math.Max(0, _flatLines.Count - Viewport.Height);
        _topRow = Math.Min(maxTop, _topRow + lines);
        if (_topRow >= maxTop)
        {
            _autoScroll = true;
        }
        SetNeedsDraw();
    }

    public void ScrollToBottom()
    {
        _autoScroll = true;
        _topRow = Math.Max(0, _flatLines.Count - Viewport.Height);
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);

        if (Viewport.Width != _lastViewportWidth && Viewport.Width > 0)
        {
            RebuildFlatLines(forceScrollToBottom: false);
        }

        var bounds = Viewport;
        var normalAttr = new Attribute(Color.Gray, Color.None);
        var errAttr = new Attribute(Color.BrightRed, Color.None);
        var contAttr = new Attribute(Color.DarkGray, Color.None);

        for (int i = 0; i < bounds.Height; i++)
        {
            int idx = _topRow + i;

            if (idx >= _flatLines.Count)
            {
                break;
            }

            var entry = _flatLines[idx];
            var logEntry = entry.Entry;

            SetAttribute(ServiceColorManager.GetColor(logEntry.ServiceName));
            AddStr(0, i, $"[{logEntry.ServiceName}] ");

            var col = logEntry.ServiceName.Length + 3;

            if (logEntry.IsError && !entry.IsContinuation)
            {
                SetAttribute(errAttr);
                AddStr(col, i, "[ERR] ");
                col += 6;
            }
            else if (entry.IsContinuation)
            {
                SetAttribute(normalAttr);
                AddStr(col, i, "  ");
                col += 2;
            }

            SetAttribute(logEntry.IsError ? errAttr : (entry.IsContinuation ? contAttr : normalAttr));
            
            if (entry.Length > 0)
            {
                var msgChunk = logEntry.Message.Substring(entry.StartIndex, entry.Length).Replace('\t', ' ');
                var maxMsgLen = Math.Max(0, bounds.Width - col);
                if (msgChunk.Length > maxMsgLen) msgChunk = msgChunk.Substring(0, maxMsgLen);
                AddStr(col, i, msgChunk);
            }
        }

        return true;
    }
}


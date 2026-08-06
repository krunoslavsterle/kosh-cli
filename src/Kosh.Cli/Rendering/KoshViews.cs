using System.Collections.Concurrent;
using System.Text;
using Kosh.Core.Runtime;
using Terminal.Gui;

namespace Kosh.Cli.Rendering;

internal static class ServiceColorManager
{
    private static readonly Color[] _palette = 
    {
        Color.BrightCyan,
        Color.BrightGreen,
        Color.BrightMagenta,
        Color.BrightYellow,
        Color.BrightBlue,
        Color.Cyan,
        Color.Green,
        Color.Magenta,
        Color.Brown,
        Color.Blue
    };

    private static readonly ConcurrentDictionary<string, Terminal.Gui.Attribute> _colorMap = new();

    public static Terminal.Gui.Attribute GetColor(string serviceName)
    {
        return _colorMap.GetOrAdd(serviceName, name =>
        {
            var hash = Math.Abs(name.GetHashCode());
            var color = _palette[hash % _palette.Length];
            return Terminal.Gui.Attribute.Make(color, Color.Black);
        });
    }

    public static Terminal.Gui.Attribute GetStatusColor(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Running => Terminal.Gui.Attribute.Make(Color.BrightGreen, Color.Black),
            ServiceStatus.Ready => Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.Black),
            ServiceStatus.Stopped => Terminal.Gui.Attribute.Make(Color.Gray, Color.Black),
            ServiceStatus.Failed => Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black),
            ServiceStatus.Starting => Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.Black),
            ServiceStatus.NotStarted => Terminal.Gui.Attribute.Make(Color.DarkGray, Color.Black),
            _ => Terminal.Gui.Attribute.Make(Color.Gray, Color.Black)
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
    public string ServiceName;
    public string Message;
    public bool IsError;
    public bool IsContinuation;
}

internal sealed class HeaderView : View
{
    private readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses;
    private readonly List<string> _orderedServices;

    public int GetAvailableWidth()
    {
        if (Bounds.Width > 0) return Bounds.Width;
        if (SuperView != null && SuperView.Bounds.Width > 2) return SuperView.Bounds.Width - 2;
        return Math.Max(10, Console.WindowWidth - 4);
    }

    public int GetNeededHeight()
    {
        int availableWidth = GetAvailableWidth();

        List<string> snapshot;
        lock (_orderedServices) { snapshot = _orderedServices.ToList(); }
        if (snapshot.Count == 0) return 1;

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

    public HeaderView(ConcurrentDictionary<string, ServiceStatus> statuses, List<string> ordered)
    {
        _serviceStatuses = statuses;
        _orderedServices = ordered;
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        Clear();

        List<string> snapshot;
        lock (_orderedServices) { snapshot = _orderedServices.ToList(); }

        var normalAttr = Terminal.Gui.Attribute.Make(Color.White, Color.Black);
        var dividerAttr = Terminal.Gui.Attribute.Make(Color.DarkGray, Color.Black);

        Move(0, 0);
        Driver.SetAttribute(normalAttr);

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

                Move(currentX, currentY);
                Driver.SetAttribute(ServiceColorManager.GetColor(service));
                Driver.AddStr(service);
                Driver.SetAttribute(normalAttr);
                Driver.AddStr(" ");
                Driver.SetAttribute(ServiceColorManager.GetStatusColor(status));
                Driver.AddStr(icon);

                if (i < snapshot.Count - 1)
                {
                    Driver.SetAttribute(dividerAttr);
                    Driver.AddStr(" │ ");
                }

                currentX += textLength;
            }
        }
    }
}

internal sealed class LogView : View
{
    public List<FlatLogLine> Entries { get; set; } = new();
    
    private int _topRow = 0;
    private bool _autoScroll = true;
    
    public void SetLines(List<FlatLogLine> lines, bool forceScrollToBottom = false)
    {
        Entries = lines;
        var maxTop = Math.Max(0, Entries.Count - Bounds.Height);

        if (forceScrollToBottom || _autoScroll)
        {
            _autoScroll = true;
            _topRow = maxTop;
        }
        else
        {
            _topRow = Math.Min(_topRow, maxTop);
        }

        SetNeedsDisplay();
    }
    
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        var flags = keyEvent.Key;
        if (flags == Key.CursorUp) { ScrollUp(); return true; }
        if (flags == Key.CursorDown) { ScrollDown(); return true; }
        if (flags == Key.PageUp) { ScrollUp(Bounds.Height); return true; }
        if (flags == Key.PageDown) { ScrollDown(Bounds.Height); return true; }
        return base.ProcessKey(keyEvent);
    }

    public override bool MouseEvent(MouseEvent mouseEvent)
    {
        if (mouseEvent.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            ScrollUp(3);
            return true;
        }
        if (mouseEvent.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            ScrollDown(3);
            return true;
        }
        return base.MouseEvent(mouseEvent);
    }

    private void ScrollUp(int lines = 1)
    {
        _autoScroll = false;
        _topRow = Math.Max(0, _topRow - lines);
        SetNeedsDisplay();
    }

    private void ScrollDown(int lines = 1)
    {
        var maxTop = Math.Max(0, Entries.Count - Bounds.Height);
        _topRow = Math.Min(maxTop, _topRow + lines);
        if (_topRow >= maxTop)
        {
            _autoScroll = true;
        }
        SetNeedsDisplay();
    }
    
    public void ScrollToBottom()
    {
        _autoScroll = true;
        _topRow = Math.Max(0, Entries.Count - Bounds.Height);
        SetNeedsDisplay();
    }
    
    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        Clear();

        var normalAttr = Terminal.Gui.Attribute.Make(Color.Gray, Color.Black);
        var errAttr = Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black);
        var contAttr = Terminal.Gui.Attribute.Make(Color.DarkGray, Color.Black);

        for (int i = 0; i < bounds.Height; i++)
        {
            int idx = _topRow + i;
            if (idx >= Entries.Count) break;

            var entry = Entries[idx];
            Move(0, i);
            
            var sColor = ServiceColorManager.GetColor(entry.ServiceName);
            Driver.SetAttribute(sColor);
            Driver.AddStr($"[{entry.ServiceName}] ");

            if (entry.IsError && !entry.IsContinuation)
            {
                Driver.SetAttribute(errAttr);
                Driver.AddStr("[ERR] ");
            }
            else if (entry.IsContinuation)
            {
                Driver.SetAttribute(normalAttr);
                Driver.AddStr("  "); // Indent for continuation
            }
            
            Driver.SetAttribute(entry.IsError ? errAttr : (entry.IsContinuation ? contAttr : normalAttr));
            var msg = entry.Message.Replace('\t', ' ');
            
            // Avoid driver crashing on too long strings
            var prefixLen = entry.ServiceName.Length + 3 + (entry.IsError && !entry.IsContinuation ? 6 : (entry.IsContinuation ? 2 : 0));
            var maxMsgLen = Math.Max(0, bounds.Width - prefixLen);
            if (msg.Length > maxMsgLen) msg = msg.Substring(0, maxMsgLen);
            
            Driver.AddStr(msg);
        }
    }
}

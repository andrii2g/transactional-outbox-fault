using System.Globalization;
using System.Xml.Linq;
namespace OutboxFaultLab;

public static class TimelineSvgWriter
{
    private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
    public static void Write(string path, RunResult r) => Render(path, r.ScenarioId, r.Trace,
        $"Final: lost={r.Metrics.LostEvents}  ghost={r.Metrics.GhostEvents}  pending={r.Metrics.PendingOutboxRows}  duplicates={r.Metrics.DuplicateDeliveries}  {r.ExecutionStatus}", false);
    public static void WriteConsumer(string path, ConsumerResult r) => Render(path, "Consumer: " + r.Mode, r.Trace,
        $"Applied effects={r.Final.Projection.Sum(p => p.AppliedCount)}  duplicateSkipped={r.DuplicateSkipped}", true);
    private static void Render(string path, string title, IReadOnlyList<TraceEvent> trace, string summary, bool consumer)
    {
        string Lane(TraceEvent e) => consumer ? e.Kind == "ConsumerMutation" || e.Kind == "ConsumerCommitted" || e.Kind == "ConsumerRolledBack" ? "Consumer SQLite" : "Consumer" :
            e.Kind is "MutationExecuted" or "TransactionCommitted" or "TransactionRolledBack" ? "SQLite" :
            e.Kind is "BrokerAccepted" or "BrokerAck" or "BrokerRejected" or "AckLost" ? "Broker" : e.Component == Component.Dispatcher ? "Dispatcher" : "Producer";
        var lanes = consumer ? new[] { "Consumer", "Consumer SQLite" } : new[] { "Producer", "SQLite", "Dispatcher", "Broker" };
        string Label(TraceEvent e) => $"#{e.Sequence}  t={e.Tick}  epoch={e.Epoch}  {e.Kind} {e.Point} {e.TransactionId} {e.EventId}" + (e.Kind is "MutationExecuted" or "ConsumerMutation" ? " TENTATIVE" : "") + (e.Details.TryGetValue("operation", out var op) ? " " + op : "");
        int RowHeight(TraceEvent e) => 58 + 16 * ((Label(e).Length + 10) / 130);
        var height = 190 + lanes.Sum(l => Math.Max(58, trace.Where(e => Lane(e) == l).Sum(RowHeight)) + 42);
        var svg = new XElement(Ns + "svg", new XAttribute("viewBox", $"0 0 1280 {height}"), new XAttribute("width", 1280), new XAttribute("height", height), new XAttribute("role", "img"), new XAttribute("aria-labelledby", "title description"),
            new XElement(Ns + "title", new XAttribute("id", "title"), title), new XElement(Ns + "desc", new XAttribute("id", "description"), "Executed trace grouped by owner. Horizontal position measures logical ticks; sequence labels retain ordering when ticks tie. " + summary));
        svg.Add(new XElement(Ns + "rect", new XAttribute("width", 1280), new XAttribute("height", height), new XAttribute("fill", "#f8fafc")));
        var defs = new XElement(Ns + "defs", new XElement(Ns + "marker", new XAttribute("id", "arrow"), new XAttribute("markerWidth", 8), new XAttribute("markerHeight", 8), new XAttribute("refX", 7), new XAttribute("refY", 3), new XAttribute("orient", "auto"), new XElement(Ns + "path", new XAttribute("d", "M0,0 L0,6 L7,3 z"), new XAttribute("fill", "#64748b")))); svg.Add(defs);
        Text(24, 30, title, 20); Text(24, 57, summary, 14);
        Text(24, 81, "Broker journal survives modeled crashes. Logical ticks are not milliseconds.", 13);
        Text(24, 104, "Dashed = tentative mutation; solid = committed. X = crash; + = restart. Equal ticks retain #sequence.", 12);
        var max = Math.Max(1, trace.Count == 0 ? 0 : trace.Max(e => e.Tick));
        double X(long tick) => 180 + tick / (double)max * 1040;
        Line(180, 135, 1220, 135, "#64748b");
        foreach (var tick in Enumerable.Range(0, 6).Select(i => (long)(max * (i / 5.0))).Distinct()) { var x = X(tick); Line(x, 130, x, 140, "#64748b"); Text(x - 5, 155, tick.ToString(CultureInfo.InvariantCulture), 11); }
        Text(30, 140, "Logical tick →", 13);
        var top = 175; var accepted = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = trace.Where(e => e.Kind == "BrokerAccepted").Where(e => !accepted.Add(e.EventId!)).Select(e => e.Sequence).ToHashSet();
        foreach (var lane in lanes)
        {
            var events = trace.Where(e => Lane(e) == lane).ToArray(); var laneHeight = Math.Max(58, events.Sum(RowHeight)) + 42;
            svg.Add(new XElement(Ns + "rect", new XAttribute("x", 16), new XAttribute("y", top), new XAttribute("width", 1248), new XAttribute("height", laneHeight), new XAttribute("fill", lane is "SQLite" or "Consumer SQLite" ? "#e2e8f0" : "#ffffff"), new XAttribute("stroke", "#cbd5e1")));
            Text(30, top + 24, lane, 16); var y = top + 46;
            foreach (var e in events)
            {
                var label = Label(e) + (duplicates.Contains(e.Sequence) ? " DUPLICATE" : "");
                for (var start = 0; start < label.Length; start += 130) Text(180, y + 16 * (start / 130), label.Substring(start, Math.Min(130, label.Length - start)), 12);
                var x = X(e.Tick); var markerY = y + RowHeight(e) - 38;
                var color = e.Kind == "CrashInjected" ? "#b91c1c" : e.Kind == "RuntimeRestarted" ? "#15803d" : "#334155";
                var line = Line(180, markerY, Math.Max(181, x), markerY, "#94a3b8");
                if (e.Kind is "MutationExecuted" or "ConsumerMutation") line.Add(new XAttribute("stroke-dasharray", "5 4"));
                if (e.Kind is "PublishInvoked" or "BrokerAccepted") line.Add(new XAttribute("marker-end", "url(#arrow)"));
                if (e.Kind == "CrashInjected") { Line(x - 5, markerY - 5, x + 5, markerY + 5, color); Line(x - 5, markerY + 5, x + 5, markerY - 5, color); }
                else if (e.Kind == "RuntimeRestarted") { Line(x - 5, markerY, x + 5, markerY, color); Line(x, markerY - 5, x, markerY + 5, color); }
                else if (e.Kind is "TransactionCommitted" or "ConsumerCommitted") { Line(x, markerY - 7, x, markerY + 7, color); Line(x + 3, markerY - 7, x + 3, markerY + 7, color); }
                else svg.Add(new XElement(Ns + "circle", new XAttribute("cx", F(x)), new XAttribute("cy", markerY), new XAttribute("r", 4), new XAttribute("fill", color)));
                y += RowHeight(e);
            }
            if (events.Length == 0) Text(180, y, "No runtime events in this lane", 12);
            top += laneHeight;
        }
        File.WriteAllText(path, new XDocument(svg).ToString().Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", new System.Text.UTF8Encoding(false));
        void Text(double x, double y, string text, int size) => svg.Add(new XElement(Ns + "text", new XAttribute("x", F(x)), new XAttribute("y", F(y)), new XAttribute("font-family", "sans-serif"), new XAttribute("font-size", size), new XAttribute("fill", "#0f172a"), text));
        XElement Line(double x1, double y1, double x2, double y2, string color)
        { var line = new XElement(Ns + "line", new XAttribute("x1", F(x1)), new XAttribute("y1", F(y1)), new XAttribute("x2", F(x2)), new XAttribute("y2", F(y2)), new XAttribute("stroke", color), new XAttribute("stroke-width", 2)); svg.Add(line); return line; }
    }
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

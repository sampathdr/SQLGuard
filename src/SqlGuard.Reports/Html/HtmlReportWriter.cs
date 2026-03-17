using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace SqlGuard.Reports.Html
{
    ///TODO: Use HTML Templates instead of string concatenation for better maintainability and readability.
   

    /// <summary>
    /// Writes a self-contained, single-file HTML report designed for:
    ///   • DBA and security team review
    ///   • Audit documentation
    ///   • Compliance evidence packages
    ///
    /// Features:
    ///   • Zero external dependencies — all CSS/JS embedded inline
    ///   • Responsive layout, printable
    ///   • Severity colour coding throughout
    ///   • Interactive collapsible sections
    ///   • Doughnut chart (Chart.js embedded via CDN — degrades gracefully offline)
    ///   • Each finding includes rule ID, severity, status, remediation, compliance refs
    /// </summary>
    public sealed class HtmlReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Html;
        public string FileExtension => ".html";
        public string ContentType => "text/html";

        public async Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            var html = BuildHtml(result);
            await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
            await writer.WriteAsync(html.AsMemory(), ct);
        }

        private static string BuildHtml(ScanResult result)
        {
            var sb = new StringBuilder(32768);

            sb.Append($$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>SqlGuard Report — {{H(result.Label ?? result.Host)}} — {{result.StartedAt:yyyy-MM-dd}}</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
  <style>
    :root {
      --critical: #dc2626; --high: #ea580c; --medium: #ca8a04;
      --low: #2563eb; --info: #6b7280; --pass: #16a34a;
      --warn: #d97706; --fail: #dc2626; --bg: #f9fafb;
      --card: #ffffff; --border: #e5e7eb; --text: #111827;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
           background: var(--bg); color: var(--text); font-size: 14px; line-height: 1.5; }
    .container { max-width: 1280px; margin: 0 auto; padding: 24px; }
    header { background: #1e293b; color: white; padding: 24px 32px; margin-bottom: 32px; border-radius: 8px; }
    header h1 { font-size: 24px; font-weight: 700; }
    header .subtitle { color: #94a3b8; font-size: 13px; margin-top: 4px; }
    .verdict { padding: 12px 20px; border-radius: 6px; margin-top: 16px; font-weight: 600; font-size: 15px; }
    .verdict.pass { background: #dcfce7; color: #166534; border-left: 4px solid #16a34a; }
    .verdict.fail { background: #fee2e2; color: #991b1b; border-left: 4px solid #dc2626; }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 16px; margin-bottom: 32px; }
    .card { background: var(--card); border: 1px solid var(--border); border-radius: 8px;
            padding: 20px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.05); }
    .card .num { font-size: 32px; font-weight: 700; }
    .card .lbl { font-size: 12px; color: #6b7280; text-transform: uppercase; letter-spacing: .05em; margin-top: 4px; }
    .card.critical .num { color: var(--critical); }
    .card.high .num    { color: var(--high); }
    .card.medium .num  { color: var(--medium); }
    .card.pass .num    { color: var(--pass); }
    .card.warn .num    { color: var(--warn); }
    .card.score .num   { color: #1e293b; }
    .section { background: var(--card); border: 1px solid var(--border); border-radius: 8px;
               margin-bottom: 24px; overflow: hidden; }
    .section-header { padding: 16px 20px; background: #f8fafc; border-bottom: 1px solid var(--border);
                      cursor: pointer; display: flex; justify-content: space-between; align-items: center; }
    .section-header h2 { font-size: 16px; font-weight: 600; }
    .section-header .toggle { color: #6b7280; font-size: 20px; user-select: none; }
    .section-body { padding: 0; overflow: hidden; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th { background: #f1f5f9; padding: 10px 14px; text-align: left; font-weight: 600;
         border-bottom: 2px solid var(--border); color: #374151; font-size: 12px;
         text-transform: uppercase; letter-spacing: .04em; white-space: nowrap; }
    td { padding: 10px 14px; border-bottom: 1px solid var(--border); vertical-align: top; }
    tr:last-child td { border-bottom: none; }
    tr:hover td { background: #f8fafc; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px;
             font-weight: 600; text-transform: uppercase; letter-spacing: .03em; }
    .badge.critical { background: #fee2e2; color: var(--critical); }
    .badge.high     { background: #ffedd5; color: var(--high); }
    .badge.medium   { background: #fef9c3; color: var(--medium); }
    .badge.low      { background: #dbeafe; color: var(--low); }
    .badge.info     { background: #f3f4f6; color: var(--info); }
    .badge.pass     { background: #dcfce7; color: var(--pass); }
    .badge.fail     { background: #fee2e2; color: var(--fail); }
    .badge.warn     { background: #fef9c3; color: var(--warn); }
    .badge.error    { background: #fee2e2; color: var(--fail); }
    .badge.na       { background: #f3f4f6; color: var(--info); }
    .mandatory-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%;
                     background: var(--critical); margin-right: 4px; vertical-align: middle; }
    .rule-id { font-family: 'Courier New', monospace; font-size: 12px; color: #6b7280; }
    .detail  { font-size: 12px; color: #374151; margin-top: 4px; }
    .remediation { background: #f0fdf4; border-left: 3px solid var(--pass);
                   padding: 8px 12px; margin-top: 6px; border-radius: 0 4px 4px 0;
                   font-size: 12px; color: #166534; white-space: pre-wrap; font-family: monospace; }
    .refs { margin-top: 4px; }
    .ref  { display: inline-block; background: #eff6ff; color: #1d4ed8; border-radius: 4px;
            padding: 1px 6px; font-size: 11px; margin: 1px; }
    .chart-container { padding: 24px; display: flex; justify-content: center; align-items: center; gap: 40px; }
    .chart-canvas { max-width: 220px; max-height: 220px; }
    .chart-legend { list-style: none; }
    .chart-legend li { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; font-size: 13px; }
    .legend-dot { width: 12px; height: 12px; border-radius: 50%; flex-shrink: 0; }
    .server-info { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                   gap: 16px; padding: 20px; }
    .info-item { display: flex; flex-direction: column; gap: 2px; }
    .info-item .label { font-size: 11px; text-transform: uppercase; color: #6b7280; letter-spacing: .05em; }
    .info-item .value { font-size: 14px; font-weight: 500; }
    footer { margin-top: 32px; padding: 16px; text-align: center; color: #9ca3af; font-size: 12px; }
    @media print {
      .section-header { cursor: default; }
      .toggle { display: none; }
      .section-body { display: block !important; }
    }
  </style>
</head>
<body>
<div class="container">
""");

            // ── Header ────────────────────────────────────────────────────────────
            var verdictClass = result.MandatoryFailCount == 0 ? "pass" : "fail";
            var verdictText = result.MandatoryFailCount == 0
                ? $"✓ All {result.TotalChecked} mandatory checks passed"
                : $"✗ {result.MandatoryFailCount} mandatory check(s) failed";

            sb.Append($$"""
<header>
  <h1>🛡️ SqlGuard Security Scan Report</h1>
  <div class="subtitle">
    {{H(result.Engine)}} · {{H(result.Host)}} / {{H(result.Database)}}
    {{(result.ServerVersion is not null ? $"· v{H(result.ServerVersion)}" : "")}}
    {{(!string.IsNullOrWhiteSpace(result.Label) ? $"· {H(result.Label)}" : "")}}
    · {{result.StartedAt:yyyy-MM-dd HH:mm}} UTC
  </div>
  <div class="verdict {{verdictClass}}">{{verdictText}}</div>
</header>
""");

            // ── Score cards ───────────────────────────────────────────────────────
            var critFails = result.Results.Count(r => r.Status == RuleStatus.Fail && r.Severity == Severity.Critical);
            var highFails = result.Results.Count(r => r.Status == RuleStatus.Fail && r.Severity == Severity.High);
            var medFails = result.Results.Count(r => r.Status == RuleStatus.Fail && r.Severity == Severity.Medium);

            sb.Append($$"""
<div class="cards">
  <div class="card score"><div class="num">{{result.ComplianceScore:F0}}%</div><div class="lbl">Score</div></div>
  <div class="card critical"><div class="num">{{critFails}}</div><div class="lbl">Critical Fails</div></div>
  <div class="card high"><div class="num">{{highFails}}</div><div class="lbl">High Fails</div></div>
  <div class="card medium"><div class="num">{{medFails}}</div><div class="lbl">Medium Fails</div></div>
  <div class="card pass"><div class="num">{{result.PassCount}}</div><div class="lbl">Passed</div></div>
  <div class="card warn"><div class="num">{{result.WarnCount}}</div><div class="lbl">Warnings</div></div>
</div>
""");

            // ── Chart + Server info ───────────────────────────────────────────────
            sb.Append($$"""
<div class="section">
  <div class="section-header" onclick="toggle(this)">
    <h2>📊 Overview</h2><span class="toggle">▼</span>
  </div>
  <div class="section-body">
    <div class="chart-container">
      <canvas id="severityChart" class="chart-canvas"></canvas>
      <ul class="chart-legend">
        <li><span class="legend-dot" style="background:#dc2626"></span>Critical ({{critFails}} fail)</li>
        <li><span class="legend-dot" style="background:#ea580c"></span>High ({{highFails}} fail)</li>
        <li><span class="legend-dot" style="background:#ca8a04"></span>Medium ({{medFails}} fail)</li>
        <li><span class="legend-dot" style="background:#16a34a"></span>Passed ({{result.PassCount}})</li>
        <li><span class="legend-dot" style="background:#d97706"></span>Warnings ({{result.WarnCount}})</li>
      </ul>
    </div>
    <div class="server-info">
      {{InfoItem("Engine", result.Engine)}}
      {{InfoItem("Host", result.Host)}}
      {{InfoItem("Database", result.Database)}}
      {{InfoItem("Server Version", result.ServerVersion ?? "unknown")}}
      {{InfoItem("Packs Loaded", string.Join(", ", result.PacksLoaded))}}
      {{InfoItem("Duration", $"{result.Duration.TotalSeconds:F2}s")}}
      {{InfoItem("Scan ID", result.ScanId.ToString())}}
    </div>
  </div>
</div>
""");

            // ── Failed findings ────────────────────────────────────────────────────
            var failures = result.Results
                .Where(r => r.Status is RuleStatus.Fail or RuleStatus.Warn)
                .OrderByDescending(r => r.Severity).ThenBy(r => r.RuleId)
                .ToList();

            sb.Append($$"""
<div class="section">
  <div class="section-header" onclick="toggle(this)">
    <h2>❌ Failed &amp; Warning Checks ({{failures.Count}})</h2><span class="toggle">▼</span>
  </div>
  <div class="section-body">
    <table>
      <thead><tr>
        <th>Rule ID</th><th>Severity</th><th>Status</th><th>Category</th>
        <th>Title &amp; Detail</th><th>References</th>
      </tr></thead>
      <tbody>
""");

            foreach (var r in failures)
            {
                var sevClass = r.Severity.ToString().ToLower();
                var statClass = r.Status.ToString().ToLower();
                var mandDot = r.Mandatory ? "<span class='mandatory-dot' title='Mandatory'></span>" : "";

                sb.Append($"""
        <tr>
          <td class="rule-id">{mandDot}{H(r.RuleId)}</td>
          <td><span class="badge {sevClass}">{H(r.Severity.ToString())}</span></td>
          <td><span class="badge {statClass}">{H(r.Status.ToString())}</span></td>
          <td>{H(r.Category ?? "")}</td>
          <td>
            <strong>{H(r.Title)}</strong>
            {(r.Detail is not null ? $"<div class='detail'>{H(r.Detail)}</div>" : "")}
            {(r.Remediation is not null ? $"<div class='remediation'>{H(r.Remediation)}</div>" : "")}
          </td>
          <td class="refs">{string.Join("", r.ComplianceReferences.Select(ref_ => $"<span class='ref'>{H(ref_)}</span>"))}</td>
        </tr>
""");
            }

            sb.AppendLine("      </tbody></table></div></div>");

            // ── Passed findings ───────────────────────────────────────────────────
            var passed = result.Results.Where(r => r.Status == RuleStatus.Pass)
                .OrderBy(r => r.RuleId).ToList();

            sb.Append($$"""
<div class="section">
  <div class="section-header" onclick="toggle(this)">
    <h2>✅ Passed Checks ({{passed.Count}})</h2><span class="toggle">►</span>
  </div>
  <div class="section-body" style="display:none">
    <table>
      <thead><tr><th>Rule ID</th><th>Severity</th><th>Category</th><th>Title</th><th>Detail</th></tr></thead>
      <tbody>
""");

            foreach (var r in passed)
            {
                sb.AppendLine($"""
        <tr>
          <td class="rule-id">{H(r.RuleId)}</td>
          <td><span class="badge {r.Severity.ToString().ToLower()}">{H(r.Severity.ToString())}</span></td>
          <td>{H(r.Category ?? "")}</td>
          <td>{H(r.Title)}</td>
          <td class="detail">{H(r.Detail ?? "")}</td>
        </tr>
""");
            }
            sb.AppendLine("      </tbody></table></div></div>");

            // ── Footer + JS ───────────────────────────────────────────────────────
            sb.Append($$"""
<footer>
  Generated by <strong>SqlGuard</strong> ·
  <a href="https://github.com/your-org/sqlguard">github.com/your-org/sqlguard</a> ·
  Scan ID: {{result.ScanId}}
</footer>
</div><!-- /container -->
<script>
function toggle(header) {
  const body = header.nextElementSibling;
  const icon = header.querySelector('.toggle');
  const hidden = body.style.display === 'none';
  body.style.display = hidden ? '' : 'none';
  icon.textContent = hidden ? '▼' : '►';
}
window.addEventListener('load', function() {
  const ctx = document.getElementById('severityChart');
  if (!ctx || typeof Chart === 'undefined') return;
  new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: ['Critical', 'High', 'Medium', 'Passed', 'Warnings'],
      datasets: [{
        data: [{{critFails}}, {{highFails}}, {{medFails}}, {{result.PassCount}}, {{result.WarnCount}}],
        backgroundColor: ['#dc2626','#ea580c','#ca8a04','#16a34a','#d97706'],
        borderWidth: 2, borderColor: '#fff'
      }]
    },
    options: {
      responsive: true, maintainAspectRatio: true,
      plugins: { legend: { display: false } },
      cutout: '65%'
    }
  });
});
</script>
</body></html>
""");

            return sb.ToString();
        }

        private static string H(string? text) => HttpUtility.HtmlEncode(text ?? string.Empty);

        private static string InfoItem(string label, string value) =>
            $"<div class='info-item'><span class='label'>{label}</span><span class='value'>{H(value)}</span></div>";
    }
}

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Configuration;
using SqlGuard.Core.Extensions;
using SqlGuard.Core.Models;
using SqlGuard.Providers.PostgreSQL;
using SqlGuard.Providers.SqlServer;
using SqlGuard.Reports;
using SqlGuard.Reports.Console;
using SqlGuard.Reports.Csv;
using SqlGuard.Reports.Html;
using SqlGuard.Reports.Json;
using SqlGuard.Reports.Markdown;
using SqlGuard.Reports.Sarif;

// ═══════════════════════════════════════════════════════════════════════════════
// Exit code contract:
//   0  All checks passed per --fail-on threshold
//   1  One or more mandatory/threshold failures
//   2  Execution error (bad config, cannot connect, unexpected exception)
// ═══════════════════════════════════════════════════════════════════════════════

var root = new RootCommand("sqlguard — Open-source Database Security Scanner");
root.Subcommands.Add(ScanCommand.Build());
root.Subcommands.Add(InventoryCommand.Build());
root.Subcommands.Add(ListRulesCommand.Build());
root.Subcommands.Add(ValidateRuleCommand.Build());
root.Subcommands.Add(InitConfigCommand.Build());

return await root.Parse(args).InvokeAsync();

// ═══════════════════════════════════════════════════════════════════════════════
// Shared helpers
// ═══════════════════════════════════════════════════════════════════════════════

static class Helpers
{
    public static ServiceProvider BuildServiceProvider(bool verbose) =>
        new ServiceCollection()
            .AddSqlGuardCore(o =>
                o.MinimumLogLevel = verbose ? LogLevel.Debug : LogLevel.Warning)
            .AddSqlGuardProvider<SqlServerProvider>()
            .AddSqlGuardProvider<PostgreSQLProvider>()
            .AddSqlGuardReportWriter<ConsoleReportWriter>()
            .AddSqlGuardReportWriter<JsonReportWriter>()
            .AddSqlGuardReportWriter<HtmlReportWriter>()
            .AddSqlGuardReportWriter<SarifReportWriter>()
            .AddSqlGuardReportWriter<CsvReportWriter>()
            .AddSqlGuardReportWriter<MarkdownReportWriter>()
            .AddSingleton<IReportPipeline, ReportPipeline>()
            .BuildServiceProvider();

    public static int DetermineExitCode(ScanResult result, string failOn) =>
        failOn.ToLowerInvariant() switch
        {
            "mandatory" => result.MandatoryFailCount > 0 ? 1 : 0,
            "critical" => result.Passed(Severity.Critical) ? 0 : 1,
            "high" => result.Passed(Severity.High) ? 0 : 1,
            "medium" => result.Passed(Severity.Medium) ? 0 : 1,
            "low" => result.Passed(Severity.Low) ? 0 : 1,
            _ => result.MandatoryFailCount > 0 ? 1 : 0
        };

    public static IReadOnlyList<ReportFormat> ParseFormats(string[] raw) =>
        raw.SelectMany(t =>
                t.Split(',', StringSplitOptions.RemoveEmptyEntries
                           | StringSplitOptions.TrimEntries))
           .Select(t => Enum.TryParse<ReportFormat>(t, ignoreCase: true, out var f)
               ? f : (ReportFormat?)null)
           .Where(f => f is not null)
           .Select(f => f!.Value)
           .Distinct()
           .ToList();

    public static string Sanitise(string s) =>
        new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-').ToLowerInvariant();

    public static string SevColor(Severity s) => s switch
    {
        Severity.Critical => "red",
        Severity.High => "darkorange",
        Severity.Medium => "yellow",
        _ => "grey"
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// scan
// ─────────────────────────────────────────────────────────────────────────────

static class ScanCommand
{
    // Option<T> constructor signatures in System.CommandLine 2.0.5:
    //   Option<T>(string name, string? description = null)
    //   Option<T>(string name, Func<T> getDefaultValue, string? description = null)
    //   Option<T>(string[] aliases, string? description = null)
    //   Option<T>(string[] aliases, Func<T> getDefaultValue, string? description = null)
    // Use new string[] { } for aliases — never collection expression syntax ["..."]
    // which the compiler misreads as char collection on string.

    public static Command Build()
    {
        var cmd = new Command("scan", "Run security rule packs against a single database");

        // ── Connection ────────────────────────────────────────────────────────
        var configOpt = new Option<FileInfo?>("--config", "Path to sqlguard.yml config file");
        var profileOpt = new Option<string?>("--profile", "Named server profile from config file");
        var engineOpt = new Option<string?>("--engine", "Database engine: SqlServer | PostgreSQL");
        var hostOpt = new Option<string?>("--host", "Server hostname or IP");
        var portOpt = new Option<int>("--port", "Port number (0 = engine default)");
        var databaseOpt = new Option<string?>("--database", "Database or catalog name");
        var userOpt = new Option<string?>("--user", "Login username");
        var passEnvOpt = new Option<string?>("--password-env", "Name of the env var containing the password");
        var labelOpt = new Option<string?>("--label", "Friendly name for this target in reports");

        // ── Scan behaviour ────────────────────────────────────────────────────
        var packsOpt = new Option<string[]>("--packs", "Rule packs to load (space or comma-separated)")
        { DefaultValueFactory = _ => new string[] { "sqlserver-core" } };
        packsOpt.AllowMultipleArgumentsPerToken = true;

        var failOnOpt = new Option<string>("--fail-on", "Exit 1 when: mandatory | critical | high | medium")
        { DefaultValueFactory = _ => "mandatory" };

        var minSevOpt = new Option<Severity>("--min-severity", "Minimum severity level to include")
        { DefaultValueFactory = _ => Severity.Low };

        var mandOnlyOpt = new Option<bool>("--mandatory-only", "Only run mandatory rules");
        var verboseOpt = new Option<bool>("--verbose", "Show diagnostic log output");

        // ── Output ────────────────────────────────────────────────────────────
        var reportOpt = new Option<string[]>("--report", "Report formats")
        { DefaultValueFactory = _ => new string[] { "console", "html", "json" } };
        reportOpt.AllowMultipleArgumentsPerToken = true;

        var outputOpt = new Option<string?>("--output", "Output directory for file reports");
        var baseNameOpt = new Option<string>("--file-name", "Base filename without extension")
        { DefaultValueFactory = _ => "sqlguard-report" };

        cmd.Options.Add(configOpt);
        cmd.Options.Add(profileOpt);
        cmd.Options.Add(engineOpt);
        cmd.Options.Add(hostOpt);
        cmd.Options.Add(portOpt);
        cmd.Options.Add(databaseOpt);
        cmd.Options.Add(userOpt);
        cmd.Options.Add(passEnvOpt);
        cmd.Options.Add(labelOpt);
        cmd.Options.Add(packsOpt);
        cmd.Options.Add(failOnOpt);
        cmd.Options.Add(minSevOpt);
        cmd.Options.Add(mandOnlyOpt);
        cmd.Options.Add(verboseOpt);
        cmd.Options.Add(reportOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(baseNameOpt);

        cmd.SetAction(async parseResult =>
        {
            bool verbose = parseResult.GetValue(verboseOpt);
            try
            {
                var sp = Helpers.BuildServiceProvider(verbose);
                var configLoader = sp.GetRequiredService<ConfigLoader>();

                var configFile = parseResult.GetValue(configOpt);
                var profile = parseResult.GetValue(profileOpt);
                var engine = parseResult.GetValue(engineOpt);
                var host = parseResult.GetValue(hostOpt);
                var port = parseResult.GetValue(portOpt);
                var database = parseResult.GetValue(databaseOpt);
                var user = parseResult.GetValue(userOpt);
                var passEnv = parseResult.GetValue(passEnvOpt);
                var label = parseResult.GetValue(labelOpt);
                var packs = parseResult.GetValue(packsOpt) ?? new string[] { "sqlserver-core" };
                var failOn = parseResult.GetValue(failOnOpt) ?? "mandatory";
                var minSev = parseResult.GetValue(minSevOpt);
                var mandOnly = parseResult.GetValue(mandOnlyOpt);
                var report = parseResult.GetValue(reportOpt) ?? new string[] { "console" };
                var outputDir = parseResult.GetValue(outputOpt);
                var baseName = parseResult.GetValue(baseNameOpt) ?? "sqlguard-report";

                // ── Resolve DatabaseTarget ────────────────────────────────────
                DatabaseTarget target;
                ScanOptions scanOptions;

                if (configFile is not null)
                {
                    var config = configLoader.LoadFromFile(configFile.FullName);
                    var serverCfg = profile is not null
                        ? config.Profiles.TryGetValue(profile, out var p) ? p
                            : throw new ConfigurationException($"Profile '{profile}' not found.")
                        : config.Server
                            ?? throw new ConfigurationException(
                                "Config has no default 'server'. Use --profile <n>.");

                    var resolved = configLoader.ResolveTarget(serverCfg);
                    var password = passEnv is not null
                        ? Environment.GetEnvironmentVariable(passEnv)
                        : resolved.Password;

                    target = resolved with
                    {
                        Engine = engine is not null
                                    ? Enum.Parse<DatabaseEngine>(engine, ignoreCase: true)
                                    : resolved.Engine,
                        Host = host ?? resolved.Host,
                        Port = port > 0 ? port : resolved.Port,
                        Database = database ?? resolved.Database,
                        Username = user ?? resolved.Username,
                        Password = password,
                        Label = label ?? resolved.Label
                    };

                    var resolvedPacks = packs.Length > 0
                        ? packs
                        : config.Scan.Packs.ToArray();

                    scanOptions = new ScanOptions
                    {
                        Packs = resolvedPacks,
                        MinSeverity = minSev,
                        MandatoryOnly = mandOnly
                    };
                }
                else
                {
                    if (engine is null)
                        throw new ConfigurationException(
                            "--engine is required when not using --config.");
                    if (host is null)
                        throw new ConfigurationException(
                            "--host is required when not using --config.");
                    if (database is null)
                        throw new ConfigurationException(
                            "--database is required when not using --config.");

                    var password = passEnv is not null
                        ? Environment.GetEnvironmentVariable(passEnv) : null;

                    if (passEnv is not null && password is null)
                        AnsiConsole.MarkupLine(
                            $"[yellow]Warning:[/] env var '{passEnv}' is not set.");

                    target = new DatabaseTarget
                    {
                        Engine = Enum.Parse<DatabaseEngine>(engine, ignoreCase: true),
                        Host = host,
                        Port = port,
                        Database = database,
                        Username = user,
                        Password = password,
                        Label = label
                    };
                    scanOptions = new ScanOptions
                    {
                        Packs = packs,
                        MinSeverity = minSev,
                        MandatoryOnly = mandOnly
                    };
                }

                // ── Run scan ──────────────────────────────────────────────────
                var orchestrator = sp.GetRequiredService<IScanOrchestrator>();
                ScanResult? result = null;

                await AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .HideCompleted(false)
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(),
                             new PercentageColumn(), new SpinnerColumn())
                    .StartAsync(async pCtx =>
                    {
                        var task = pCtx.AddTask(
                            $"[cyan]Scanning {target.Engine} @ " +
                            $"{Markup.Escape(target.Host)}/{Markup.Escape(target.Database)}[/]",
                            maxValue: 100);

                        result = await orchestrator.ScanAsync(target, scanOptions,
                            new Progress<ScanProgressUpdate>(u =>
                            {
                                task.Value = (double)u.Current / u.Total * 100;
                                task.Description =
                                    $"[grey]{u.RuleId}[/] {Markup.Escape(u.RuleTitle)}";
                            }));
                        task.Value = 100;
                    });

                // ── Reports ───────────────────────────────────────────────────
                var formats = Helpers.ParseFormats(report);
                var outDir = outputDir ?? "./reports";
                var fileName = baseName;

                if (!string.IsNullOrWhiteSpace(result!.Label))
                    fileName = $"{Helpers.Sanitise(result.Label)}-{fileName}";

                await sp.GetRequiredService<IReportPipeline>()
                        .RunAsync(result, formats, outDir, fileName);

                if (formats.Any(f => f != ReportFormat.Console))
                    AnsiConsole.MarkupLine(
                        $"[grey]File reports →[/] [cyan]{Path.GetFullPath(outDir)}[/]");

                return Helpers.DetermineExitCode(result, failOn);
            }
            catch (ConfigurationException ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Configuration error:[/] {Markup.Escape(ex.Message)}");
                return 2;
            }
            catch (Exception ex) when (!verbose)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Fatal error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine(
                    "[grey]Run with --verbose for a full stack trace.[/]");
                return 2;
            }
        });

        return cmd;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// inventory
// ─────────────────────────────────────────────────────────────────────────────

static class InventoryCommand
{
    public static Command Build()
    {
        var cmd = new Command("inventory",
            "Scan multiple servers defined in a JSON inventory file");

        var fileOpt = new Option<FileInfo>("--file", "Path to inventory JSON file");
        fileOpt.Required = true;

        var packsOpt = new Option<string[]>("--packs", "Default packs (per-server settings in inventory override this)")
        { DefaultValueFactory = _ => new string[] { "sqlserver-core" } };
        packsOpt.AllowMultipleArgumentsPerToken = true;

        var failOnOpt = new Option<string>("--fail-on", "Exit 1 threshold: mandatory | critical | high | medium")
        { DefaultValueFactory = _ => "mandatory" };

        var reportOpt = new Option<string[]>("--report", "Report formats")
        { DefaultValueFactory = _ => new string[] { "console", "html", "json" } };
        reportOpt.AllowMultipleArgumentsPerToken = true;

        var outputOpt = new Option<string?>("--output", "Output directory");
        var verboseOpt = new Option<bool>("--verbose", "Enable diagnostic logging");

        cmd.Options.Add(fileOpt);
        cmd.Options.Add(packsOpt);
        cmd.Options.Add(failOnOpt);
        cmd.Options.Add(reportOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(verboseOpt);

        cmd.SetAction(async parseResult =>
        {
            bool verbose = parseResult.GetValue(verboseOpt);
            try
            {
                var file = parseResult.GetValue(fileOpt)!;
                var packs = parseResult.GetValue(packsOpt) ?? new string[] { "sqlserver-core" };
                var failOn = parseResult.GetValue(failOnOpt) ?? "mandatory";
                var report = parseResult.GetValue(reportOpt) ?? new string[] { "console" };
                var outputDir = parseResult.GetValue(outputOpt);

                var sp = Helpers.BuildServiceProvider(verbose);
                var invLoader = sp.GetRequiredService<InventoryLoader>();
                var orchestrator = sp.GetRequiredService<IMultiScanOrchestrator>();
                var pipeline = sp.GetRequiredService<IReportPipeline>();

                var formats = Helpers.ParseFormats(report);
                var outDir = outputDir ?? "./reports";
                var targets = await invLoader.LoadAsync(file.FullName);
                var defaultOpts = new ScanOptions { Packs = packs };

                AnsiConsole.MarkupLine(
                    $"\n[cyan]Inventory scan:[/] {targets.Count} server(s) " +
                    $"from [dim]{file.Name}[/]\n");

                var progress = new Progress<MultiScanProgressUpdate>(u =>
                {
                    if (u.RuleUpdate is null)
                        AnsiConsole.MarkupLine(
                            $"  [cyan][{u.ServerCurrent}/{u.ServerTotal}][/] " +
                            $"Connecting to [bold]{Markup.Escape(u.ServerLabel)}[/]...");
                });

                var multiResult = await orchestrator.ScanAllAsync(
                    targets, defaultOpts, progress);

                foreach (var scanResult in multiResult.Results)
                {
                    var baseName =
                        $"{Helpers.Sanitise(scanResult.Label ?? scanResult.Host)}" +
                        "-sqlguard-report";
                    await pipeline.RunAsync(scanResult, formats, outDir, baseName);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.Write(
                    new Rule("[bold cyan]Inventory Scan Results[/]").RuleStyle("cyan"));

                var table = new Table().Border(TableBorder.Rounded)
                    .AddColumn("Server")
                    .AddColumn("Engine")
                    .AddColumn("Score %")
                    .AddColumn("Pass")
                    .AddColumn("Fail")
                    .AddColumn("Warn")
                    .AddColumn("Verdict");

                foreach (var r in multiResult.Results)
                {
                    var sc = r.ComplianceScore;
                    var color = sc >= 90 ? "green" : sc >= 70 ? "yellow" : "red";
                    table.AddRow(
                        Markup.Escape(r.Label ?? r.Host),
                        r.Engine,
                        $"[{color} bold]{sc:F0}%[/]",
                        $"[green]{r.PassCount}[/]",
                        $"[red]{r.FailCount}[/]",
                        $"[yellow]{r.WarnCount}[/]",
                        r.MandatoryFailCount == 0
                            ? "[green]✓ PASSED[/]"
                            : "[red]✗ FAILED[/]"
                    );
                }
                AnsiConsole.Write(table);

                AnsiConsole.MarkupLine(
                    $"\n  [grey]Servers passed:[/] " +
                    $"[green]{multiResult.Results.Count(r => r.MandatoryFailCount == 0)}[/]  " +
                    $"[grey]failed:[/] [red]{multiResult.ServersFailed}[/]");
                AnsiConsole.MarkupLine(
                    $"[grey]File reports →[/] [cyan]{Path.GetFullPath(outDir)}[/]");

                return multiResult.Results.Any(r =>
                    Helpers.DetermineExitCode(r, failOn) == 1) ? 1 : 0;
            }
            catch (Exception ex) when (!verbose)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Fatal error:[/] {Markup.Escape(ex.Message)}");
                return 2;
            }
        });

        return cmd;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// list-rules
// ─────────────────────────────────────────────────────────────────────────────

static class ListRulesCommand
{
    public static Command Build()
    {
        var cmd = new Command("list-rules", "List all rules in a pack");

        var packOpt = new Option<string>("--pack", "Pack name or directory path")
        { DefaultValueFactory = _ => "sqlserver-core" };
        cmd.Options.Add(packOpt);

        cmd.SetAction(async parseResult =>
        {
            var pack = parseResult.GetValue(packOpt) ?? "sqlserver-core";
            var loader = Helpers.BuildServiceProvider(false)
                                .GetRequiredService<IRulePackLoader>();
            var rules = await loader.LoadAsync(pack);

            AnsiConsole.MarkupLine($"[cyan]Pack:[/] {pack}  [grey]({rules.Count} rules)[/]\n");

            var table = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[grey]Rule ID[/]").Width(22))
                .AddColumn(new TableColumn("[grey]Severity[/]").Width(10))
                .AddColumn(new TableColumn("[grey]Mandatory[/]").Width(10))
                .AddColumn(new TableColumn("[grey]Category[/]").Width(20))
                .AddColumn(new TableColumn("[grey]Title[/]"));

            foreach (var r in rules.OrderBy(r => r.Id))
            {
                var c = Helpers.SevColor(r.Severity);
                table.AddRow(
                    $"[dim]{r.Id}[/]",
                    $"[{c}]{r.Severity}[/]",
                    r.Mandatory ? "[red]● Yes[/]" : "[grey]○ No[/]",
                    r.Category,
                    Markup.Escape(r.Title)
                );
            }
            AnsiConsole.Write(table);
            return 0;
        });

        return cmd;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// validate-rule
// ─────────────────────────────────────────────────────────────────────────────

static class ValidateRuleCommand
{
    public static Command Build()
    {
        var cmd = new Command("validate-rule",
            "Validate a YAML rule file for schema correctness");

        var pathArg = new Argument<FileInfo>("path");
        pathArg.Description = "Path to .yaml rule file";
        cmd.Arguments.Add(pathArg);

        cmd.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(pathArg)!;
            var loader = Helpers.BuildServiceProvider(false)
                                .GetRequiredService<IRulePackLoader>();
            try
            {
                var rules = await loader.LoadAsync(file.FullName);
                AnsiConsole.MarkupLine(
                    $"[green]✓ Valid[/] — {rules.Count} rule(s) in [dim]{file.Name}[/]");
                foreach (var r in rules)
                    AnsiConsole.MarkupLine(
                        $"  [{Helpers.SevColor(r.Severity)}]●[/] " +
                        $"[dim]{r.Id}[/] {Markup.Escape(r.Title)}");
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]✗ Invalid:[/] {Markup.Escape(ex.Message)}");
                return 2;
            }
        });

        return cmd;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// init-config
// ─────────────────────────────────────────────────────────────────────────────

static class InitConfigCommand
{
    public static Command Build()
    {
        var cmd = new Command("init-config",
            "Scaffold a starter sqlguard.yml in the current directory");

        var outOpt = new Option<FileInfo>("--output", "Output path")
        { DefaultValueFactory = _ => new FileInfo("sqlguard.yml") };
        cmd.Options.Add(outOpt);

        cmd.SetAction(async parseResult =>
        {
            var output = parseResult.GetValue(outOpt)!;

            if (output.Exists)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]File already exists:[/] {output.FullName}");
                return 2;
            }

            const string template = """
# SqlGuard Configuration — safe to commit to version control.
# NEVER store literal passwords here.
# Use password_env: VARNAME  (env var NAME, not its value)
# Docs: https://github.com/your-org/sqlguard/docs/configuration.md
schema_version: "1"

server:
  label: "Production DB"
  engine: SqlServer           # SqlServer | PostgreSQL | MySQL | Oracle
  host: localhost
  port: 0                     # 0 = use engine default
  database: master
  timeout_seconds: 30
  auth:
    type: password            # password | integrated | managed_identity | connection_string
    username: sqlguard_scanner
    password_env: SQLGUARD_DB_PASS

profiles:
  staging:
    label: "Staging"
    engine: SqlServer
    host: staging-db.internal
    database: AppDb
    auth:
      type: password
      username: sqlguard_scanner
      password_env: STAGING_DB_PASS

scan:
  packs:
    - sqlserver-core
    - sqlserver-cis
  fail_on: mandatory
  min_severity: Low
  mandatory_only: false
  exclude_rules: []
  max_concurrency: 4

report:
  formats:
    - console
    - json
    - html
  output_directory: ./reports
  file_base_name: sqlguard-report
  per_server_files: true
""";
            await File.WriteAllTextAsync(output.FullName, template);
            AnsiConsole.MarkupLine($"[green]✓ Created:[/] {output.FullName}");
            AnsiConsole.MarkupLine("[grey]Next steps:[/]");
            AnsiConsole.MarkupLine(
                "[grey]  1. Edit[/] sqlguard.yml [grey]with your server details[/]");
            AnsiConsole.MarkupLine(
                "[grey]  2. Set  [/] [cyan]export SQLGUARD_DB_PASS=<password>[/]");
            AnsiConsole.MarkupLine(
                "[grey]  3. Run  [/] [cyan]sqlguard scan --config sqlguard.yml[/]");
            return 0;
        });

        return cmd;
    }
}
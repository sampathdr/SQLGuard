<!-- README.md — auto-kept in sync with every code change. Last updated: v0.1.0 -->
<div align="center">

# 🛡️ SqlGuard

**Open-source database security scanner — declarative YAML rules, multi-format reports, CI/CD-native**

[![CI](https://github.com/your-org/sqlguard/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/sqlguard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SqlGuard.Core.svg?label=SqlGuard.Core)](https://www.nuget.org/packages/SqlGuard.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![codecov](https://codecov.io/gh/your-org/sqlguard/branch/main/graph/badge.svg)](https://codecov.io/gh/your-org/sqlguard)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/your-org/sqlguard/badge)](https://securityscorecards.dev/viewer/?uri=github.com/your-org/sqlguard)

Scan **SQL Server** and **PostgreSQL** databases against CIS Benchmarks, NIST 800-53, DISA STIGs, and your own policies — all defined in plain **YAML**. No C# required to contribute rules.

[📖 Docs](docs/) · [🐛 Bug Report](.github/ISSUE_TEMPLATE/bug_report.md) · [📋 Rule Request](.github/ISSUE_TEMPLATE/rule_request.md) · [💬 Discussions](https://github.com/your-org/sqlguard/discussions)

</div>

---

## Contents

- [Why SqlGuard](#why-sqlguard)
- [Quick Start](#quick-start)
- [CLI Reference](#cli-reference)
- [Configuration File](#configuration-file-sqlguardyml)
- [Secure Credential Handling](#secure-credential-handling)
- [Multi-Server Inventory Scanning](#multi-server-inventory-scanning)
- [Report Formats](#report-formats)
- [Rule Packs](#rule-packs)
- [YAML Rule Format](#yaml-rule-format)
- [Architecture](#architecture)
- [Project Layout](#project-layout)
- [NuGet Packages](#nuget-packages)
- [Adding a Rule](#adding-a-rule-5-minutes-no-c-required)
- [Adding a Database Provider](#adding-a-database-provider)
- [Running Tests](#running-tests)
- [CI/CD Integration](#cicd-integration)
- [Roadmap](#roadmap)
- [Contributing](#contributing)

---

## Why SqlGuard

| Capability | SqlGuard | Typical alternatives |
|---|---|---|
| Rules defined in YAML — no coding to contribute | ✅ | ❌ Most require C# or Python |
| `mandatory` vs `advisory` controls — separate CI gates from warnings | ✅ | ❌ |
| Version-aware expected values in a single rule | ✅ | ❌ |
| Six report formats from one scan (console, JSON, HTML, SARIF, CSV, Markdown) | ✅ | Partial |
| Multi-server inventory scanning | ✅ | Partial |
| SARIF → GitHub Advanced Security tab | ✅ | ❌ |
| Config file safe to commit — zero secrets in YAML | ✅ | ❌ |
| Self-contained binary, no runtime required | ✅ | ❌ |
| Open source, no SaaS required | ✅ | ❌ Many are freemium |

---

## Quick Start

### Install

```bash
# Global .NET tool
dotnet tool install -g SqlGuard.Cli

# Or download a self-contained binary from GitHub Releases (no .NET needed)
# https://github.com/your-org/sqlguard/releases/latest
```

### Scaffold a config file

```bash
sqlguard init-config
# Creates sqlguard.yml in the current directory
```

### Scan with a config file

```bash
export SQLGUARD_DB_PASS=my_password
sqlguard scan --config sqlguard.yml
```

### Scan with flags only (no config file)

```bash
sqlguard scan \
  --engine SqlServer \
  --host myserver.database.windows.net \
  --database AppDb \
  --user sqlguard_scanner \
  --password-env SQLGUARD_DB_PASS \
  --packs sqlserver-core,sqlserver-cis \
  --report console,html,json \
  --output ./reports
```

### Scan multiple servers from an inventory file

```bash
sqlguard inventory --file servers.json --report html,json,sarif --output ./reports
```

---

## CLI Reference

### `sqlguard scan` — single database

```
Options:
  Connection:
    --config <file>         Path to sqlguard.yml config file
    --profile <name>        Named server profile from config file
    --engine <engine>       Database engine: SqlServer | PostgreSQL
    --host <host>           Server hostname or IP
    --port <port>           Port number (0 = engine default)
    --database <db>         Database or catalog name
    --user <username>       Login username
    --password-env <VAR>    Name of the env var holding the password  ← never --password
    --label <label>         Friendly name shown in reports

  Scan behaviour:
    --packs <packs>         Rule packs to load (space/comma-separated)  [default: sqlserver-core]
    --fail-on <threshold>   Exit 1 when: mandatory|critical|high|medium [default: mandatory]
    --min-severity <level>  Minimum severity: Info|Low|Medium|High|Critical [default: Low]
    --mandatory-only        Only run mandatory rules
    --verbose               Show diagnostic log output

  Output:
    --report <formats>      console,json,html,sarif,csv,markdown        [default: console]
    --output <directory>    Directory for file reports                   [default: ./reports]
    --file-name <name>      Base filename without extension              [default: sqlguard-report]
```

### `sqlguard inventory` — multiple servers

```
Options:
  --file <path>         Path to inventory JSON file                     [required]
  --packs <packs>       Default packs (per-server settings override)
  --fail-on <threshold> Exit 1 threshold
  --report <formats>    Output formats                                  [default: console,html,json]
  --output <directory>  Output directory
  --verbose             Enable diagnostic logging
```

### `sqlguard list-rules` — inspect a pack

```bash
sqlguard list-rules --pack sqlserver-core
sqlguard list-rules --pack ./rules/my-company
```

### `sqlguard validate-rule` — validate YAML before committing

```bash
sqlguard validate-rule rules/sqlserver-core/server-security.yaml
# Exit code 0 = valid, exit code 2 = schema error
```

### `sqlguard init-config` — scaffold starter config

```bash
sqlguard init-config                    # creates sqlguard.yml
sqlguard init-config --output prod.yml  # custom path
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | All checks passed per `--fail-on` threshold |
| `1` | One or more mandatory/threshold rule failures |
| `2` | Execution error — bad config, cannot connect, unexpected exception |

---

## Configuration File (`sqlguard.yml`)

The config file is **safe to commit** — it never contains secrets. All credentials come from environment variables.

```yaml
# sqlguard.yml  — commit this file safely
schema_version: "1"

server:
  label: "Production SQL Server"
  engine: SqlServer         # SqlServer | PostgreSQL | MySQL | Oracle
  host: prod-db.internal
  port: 0                   # 0 = engine default (1433 / 5432 / etc.)
  database: AppDatabase
  timeout_seconds: 30
  auth:
    type: password          # password | integrated | managed_identity | connection_string
    username: sqlguard_scanner
    password_env: SQLGUARD_DB_PASS  # env var NAME — never the value

profiles:
  staging:
    label: "Staging"
    engine: SqlServer
    host: staging-db.internal
    database: AppDatabase
    auth:
      type: password
      username: sqlguard_scanner
      password_env: STAGING_DB_PASS

  azure-sql:
    label: "Azure SQL (Managed Identity)"
    engine: SqlServer
    host: myapp.database.windows.net
    database: AppDb
    auth:
      type: managed_identity   # No password — uses Azure MI

  postgres-prod:
    label: "Production PostgreSQL"
    engine: PostgreSQL
    host: pg-prod.internal
    port: 5432
    database: app_db
    auth:
      type: password
      username: sqlguard_scanner
      password_env: PG_PROD_DB_PASS

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
  formats: [console, json, html]
  output_directory: ./reports
  file_base_name: sqlguard-report
  per_server_files: true
```

**Use with CLI:**
```bash
# Default server
sqlguard scan --config sqlguard.yml

# Named profile — override any option with a flag
sqlguard scan --config sqlguard.yml --profile staging --report html,sarif
```

---

## Secure Credential Handling

SqlGuard enforces a strict security contract for credentials:

### ✅ Correct patterns

```yaml
# Pattern 1: password_env — most common
auth:
  type: password
  username: sqlguard_scanner
  password_env: SQLGUARD_DB_PASS      # name of env var, not the value
```

```yaml
# Pattern 2: full connection string via env var
auth:
  type: connection_string
  connection_string_env: SQLGUARD_CONNSTR  # env var holds the full string
```

```yaml
# Pattern 3: Windows / Kerberos (SQL Server — no password needed)
auth:
  type: integrated
```

```yaml
# Pattern 4: Azure Managed Identity
auth:
  type: managed_identity
```

### ❌ Never do this

```yaml
auth:
  password_env: myP@ssw0rd   # ← WRONG: this is the password, not a var name
```

### CLI pattern

```bash
# Always pass the env var NAME, not the value
sqlguard scan --password-env DB_PASS    # ✅
sqlguard scan --password "mypassword"   # ❌ avoid — visible in process list
```

### CI/CD pattern

```yaml
# GitHub Actions
- name: Run SqlGuard
  run: sqlguard scan --config sqlguard.yml --fail-on mandatory
  env:
    SQLGUARD_DB_PASS: ${{ secrets.PROD_DB_PASSWORD }}   # ✅ secret injected as env var
```

---

## Multi-Server Inventory Scanning

Create an inventory JSON file (safe to commit — no secrets):

```json
{
  "default_packs": ["sqlserver-core", "sqlserver-cis"],
  "servers": [
    {
      "label": "prod-db-1",
      "engine": "SqlServer",
      "host": "10.0.1.10",
      "database": "AppDb",
      "auth": { "type": "password", "username": "sqlguard_scanner", "password_env": "PROD_DB_PASS" }
    },
    {
      "label": "prod-db-2",
      "engine": "SqlServer",
      "host": "10.0.1.11",
      "database": "AppDb",
      "auth": { "type": "password", "username": "sqlguard_scanner", "password_env": "PROD_DB_PASS" }
    },
    {
      "label": "postgres-analytics",
      "engine": "PostgreSQL",
      "host": "10.0.2.20",
      "database": "analytics",
      "packs": ["postgresql-core"],
      "auth": { "type": "password", "username": "sqlguard_scanner", "password_env": "PG_PASS" }
    }
  ]
}
```

```bash
export PROD_DB_PASS=secret
export PG_PASS=secret2

sqlguard inventory \
  --file servers.json \
  --report html,json,sarif \
  --output ./reports

# Output:
# reports/
#   prod-db-1-sqlguard-report.html
#   prod-db-1-sqlguard-report.json
#   prod-db-2-sqlguard-report.html
#   ...
```

**Resilient:** a connection failure on one server is recorded as an `Error` result — the remaining servers continue scanning.

---

## Report Formats

A single scan can produce multiple formats simultaneously:

```bash
sqlguard scan ... --report console,json,html,sarif,csv,markdown --output ./reports
```

| Format | Flag | File | Best for |
|--------|------|------|---------|
| **Console** | `console` | stdout only | Developers, quick feedback |
| **JSON** | `json` | `.json` | Automation, dashboards, APIs |
| **HTML** | `html` | `.html` | DBAs, security teams, audits |
| **SARIF** | `sarif` | `.sarif` | GitHub Advanced Security, Azure DevOps |
| **CSV** | `csv` | `.csv` | Excel analysis, Power BI, pivot tables |
| **Markdown** | `markdown` | `.md` | GitHub PR comments, wikis, documentation |

### Sample output directory

```
reports/
├── sqlguard-report.json      ← CI/CD automation
├── sqlguard-report.html      ← DBA review / audit evidence
├── sqlguard-report.sarif     ← GitHub Advanced Security
├── sqlguard-report.csv       ← Excel / Power BI
└── sqlguard-report.md        ← GitHub wiki / PR comment
```

### SARIF → GitHub Advanced Security

```yaml
# .github/workflows/db-security.yml
- name: Run SqlGuard
  run: sqlguard scan --config sqlguard.yml --report sarif --output ./reports
  env:
    SQLGUARD_DB_PASS: ${{ secrets.DB_PASS }}

- name: Upload SARIF to GitHub Security tab
  uses: github/codeql-action/upload-sarif@v3
  if: always()
  with:
    sarif_file: ./reports/sqlguard-report.sarif
    category: sqlguard-db-scan
```

Results appear in **Security → Code scanning** on your repository.

---

## Rule Packs

Rule packs are directories of YAML files. Built-in packs ship with SqlGuard:

| Pack | Engine | Description |
|------|--------|-------------|
| `sqlserver-core` | SQL Server | Mandatory security baseline (22 rules) |
| `sqlserver-cis` | SQL Server | CIS SQL Server Benchmark v1.4 (35 rules) |
| `postgresql-core` | PostgreSQL | Mandatory security baseline (18 rules) |
| `postgresql-cis` | PostgreSQL | CIS PostgreSQL Benchmark v1.0 (28 rules) |
| `community` | Both | Community-contributed rules |

### Loading packs

```bash
# Core only (fast, CI gate)
--packs sqlserver-core

# Core + CIS (full compliance)
--packs sqlserver-core,sqlserver-cis

# Custom enterprise rules on disk
--packs sqlserver-core,./rules/my-company
```

### Pack loading order

1. Built-in embedded packs (ship in `SqlGuard.RulePacks.CIS` NuGet)
2. File system directory paths
3. Explicit `.yaml` file paths

---

## YAML Rule Format

Every security check is a YAML document. **No C# required.**

### Basic rule

```yaml
id: MSSQL-CORE-003
title: xp_cmdshell is disabled
category: SurfaceReduction
severity: critical
mandatory: true           # false = WARN instead of FAIL — never breaks CI/CD
database: SqlServer
description: >
  xp_cmdshell allows execution of OS commands from T-SQL.
  This must be disabled in all production instances.
query: |
  SELECT value_in_use
  FROM sys.configurations
  WHERE name = 'xp_cmdshell'
operator: equals          # equals|not_equals|greater_than|less_than|contains|in|regex|custom
expected: 0
remediation: >
  EXEC sp_configure 'xp_cmdshell', 0;
  RECONFIGURE;
compliance_references:
  - "CIS SQL Server 2.1"
  - "NIST 800-53 CM-7"
tags: [rce, surface-reduction, critical]
```

### Version-aware rule

```yaml
id: MSSQL-CIS-010
title: Compatibility level matches SQL Server version
operator: equals
version_valid_values:
  "16":  "160"    # SQL Server 2022
  "15":  "150"    # SQL Server 2019
  "14":  "140"    # SQL Server 2017
  "13":  "130"    # SQL Server 2016
```

### Advisory (non-mandatory) rule

```yaml
mandatory: false    # Raises WARN — never breaks CI/CD regardless of --fail-on
notes: >
  Set mandatory: true in a site-policy override if CLR is prohibited in your environment.
```

### Full schema reference

| Field | Required | Description |
|-------|----------|-------------|
| `id` | ✅ | Unique identifier, e.g. `MSSQL-CORE-001` |
| `title` | ✅ | Human-readable title |
| `description` | ✅ | Why this matters |
| `category` | ✅ | `PatchManagement`, `Authentication`, `Encryption`, etc. |
| `severity` | ✅ | `info` / `low` / `medium` / `high` / `critical` |
| `mandatory` | ✅ | `true` = FAIL on violation; `false` = WARN only |
| `database` | ✅ | `SqlServer` / `PostgreSQL` / `MySQL` / `Oracle` |
| `query` | ✅ | T-SQL or PL/pgSQL to collect the value |
| `operator` | ✅ | See table above |
| `expected` | ⚠️ | Fixed expected value |
| `valid_values` | ⚠️ | List of acceptable values (any match = pass) |
| `version_valid_values` | ⚠️ | Version-prefix keyed dict of expected values |
| `dynamic_expected_function` | ⚠️ | Resolver function name for runtime calculation |
| `remediation` | — | Fix guidance shown in all reports |
| `compliance_references` | — | CIS / NIST / STIG / PCI DSS citations |
| `tags` | — | Free-form labels for filtering |
| `min_version` / `max_version` | — | Skip rule outside this version range |
| `enabled` | — | `false` to disable without deleting |
| `notes` | — | Contributor notes (not shown in reports) |

*⚠️ At least one of `expected`, `valid_values`, `version_valid_values`, or `dynamic_expected_function` is required.*

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                          SqlGuard CLI                               │
│  scan · inventory · list-rules · validate-rule · init-config        │
└────────────────────────────┬────────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                        SqlGuard Core                                │
│                                                                     │
│  ┌──────────────────┐  ┌─────────────────┐  ┌──────────────────┐   │
│  │ ConfigLoader      │  │ ScanOrchestrator │  │ MultiScanOrch.   │   │
│  │ InventoryLoader   │  │                 │  │ (inventory scans)│   │
│  │                   │  │ 1. Load packs   │  │                  │   │
│  │ • YAML config     │  │ 2. Connect      │  │ • Runs single    │   │
│  │ • Env var interp. │  │ 3. Filter rules │  │   orchestrator   │   │
│  │ • Profile select  │  │ 4. Evaluate     │  │   per server     │   │
│  │ • Credential safe │  │ 5. Aggregate    │  │ • Fault-tolerant │   │
│  └──────────────────┘  └────────┬────────┘  └──────────────────┘   │
│                                  │                                   │
│  ┌───────────────────────────────▼─────────────────────────────┐    │
│  │ EvaluationEngine                                             │    │
│  │  1. Version gate   2. Collect   3. Resolve expected          │    │
│  │  4. Apply operator 5. Map → RuleStatus (respects mandatory)  │    │
│  └───────────────────────────────┬─────────────────────────────┘    │
│                                  │                                   │
│  ┌───────────────────────────────▼─────────────────────────────┐    │
│  │ YamlRulePackLoader                                           │    │
│  │  • File system directories  • Explicit .yaml files           │    │
│  │  • Embedded NuGet resources (sqlserver-core, pg-cis, etc.)   │    │
│  └──────────────────────────────────────────────────────────────┘   │
└──────────┬────────────────────────────────┬────────────────────────-┘
           │                                │
     ┌─────▼──────┐                  ┌──────▼──────────────────────┐
     │  Providers │                  │  Report Pipeline             │
     │            │                  │                              │
     │ SqlServer  │                  │  ReportPipeline coordinator  │
     │ PostgreSQL │                  │  ┌──────────────────────┐   │
     │ (MySQL →)  │                  │  │ ConsoleReportWriter   │   │
     │ (Oracle →) │                  │  │ JsonReportWriter      │   │
     └────────────┘                  │  │ HtmlReportWriter      │   │
                                     │  │ SarifReportWriter     │   │
                                     │  │ CsvReportWriter       │   │
                                     │  │ MarkdownReportWriter  │   │
                                     │  └──────────────────────┘   │
                                     └──────────────────────────────┘
```

### Key design principles

| Principle | Implementation |
|-----------|----------------|
| **Declarative rules** | YAML files — contributors never need to write C# |
| **Mandatory vs Advisory** | `mandatory: true/false` controls CI gate vs warning |
| **Version-aware** | `version_valid_values` per DB major version |
| **Provider isolation** | Each DB engine is a separate NuGet package; core has zero DB deps |
| **Testable** | `IMetadataCollector` abstracts the DB connection; every rule testable with a mock |
| **Fail-safe** | Evaluation exceptions → `Error` result; scan always completes |
| **Secret-free config** | Config files contain only env var *names*, never values |
| **Report pipeline** | Console renders first; all file writers run in parallel; no writer failure blocks others |

---

## Project Layout

```
SqlGuard/
│
├── src/
│   ├── SqlGuard.Core/
│   │   ├── Abstractions/
│   │   │   └── Abstractions.cs          # All interfaces: IMetadataCollector, IDatabaseProvider,
│   │   │                                #   IRulePackLoader, IEvaluationEngine, IScanOrchestrator,
│   │   │                                #   IMultiScanOrchestrator, IReportWriter, IReportPipeline,
│   │   │                                #   IDynamicExpectedResolver, progress types
│   │   ├── Configuration/
│   │   │   ├── SqlGuardConfig.cs        # Config models: SqlGuardConfig, ServerConfig, AuthConfig,
│   │   │   │                            #   ScanConfig, ReportConfig
│   │   │   ├── ConfigLoader.cs          # YAML loader + env var interpolation + validation
│   │   │   └── InventoryLoader.cs       # JSON inventory file loader
│   │   ├── Engine/
│   │   │   ├── ScanOrchestrator.cs      # Single-server scan pipeline
│   │   │   ├── MultiScanOrchestrator.cs # Multi-server inventory scan pipeline
│   │   │   └── YamlRulePackLoader.cs    # Loads YAML rule packs (filesystem + embedded)
│   │   ├── Evaluation/
│   │   │   └── EvaluationEngine.cs      # Version gate, collect, compare, map to status
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs  # AddSqlGuardCore(), AddSqlGuardProvider<T>(),
│   │   │                                        #   AddSqlGuardReportWriter<T>()
│   │   └── Models/
│   │       └── Models.cs                # All domain types: enums, DatabaseTarget, RuleDefinition,
│   │                                    #   EvaluationResult, ScanResult, ScanOptions,
│   │                                    #   ReportOutputOptions, InventoryTarget,
│   │                                    #   MultiScanResult, RulePackManifest, AuthType
│   │
│   ├── SqlGuard.Providers.SqlServer/
│   │   └── SqlServerProvider.cs         # IDatabaseProvider + SqlServerMetadataCollector
│   │
│   ├── SqlGuard.Providers.PostgreSQL/
│   │   └── PostgreSQLProvider.cs        # IDatabaseProvider + PostgreSQLMetadataCollector
│   │
│   ├── SqlGuard.Reports/
│   │   ├── ReportPipeline.cs            # Coordinates all writers; console first, files parallel
│   │   ├── Console/
│   │   │   └── ConsoleReportWriter.cs   # Spectre.Console: FigletText, BreakdownChart, Tree, Table
│   │   ├── Json/
│   │   │   └── JsonReportWriter.cs      # Stable DTO schema; snake_case; null-omitting
│   │   ├── Html/
│   │   │   └── HtmlReportWriter.cs      # Self-contained HTML; Chart.js doughnut; XSS-safe
│   │   ├── Sarif/
│   │   │   └── SarifReportWriter.cs     # SARIF 2.1.0; GitHub Advanced Security compatible
│   │   ├── Csv/
│   │   │   └── CsvReportWriter.cs       # RFC 4180; UTF-8 BOM; summary block; Excel-ready
│   │   └── Markdown/
│   │       └── MarkdownReportWriter.cs  # CommonMark; collapsible sections; PR-comment ready
│   │
│   └── SqlGuard.Cli/
│       └── Program.cs                   # 5 commands: scan, inventory, list-rules,
│                                        #   validate-rule, init-config
│
├── rules/                               # YAML rule packs — source of truth
│   ├── sqlserver-core/
│   │   ├── manifest.yaml
│   │   ├── server-security.yaml         # MSSQL-CORE-001..007 (patch, SA, xp_cmdshell,
│   │   │                                #   OLE, ad-hoc, auditing, CLR)
│   │   ├── db-config.yaml               # MSSQL-CORE-010..011 (TDE, force encryption)
│   │   └── auth.yaml                    # MSSQL-CORE-020..022 (auth mode, password policy,
│   │                                    #   orphaned accounts)
│   └── postgresql-core/
│       └── server-security.yaml         # PG-CORE-001..008 (version, SSL, trust auth,
│                                        #   scram-sha-256, logging, superusers, pgaudit,
│                                        #   listen_addresses)
│
├── tests/
│   ├── SqlGuard.Core.Tests/             # Unit tests — no DB required
│   ├── SqlGuard.Providers.SqlServer.Tests/
│   ├── SqlGuard.Providers.PostgreSQL.Tests/
│   ├── SqlGuard.Reports.Tests/
│   │   └── ReportWriterTests.cs         # Tests for all 6 writers + ReportPipeline;
│   │                                    #   includes XSS-escape test for HTML writer
│   └── SqlGuard.Integration.Tests/
│       └── IntegrationTests.cs          # Testcontainers — real SQL Server + PostgreSQL via Docker
│
├── samples/
│   ├── config/
│   │   └── sqlguard.yml                 # Annotated example config with all auth patterns
│   ├── inventory/
│   │   └── servers.json                 # Example inventory for 4 servers
│   └── ci-cd/
│       └── README.md                    # GitHub Actions + Azure DevOps examples with
│                                        #   minimal-permission scanner account SQL scripts
│
├── docs/                                # Architecture, contributing, rule schema guides
│
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                       # 5 jobs: build+test (3 OS), validate-rules,
│   │   │                                #   integration tests, CodeQL+DevSkim, self-scan+SARIF
│   │   └── release.yml                  # NuGet publish + 6-platform CLI binaries + checksums
│   └── ISSUE_TEMPLATE/
│       ├── bug_report.md
│       └── rule_request.md              # Most common contribution type
│
├── Directory.Build.props                # Shared: .NET 9, nullable, MinVer, SourceLink
├── global.json                          # Pins SDK version
├── README.md                            # This file
└── SECURITY.md                          # Vulnerability disclosure policy
```

---

## NuGet Packages

| Package | Description | Key dependencies |
|---------|-------------|-----------------|
| `SqlGuard.Core` | Interfaces, models, engine, evaluation — use to build plugins | `YamlDotNet`, `Microsoft.Extensions.*` |
| `SqlGuard.Providers.SqlServer` | SQL Server provider + metadata collector | `Microsoft.Data.SqlClient` |
| `SqlGuard.Providers.PostgreSQL` | PostgreSQL provider + metadata collector | `Npgsql` |
| `SqlGuard.RulePacks.CIS` | CIS Benchmark rule packs (embedded YAML) | `SqlGuard.Core` |
| `SqlGuard.Reports` | All 6 report writers + pipeline | `Spectre.Console` |
| `SqlGuard.Cli` | Global dotnet tool | `System.CommandLine` |

---

## Adding a Rule (5 minutes, no C# required)

1. **Pick the right pack file** — `rules/sqlserver-core/server-security.yaml` for mandatory SQL Server rules, `rules/community/` for everything else.

2. **Add your rule to the YAML file:**

```yaml
- id: MSSQL-CORE-099
  title: Remote admin connections are disabled
  category: Network
  severity: medium
  mandatory: false            # advisory — never breaks CI/CD
  database: SqlServer
  description: >
    The Dedicated Administrator Connection should not be remotely accessible.
  query: |
    SELECT value_in_use
    FROM sys.configurations
    WHERE name = 'remote admin connections'
  operator: equals
  expected: 0
  remediation: >
    EXEC sp_configure 'remote admin connections', 0;
    RECONFIGURE;
  compliance_references:
    - "CIS SQL Server 2.9"
  tags: [network, dac]
```

3. **Validate it:**
```bash
sqlguard validate-rule rules/sqlserver-core/server-security.yaml
# ✓ Valid — 8 rule(s) loaded from server-security.yaml
```

4. **Submit a PR.** See [docs/contributing/adding-rules.md](docs/contributing/adding-rules.md).

---

## Adding a Database Provider

Implement two interfaces in a new project (`SqlGuard.Providers.MySQL`, etc.):

```csharp
// 1. Provider — handles connection lifecycle
public sealed class MySQLProvider : IDatabaseProvider
{
    public DatabaseEngine Engine => DatabaseEngine.MySQL;
    public string SupportedVersionRange => "MySQL 8.0+";

    public async Task<bool> TestConnectionAsync(DatabaseTarget target, CancellationToken ct = default) { ... }
    public async Task<IMetadataCollector> CreateCollectorAsync(DatabaseTarget target, CancellationToken ct = default) { ... }
}

// 2. Collector — executes SQL, returns raw data; also implements IAsyncDisposable
internal sealed class MySQLMetadataCollector : IMetadataCollector, IAsyncDisposable
{
    public DatabaseEngine Engine => DatabaseEngine.MySQL;
    public string ServerVersion => ...;
    public int MajorVersion => ...;

    public Task<T?> QueryScalarAsync<T>(string sql, CancellationToken ct = default) { ... }
    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string sql, CancellationToken ct = default) { ... }
    public ValueTask DisposeAsync() => ...;
}
```

Register with DI:
```csharp
services.AddSqlGuardProvider<MySQLProvider>();
```

See [docs/contributing/adding-providers.md](docs/contributing/adding-providers.md) for the full guide.

---

## Running Tests

```bash
# Unit tests — no database or Docker required (fast)
dotnet test --filter "Category!=Integration"

# Report writer tests specifically
dotnet test tests/SqlGuard.Reports.Tests

# Integration tests — requires Docker
dotnet test tests/SqlGuard.Integration.Tests --filter "Category=Integration"

# All tests
dotnet test
```

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up real SQL Server and PostgreSQL containers automatically via Docker.

---

## CI/CD Integration

### GitHub Actions — full example

```yaml
# .github/workflows/db-security.yml
name: Database Security Scan

on:
  schedule:
    - cron: '0 6 * * 1'     # Weekly Monday 06:00 UTC
  push:
    branches: [ main ]

permissions:
  security-events: write     # Required for SARIF upload

jobs:
  scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Install SqlGuard
        run: dotnet tool install -g SqlGuard.Cli

      # Mandatory rules — blocks the pipeline on failure
      - name: SqlGuard Security Scan
        run: |
          sqlguard scan \
            --config sqlguard.yml \
            --report console,json,html,sarif \
            --output ./reports \
            --fail-on mandatory
        env:
          SQLGUARD_DB_PASS: ${{ secrets.PROD_DB_PASSWORD }}

      # Upload SARIF to GitHub Security → Code scanning tab
      - name: Upload SARIF
        uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: ./reports/sqlguard-report.sarif

      # Upload reports as downloadable artifacts
      - name: Upload reports
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: sqlguard-reports
          path: ./reports/
          retention-days: 90
```

### Multi-server scan in CI

```yaml
- name: Scan all production servers
  run: |
    sqlguard inventory \
      --file servers.json \
      --report html,json,sarif \
      --output ./reports \
      --fail-on mandatory
  env:
    PROD_DB_PASS: ${{ secrets.PROD_DB_PASS }}
    PG_PASS:      ${{ secrets.PG_PASS }}
```

### Minimum-permission scanner account

```sql
-- SQL Server: create a read-only scanner login
CREATE LOGIN [sqlguard_scanner]
  WITH PASSWORD = N'<strong_password>',
       CHECK_POLICY = ON, CHECK_EXPIRATION = ON;

GRANT VIEW SERVER STATE          TO [sqlguard_scanner];
GRANT VIEW ANY DEFINITION        TO [sqlguard_scanner];
GRANT VIEW ANY DATABASE          TO [sqlguard_scanner];
GRANT VIEW SERVER PERFORMANCE STATE TO [sqlguard_scanner];
```

```sql
-- PostgreSQL: minimum permissions
CREATE ROLE sqlguard_scanner WITH LOGIN PASSWORD '<strong_password>';
GRANT pg_monitor TO sqlguard_scanner;
GRANT SELECT ON pg_hba_file_rules TO sqlguard_scanner;
```

---

## Roadmap

| Milestone | Status |
|-----------|--------|
| SQL Server core rules (MSSQL-CORE-001..022) | ✅ v0.1 |
| PostgreSQL core rules (PG-CORE-001..008) | ✅ v0.1 |
| 6 report formats (console, JSON, HTML, SARIF, CSV, Markdown) | ✅ v0.1 |
| Config file + secure credential handling | ✅ v0.1 |
| Multi-server inventory scanning | ✅ v0.1 |
| `init-config` scaffold command | ✅ v0.1 |
| `validate-rule` CI command | ✅ v0.1 |
| SQL Server CIS Benchmark pack | 🔜 v0.2 |
| PostgreSQL CIS Benchmark pack | 🔜 v0.2 |
| MySQL / MariaDB provider | 🔜 v0.3 |
| Oracle 19c+ provider | 🔜 v0.4 |
| Azure SQL / RDS cloud metadata rules | 🔜 v0.5 |
| Web dashboard (ASP.NET Core) | 🔜 v1.0 |
| Scan result diff / trend tracking | 🔜 v1.0 |

---

## Contributing

Contributions are welcome. The most impactful contribution is a **new YAML rule** — no C# required.

- Read [CONTRIBUTING.md](docs/contributing.md) before opening a PR
- Use the [Rule Request template](.github/ISSUE_TEMPLATE/rule_request.md) to propose new rules
- All rule PRs must include: a passing `validate-rule` check, a unit test using a mock `IMetadataCollector`, and at least one compliance reference
- All code PRs must pass `dotnet format --verify-no-changes` and all existing tests

**Code of Conduct:** This project follows the [Contributor Covenant v2.1](CODE_OF_CONDUCT.md).

---

## Security

SqlGuard never transmits scan results anywhere. All output is written locally.

To report a vulnerability in SqlGuard itself, see [SECURITY.md](SECURITY.md).

---

## License

MIT © 2024 SqlGuard Contributors — see [LICENSE](LICENSE).

> SqlGuard is not affiliated with Microsoft, PostgreSQL Global Development Group, CIS, NIST, or DISA.

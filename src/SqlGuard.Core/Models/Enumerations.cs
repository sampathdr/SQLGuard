using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    public enum RuleStatus { Pass, Fail, Warn, Error, NotApplicable, Skipped }
    public enum Severity { Info, Low, Medium, High, Critical }
    public enum RuleOperator { Equals, NotEquals, GreaterThan, LessThan, Contains, NotContains, In, NotIn, Regex, Custom }
    public enum DatabaseEngine { SqlServer, PostgreSQL, MySQL, Oracle }
    public enum ReportFormat { Console, Json, Html, Markdown, Sarif, Csv }

    /// <summary>
    /// Authentication strategy for a database connection.
    /// Determines how credentials are sourced — never stored in source control.
    /// </summary>
    public enum AuthType
    {
        /// <summary>Username + password (sourced from env vars or vault).</summary>
        Password,
        /// <summary>Windows / Kerberos integrated authentication (SQL Server).</summary>
        Integrated,
        /// <summary>PostgreSQL peer authentication (Unix socket).</summary>
        Peer,
        /// <summary>Azure Managed Identity / Workload Identity.</summary>
        ManagedIdentity,
        /// <summary>Connection string provided in full via env var (never in config files).</summary>
        ConnectionString
    }
}

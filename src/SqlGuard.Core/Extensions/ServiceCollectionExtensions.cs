using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Configuration;
using SqlGuard.Core.Evaluation;
using SqlGuard.Core.Engine;

namespace SqlGuard.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all SqlGuard Core services.
        /// After calling this, register providers with <see cref="AddSqlGuardProvider{TProvider}"/>
        /// and report writers with <see cref="AddSqlGuardReportWriter{TWriter}"/>.
        /// </summary>
        public static IServiceCollection AddSqlGuardCore(this IServiceCollection services,
        Action<SqlGuardCoreOptions>? configure = null)
        {
            var opts = new SqlGuardCoreOptions();
            configure?.Invoke(opts);

            services.AddLogging(b => b.SetMinimumLevel(opts.MinimumLogLevel));

            // Configuration
            services.AddSingleton<ConfigLoader>();
            services.AddSingleton<InventoryLoader>();

            // Rule engine
            services.AddSingleton<YamlRulePackLoader>(sp =>
            {
                var loader = new YamlRulePackLoader();
                foreach (var asm in opts.RulePackAssemblies)
                    loader.RegisterAssembly(asm);
                return loader;
            });
            services.AddSingleton<IRulePackLoader>(sp => sp.GetRequiredService<YamlRulePackLoader>());
            services.AddSingleton<IEvaluationEngine, EvaluationEngine>();
            services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();
            services.AddSingleton<IMultiScanOrchestrator, MultiScanOrchestrator>();

            return services;
        }

        /// <summary>Register a database provider (e.g. SqlServerProvider, PostgreSQLProvider).</summary>
        public static IServiceCollection AddSqlGuardProvider<TProvider>(this IServiceCollection services)
            where TProvider : class, IDatabaseProvider
        {
            services.AddSingleton<IDatabaseProvider, TProvider>();
            return services;
        }

        /// <summary>Register a report writer. All registered writers are used by ReportPipeline.</summary>
        public static IServiceCollection AddSqlGuardReportWriter<TWriter>(this IServiceCollection services)
            where TWriter : class, IReportWriter
        {
            services.AddSingleton<IReportWriter, TWriter>();
            return services;
        }
    }

    public sealed class SqlGuardCoreOptions
    {
        public List<System.Reflection.Assembly> RulePackAssemblies { get; } = [];
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Warning;
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Pegasus.Infrastructure.Persistence;

public static class PegasusDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UsePegasusSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        optionsBuilder.UseSqlite(connectionString);
        ConfigureProviderNeutralMigrations(optionsBuilder);
        return optionsBuilder;
    }

    public static DbContextOptionsBuilder UsePegasusSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(connection);

        optionsBuilder.UseSqlite(connection);
        ConfigureProviderNeutralMigrations(optionsBuilder);
        return optionsBuilder;
    }

    public static DbContextOptionsBuilder<TContext> UsePegasusSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(connection);

        optionsBuilder.UseSqlite(connection);
        ConfigureProviderNeutralMigrations(optionsBuilder);
        return optionsBuilder;
    }

    private static void ConfigureProviderNeutralMigrations(DbContextOptionsBuilder optionsBuilder)
    {
#pragma warning disable EF1001 // The single provider-neutral migration chain needs a provider-normalized snapshot.
        optionsBuilder.ReplaceService<IMigrationsAssembly, SqliteMigrationsAssembly>();
#pragma warning restore EF1001
    }
}

#pragma warning disable EF1001 // MigrationsAssembly is the supported EF service behind IMigrationsAssembly replacement.
internal sealed class SqliteMigrationsAssembly : MigrationsAssembly
{
    private readonly ICurrentDbContext _currentContext;
    private readonly IDatabaseProvider _databaseProvider;

    public SqliteMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger,
        IDatabaseProvider databaseProvider)
        : base(currentContext, options, idGenerator, logger)
    {
        _currentContext = currentContext;
        _databaseProvider = databaseProvider;
    }
    private const string SqlServerAnnotationPrefix = "SqlServer:";
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private ModelSnapshot? _modelSnapshot;
    private bool _modelSnapshotInitialized;

    public override ModelSnapshot? ModelSnapshot
    {
        get
        {
            if (!string.Equals(_databaseProvider.Name, SqliteProviderName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"SQLite migration snapshot normalization cannot run for provider '{_databaseProvider.Name}'.");
            }

            if (!_modelSnapshotInitialized)
            {
                _modelSnapshot = base.ModelSnapshot;
                if (_modelSnapshot?.Model is { } model)
                {
                    NormalizeForSqlite(model, _currentContext.Context.Model);
                }

                _modelSnapshotInitialized = true;
            }

            return _modelSnapshot;
        }
    }

    private static void NormalizeForSqlite(IModel model, IModel sqliteModel)
    {
        if (model is not IMutableModel mutableModel)
        {
            throw new InvalidOperationException(
                "The Pegasus migration snapshot must be mutable before SQLite normalization.");
        }

        mutableModel.RemoveAnnotation(RelationalAnnotationNames.MaxIdentifierLength);
        RemoveSqlServerAnnotations(mutableModel);

        foreach (var entityType in mutableModel.GetEntityTypes())
        {
            RemoveSqlServerAnnotations(entityType);
            var sqliteEntityType = sqliteModel.FindEntityType(entityType.Name);

            foreach (var property in entityType.GetProperties())
            {
                if (sqliteEntityType?.FindProperty(property.Name) is { } sqliteProperty
                    && sqliteProperty.FindAnnotation(RelationalAnnotationNames.ColumnType) is null)
                {
                    property.SetColumnType(sqliteProperty.GetRelationalTypeMapping().StoreType);
                }

                RemoveSqlServerAnnotations(property);
            }

            foreach (var key in entityType.GetKeys())
            {
                RemoveSqlServerAnnotations(key);
            }

            foreach (var index in entityType.GetIndexes())
            {
                RemoveSqlServerConventionFilter(index);
                RemoveSqlServerAnnotations(index);
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                RemoveSqlServerAnnotations(foreignKey);
            }
        }
    }

    private static void RemoveSqlServerConventionFilter(IMutableIndex index)
    {
        if (!index.IsUnique || index.GetFilter() is not { } filter)
        {
            return;
        }

        var nullableProperties = index.Properties
            .Where(property => property.IsNullable)
            .ToArray();
        var columnNames = nullableProperties
            .Select(property => property.GetColumnName())
            .ToArray();
        if (columnNames.Length == 0 || columnNames.Any(string.IsNullOrEmpty))
        {
            return;
        }

        var conventionFilter = string.Join(
            " AND ",
            columnNames.Select(columnName => $"[{columnName}] IS NOT NULL"));
        if (string.Equals(filter, conventionFilter, StringComparison.Ordinal))
        {
            index.SetFilter(null);
        }
    }

    private static void RemoveSqlServerAnnotations(IMutableAnnotatable metadata)
    {
        foreach (var annotation in metadata.GetAnnotations()
                     .Where(annotation => annotation.Name.StartsWith(
                         SqlServerAnnotationPrefix,
                         StringComparison.Ordinal))
                     .ToArray())
        {
            metadata.RemoveAnnotation(annotation.Name);
        }
    }
}
#pragma warning restore EF1001

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ActuarialTranslationEngine.Core.Persistence;

namespace ActuarialTranslationEngine.Persistence;

/// <summary>
/// Extension methods to register the actuarial persistence layer with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ActuarialDbContext and SqlitePersistenceManager with the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="databasePath">The file path for the SQLite .db file (e.g. "audit.db").</param>
    public static IServiceCollection AddActuarialPersistence(
        this IServiceCollection services,
        string databasePath)
    {
        services.AddDbContext<ActuarialDbContext>(options =>
            options.UseSqlite($"DataSource={databasePath}"));

        services.AddScoped<IPersistenceManager, SqlitePersistenceManager>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;

namespace Demo.Embedding.Web.Configurations;

public static class InfraConfig
{
    public static IServiceCollection AddInfraStructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextFactory<AppEmbeddingDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
            })
            .EnableSensitiveDataLogging(false)  // Desabilitar dados sensíveis nos logs
            .EnableDetailedErrors(false)        // Desabilitar erros detalhados nos logs
            .LogTo(Console.WriteLine, LogLevel.None); // Desabilitar logs completamente
        });

        return services;
    }
}
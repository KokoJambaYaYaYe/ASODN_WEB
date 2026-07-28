using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCommon.Database.Infrastructure.Extention;

public static class AuthSystemDBExt
{
    public static void RegisterAuthDBContextExt(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AuthSystemDataBaseDBContext>(
            (serviceProvider, options) =>
            {
                options
                    .UseNpgsql(configuration.GetConnectionString("PostgresqlAuthDBConnection"));
                    // Подключаем интерцептор кеширования к пайплайну Entity Framework.
                    //.AddInterceptors(
                    //    serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>()
                    //);
            }
        );
    }
}

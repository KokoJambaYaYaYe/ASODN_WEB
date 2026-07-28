using System.Security.Cryptography.X509Certificates;
using Common.Redis.Constants;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuthCommon.Redis.Extension;

public static class RedisExt
{
    public static void AddDataProtectionPersistKeysToStackExchangeRedisExt(this IServiceCollection services, ConnectionMultiplexer redisMultiplexer, IConfiguration configuration)
    {
        string redisKey = $"{configuration["RedisSettings:ApplicationName"]}:DataProtection:";

        // Формируем полный путь к сертификату
        string certPath = Path.Combine(
            AppContext.BaseDirectory,
            configuration["CertificateSettings:PathToFile"],
            configuration["CertificateSettings:FileName"]
        );

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException(
                $"Certificate for DataProtection not found at: {certPath}"
            );
        }

        // Загружаем сертификат (рекомендуется использовать X509CertificateLoader в .NET 9+)
        X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile(
            certPath,
            configuration["CertificateSettings:Password"]
        );

        services
            .AddDataProtection()
            // 1. Указываем, где хранить ключи
            .PersistKeysToStackExchangeRedis(
                () => redisMultiplexer.GetDatabase((int)RedisDbRoleConst.Auth_Cache),
                redisKey + "Data-Protection-Key"
            )
            // 2. Шифруем сами ключи в Redis этим сертификатом
            .ProtectKeysWithCertificate(cert)
            // 3. Изолируем ключи этого приложения от других
            .SetApplicationName($"{configuration["RedisSettings:ApplicationName"]}")
            // 4. Увеличиваем срок жизни ключей (по умолчанию 90 дней)
            .SetDefaultKeyLifetime(TimeSpan.FromDays(180));
    }

    //public static void AddEFSecondLevelCacheExt(this IServiceCollection services,ConfigurationOptions redisOptions,IConfiguration configuration)
    //{
    //    var expiry = TimeSpan.FromMinutes(10);
    //    var appName = configuration["RedisSettings:ApplicationName"];
    //    var redisConnection = configuration.GetConnectionString("RedisConnection");

    //    services.AddEFSecondLevelCache(options =>
    //        options
    //            .UseStackExchangeRedisCacheProvider(
    //                $"{redisConnection},defaultDatabase={(int)RedisDbRoleConst.EFCoreSecondLevel_Cache}",
    //                expiry
    //            )
    //            .UseCacheKeyPrefix($"{appName}:EFSecondLevelCacheStore:")
    //    );
    //}
}

using Common.Redis.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Security.Claims;

namespace AuthCommon.Redis;

/// <summary>
/// Реализация хранилища тикетов (сессий) в Redis.
/// Позволяет хранить тяжелые данные аутентификации на сервере,
/// передавая клиенту лишь компактный идентификатор (ключ).
/// </summary>
public class RedisTicketStore : ITicketStore
{
    // Префикс для ключей в Redis, чтобы сессии не перемешивались с другими данными
    private const string KeyPrefix = "AuthTicket-";

    // Префикс для lock-ключей
    private const string LockPrefix = "AuthTicketLock-";

    private readonly IDatabase _authCacheDB;

    // Стандартный сериализатор ASP.NET Core для превращения объекта AuthenticationTicket в байты
    private readonly TicketSerializer _serializer = TicketSerializer.Default;

    // Интерфейс для шифрования данных
    private readonly IDataProtector _protector;

    private readonly IConfiguration _configuration;

    /// <param name="cache">Распределенный кэш (обычно StackExchange.Redis)</param>
    /// <param name="dataProtectionProvider">Провайдер для создания средств защиты данных</param>
    public RedisTicketStore(
        Func<RedisDbRoleConst, IDatabase> dbResolver,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration
    )
    {
        _authCacheDB = dbResolver(RedisDbRoleConst.Auth_Cache);

        // Создаем "защитника" с уникальной целью (purpose).
        // Это гарантирует, что данные, зашифрованные здесь, не смогут быть расшифрованы
        // другим компонентом системы, что повышает безопасность.
        _protector = dataProtectionProvider.CreateProtector("Common.Redis.RedisTicketStore.v1");

        _configuration = configuration;
    }

    /// <summary>
    /// Сохраняет новый тикет и возвращает уникальный ключ (ID сессии).
    /// Этот ключ в итоге попадет в браузер пользователя в зашифрованном виде.
    /// </summary>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        // 1. Получаем ID пользователя из Claims (ищем стандартный claim NameIdentifier / Sub)
        var userId = ticket.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ticket.Principal.FindFirst("sub")?.Value
                     ?? "unknown_user";

        // 2. Добавляем префикс приложения из конфигурации
        var appName = _configuration["RedisAuthSettings:AuthApplicationName"];

        // 3. Формируем ключ, включая userId
        var key = $"{appName}:TicketsStore:{userId}:{KeyPrefix + Guid.NewGuid().ToString()}";

        // Сохраняем данные в Redis
        await RenewAsync(key, ticket);

        return key;
    }

    /// <summary>
    /// Обновляет данные существующего тикета или записывает новый.
    /// </summary>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // 1. Сериализуем объект тикета (Claims, роли, сроки) в массив байтов
        byte[] serialized = _serializer.Serialize(ticket);

        // 2. Шифруем байты с помощью Data Protection API
        byte[] protectedVal = _protector.Protect(serialized);

        // 3. Вычисляем TTL
        TimeSpan? expiry = ticket.Properties.ExpiresUtc.HasValue
            ? ticket.Properties.ExpiresUtc.Value - DateTimeOffset.UtcNow
            : null;

        // Если тикет уже просрочен — удаляем
        if (expiry.HasValue && expiry.Value <= TimeSpan.Zero)
        {
            await _authCacheDB.KeyDeleteAsync(key);
            return;
        }

        // 4. Сохраняем с явным приведением к типу Expiration
        // Используем implicit conversion из TimeSpan?
        await _authCacheDB.StringSetAsync(
            key,
            protectedVal,
            expiry.HasValue ? (StackExchange.Redis.Expiration)expiry.Value : default
        );
    }

    /// <summary>
    /// Извлекает тикет из Redis по ключу и восстанавливает объект AuthenticationTicket.
    /// </summary>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        // 1. Получаем данные из Redis (метод StringGetAsync возвращает RedisValue)
        var protectedValue = await _authCacheDB.StringGetAsync(key);

        // В StackExchange.Redis проверка на null делается через свойство .HasValue или .IsNull
        if (!protectedValue.HasValue)
        {
            return null;
        }

        try
        {
            // 2. Приводим RedisValue к массиву байтов byte[]
            byte[] protectedBytes = (byte[])protectedValue!;

            // 3. Расшифровываем данные
            byte[] unprotectedBytes = _protector.Unprotect(protectedBytes);

            // 4. Десериализуем обратно в объект тикета
            return _serializer.Deserialize(unprotectedBytes);
        }
        catch (Exception)
        {
            // Ошибка может возникнуть, если ключи Data Protection обновились
            // или данные в Redis были повреждены.
            return null;
        }
    }

    /// <summary>
    /// Удаляет сессию из Redis (например, при Logout).
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        // В StackExchange.Redis метод для удаления ключа называется KeyDeleteAsync
        await _authCacheDB.KeyDeleteAsync(key);
    }

    #region Distributed Lock для Silent Refresh

    /// <summary>
    /// Попытка получить distributed lock на refresh токена.
    /// Возвращает true, если lock успешно получен.
    /// </summary>
    /// <param name="key">Уникальный ключ пользователя</param>
    /// <param name="ttl">Время жизни lock</param>
    public async Task<bool> TryAcquireLockAsync(string key, TimeSpan ttl)
    {
        var lockKey = LockPrefix + key;

        // Redis Set NX PX — установить ключ, если его нет, с TTL
        return await _authCacheDB.StringSetAsync(
            lockKey,
            Environment.MachineName, // значение можно любое, главное — уникальность
            ttl,
            when: When.NotExists
        );
    }

    /// <summary>
    /// Освобождение distributed lock.
    /// </summary>
    /// <param name="key">Уникальный ключ пользователя</param>
    public async Task ReleaseLockAsync(string key)
    {
        var lockKey = LockPrefix + key;
        await _authCacheDB.KeyDeleteAsync(lockKey);
    }

    #endregion
}

using Microsoft.AspNetCore.Authentication.Cookies;

namespace AuthCommon.Redis;

public static class RedisTicketStoreExt
{
    public static Task<bool> TryAcquireLockAsync(this ITicketStore store, string key, TimeSpan ttl)
    {
        if (store is RedisTicketStore redisStore)
            return redisStore.TryAcquireLockAsync(key, ttl);

        throw new NotSupportedException("Lock доступен только для RedisTicketStore");
    }

    public static Task ReleaseLockAsync(this ITicketStore store, string key)
    {
        if (store is RedisTicketStore redisStore)
            return redisStore.ReleaseLockAsync(key);

        throw new NotSupportedException("Lock доступен только для RedisTicketStore");
    }
}

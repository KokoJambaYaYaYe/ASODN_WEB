using AuthCommon.Redis.Extension;
using AuthSystem.Service.Abstraction.IService;
using AuthSystem.Service.Service;
using Common.OpenIdDict.Extension;
using Common.Redis.Constants;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;

#region Builder

var builder = WebApplication.CreateBuilder(args);

// Подключаем Serilog, читая настройки из builder.Configuration
builder.Host.UseSerilog(
    (context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
);

builder.Services.AddOpenApi();

// --- НАСТРОЙКА CORS ---
builder.Services.AddCors(options => {
    options.AddPolicy(
        "AllowReactApp",
        policy => {
            policy
                // КРИТИЧНО: Меняем https на http и добавляем оба порта
                .WithOrigins("https://asodn.mod.com", "https://localhost:60113", "https://localhost:63554")

                // Для надежности при работе с Negotiate лучше оставить .AllowAnyMethod() и .AllowAnyHeader(),
                // так как браузер при handshake может генерировать специфические заголовки
                .AllowAnyMethod()
                .AllowAnyHeader()

                // КРИТИЧНО: Разрешает передачу Windows-сессии/Cookies
                .AllowCredentials();
        }
    );
});

#region Redis

// --- НАСТРОЙКА REDIS ---
// Используется для кеширования, хранения сессий и защиты данных.

// Получаем настройки подключения
var redisConnString = builder.Configuration.GetConnectionString("RedisConnection");
var redisOptions = ConfigurationOptions.Parse(redisConnString);


// Регистрация мультиплексора как Singleton для эффективного использования соединений
var redisMultiplexer = ConnectionMultiplexer.Connect(redisOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

// Регистрируем IDistributedCache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConnectionMultiplexerFactory = () =>
        Task.FromResult<IConnectionMultiplexer>(redisMultiplexer);
});

// Резолвер, который по ключу достает базу из контейнера
builder.Services.AddSingleton<Func<RedisDbRoleConst, IDatabase>>(sp =>
    role => sp.GetRequiredKeyedService<IDatabase>(role)
);

// Пример регистрации конкретных баз
builder.Services.AddKeyedSingleton<IDatabase>(
    RedisDbRoleConst.Auth_Cache,
    (sp, key) => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase((int)RedisDbRoleConst.Auth_Cache)
);

#endregion Redis


// Data Protection: Хранение ключей шифрования кук в Redis (чтобы сессия не слетала при перезапуске сервера)
builder.Services.AddDataProtectionPersistKeysToStackExchangeRedisExt(redisMultiplexer, builder.Configuration);

builder.Services.AddOpenIdDictExt(builder.Configuration);

builder.Services.AddAuthentication(options => {
    // Для проверки токенов в защищенных эндпоинтах API (Bearer)
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

    // КРИТИЧНО: Если пользователь не авторизован в браузере, 
    // по умолчанию отправляем его на стандартную схему Кук Identity
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
})
.AddNegotiate();

builder.Services.AddAuthorization(options => {
    // Эта политика применяется ко всем стандартным [Authorize] контроллерам
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
            IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .Build();

    // КРИТИЧНО: Создаем ОТДЕЛЬНУЮ политику чисто под Windows Auth
    options.AddPolicy("WindowsAuthPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(NegotiateDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddScoped<IUserWindowsAuthService, UserWindowsAuthService>();

// --- БИЗНЕС-ЛОГИКА ---
// Регистрация контроллеров
builder.Services.AddControllers();


#endregion Builder

#region APP
var app = builder.Build();

// Использовать заголовки Nginx (X-Forwarded-For и X-Forwarded-Proto) в Production.
// Этот блок ОБЯЗАТЕЛЬНО должен идти самым первым, до маршрутизации и CORS.
if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                           Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });
}
else
{
    app.MapOpenApi(); // Генерирует спецификацию при разработке
    app.MapScalarApiReference(); // Отображает UI
    app.UseDeveloperExceptionPage(); // Подробные ошибки при разработке
}

// Логирование HTTP-запросов через Serilog
app.UseSerilogRequestLogging();

// Включаем маршрутизацию (определяем, какой контроллер вызван)
app.UseRouting();

// Применяем правила CORS строго ПОСЛЕ UseRouting, но ДО авторизации
app.UseCors("AllowReactApp");

// Перенаправление на HTTPS. 
// ВНИМАНИЕ: Если у вас SSL-сертификат (PFX) настроен внутри самого .NET приложения, 
// эту строку нужно оставить. Если SSL «терминируется» (настроен) на Nginx, 
// а до .NET запрос идет по обычному HTTP, эту строку лучше закомментировать // app.UseHttpsRedirection();
//app.UseHttpsRedirection();

// Проверяем токены/куки (Кто делает запрос)
app.UseAuthentication();

// Проверяем права доступа (Разрешено ли действие)
app.UseAuthorization();

// Маппинг эндпоинтов контроллеров
app.MapControllers();

app.Run();
#endregion APP

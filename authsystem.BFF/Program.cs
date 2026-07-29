using AuthCommon.Redis.Extension;
using AuthSystem.Service.Abstraction.IService;
using AuthSystem.Service.Service;
using Common.OpenIdDict.Extension;
using Common.Redis.Constants;
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
                .WithOrigins("https://localhost:60113", "https://localhost:63554")

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
.AddNegotiate(); // Оставляем поддержку Windows-аутентификации

builder.Services.AddAuthorization(options => {
    // Вместо DefaultPolicy настраиваем DefaultPolicy.
    // Она сработает на всех контроллерах, где написано просто [Authorize]
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
            IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<IUserWindowsAuthService, UserWindowsAuthService>();

// --- БИЗНЕС-ЛОГИКА ---
// Регистрация контроллеров
builder.Services.AddControllers();


#endregion Builder

#region APP

var app = builder.Build();

if (app.Environment.IsProduction())
{
    // Этот блок должен идти до аутентификации, авторизации и маршрутизации:
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

// (Опционально) Логирование HTTP-запросов
app.UseSerilogRequestLogging();

// --- КОНВЕЙЕР ОБРАБОТКИ (MIDDLEWARE) ---
// ПОРЯДОК ВЫЗОВОВ ИМЕЕТ ЗНАЧЕНИЕ ДЛЯ БЕЗОПАСНОСТИ

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Генерирует JSON/YAML спецификацию
    app.MapScalarApiReference(); // Отображает UI
    app.UseDeveloperExceptionPage(); // Подробные ошибки в консоли при разработке
}

app.UseRouting(); // 1. Определяем, какой маршрут вызван

// 2. Применяем правила CORS до того, как сработает авторизация
app.UseCors("AllowReactApp");

app.UseForwardedHeaders();

app.UseHttpsRedirection();

// 3. Проверяем, кто делает запрос (токены/куки)
app.UseAuthentication();

// 4. Проверяем, есть ли у пользователя права на действие
app.UseAuthorization();

// 5. Выполняем логику контроллеров
app.MapControllers();

app.Run();

#endregion APP
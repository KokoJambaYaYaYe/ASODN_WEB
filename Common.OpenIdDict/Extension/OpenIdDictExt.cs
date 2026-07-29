using AuthCommon.Redis;
using AuthCommon.Database.Infrastructure;
using AuthCommon.Database.Infrastructure.Extention;
using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using Quartz;

namespace Common.OpenIdDict.Extension;

public static class OpenIdDictExt
{
    public static void AddOpenIdDictExt(this IServiceCollection services, IConfiguration configuration)
    {

        // Находим зарегистрированное окружение в коллекции сервисов
        var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IWebHostEnvironment));
        var environment = serviceDescriptor?.ImplementationInstance as IWebHostEnvironment;

        // Если через Instance не нашлось, можно проверить через фабрику (альтернативный безопасный вариант)
        bool isDevelopment = environment?.IsDevelopment() ?? true;



        // 1. ОБЯЗАТЕЛЬНО: Регистрируем сам контекст БД в DI и указываем провайдер (например, PostgreSQL)
        services.RegisterAuthDBContextExt(configuration);

        services.AddIdentity<User, Role>(options =>
                    {
                        options.Password.RequireDigit = true;
                        options.Password.RequiredLength = 8;
                        options.Password.RequireNonAlphanumeric = false;

                        options.User.AllowedUserNameCharacters =
                            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                        options.User.RequireUniqueEmail = true; // Рекомендуется оставить уникальность Email
                    })
                .AddEntityFrameworkStores<AuthSystemDataBaseDBContext>()
                .AddDefaultTokenProviders();

        services.AddQuartz();
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        // --- НАСТРОЙКА OPENIDDICT (Авторизация) ---
        services.AddOpenIddict()
                .AddCore(options =>
                {
                    // Настройка хранилища: используем EF Core для хранения приложений, токенов и областей (scopes)
                    options.UseEntityFrameworkCore()
                           .UseDbContext<AuthSystemDataBaseDBContext>()
                           // Используем стандартные сущности, но с типом ключа long
                           .ReplaceDefaultEntities<long>();

                    // Интеграция с Quartz.net для автоматической очистки просроченных токенов из БД
                    options.UseQuartz();
                })
                .AddServer(options =>
                {
                    /* --- НАСТРОЙКА ЭНДПОИНТОВ (Точки входа) --- */

                    options.SetAuthorizationEndpointUris("/connect/authorize") // Страница логина/согласия
                           .SetTokenEndpointUris("/connect/token") // Выдача токенов (обмен кода на токен)
                           .SetUserInfoEndpointUris("/connect/userinfo") // Данные о пользователе
                           .SetEndSessionEndpointUris("/connect/logout"); // Эндпоинт завершения сессии (Logout).

                    /* --- ГРАНТЫ И БЕЗОПАСНОСТЬ (Flows) --- */

                    options.AllowAuthorizationCodeFlow(); // Самый безопасный флоу для Web/Mobile
                    options.RequireProofKeyForCodeExchange(); // Обязательный PKCE для защиты от перехвата кода
                    options.AllowRefreshTokenFlow(); // Позволяет обновлять Access Token без повторного логина

                    // Использование Reference Tokens вместо JWT (в БД хранится лишь идентификатор, а не весь токен)
                    // Это позволяет мгновенно отозвать токен, но требует обращения к БД при каждой валидации
                    options.UseReferenceRefreshTokens();

                    /* --- КРИПТОГРАФИЯ --- */

                    // ВНИМАНИЕ: Для Production нужно использовать .AddSigningCertificate / .AddEncryptionCertificate
                    // Development-методы создают временные сертификаты, которые сгорают при перезапуске сервера
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();
                    //ДЛЯ ПРОДА ЗАМЕНИТЬ НА:
                    /*
                     * options.AddSigningCertificate(
                        new X509Certificate2("signing.pfx", password));

                    options.AddEncryptionCertificate(
                        new X509Certificate2("encryption.pfx", password));
                    */


                    /* --- ОБЛАСТИ ДОСТУПА (Scopes) --- */

                    options.RegisterScopes(
                        OpenIddictConstants.Scopes.OpenId, // Базовый маркер OIDC
                        OpenIddictConstants.Scopes.Profile, // Доступ к имени, фамилии и т.д.
                        OpenIddictConstants.Scopes.Email, // Доступ к email
                        OpenIddictConstants.Scopes.Roles, // Доступ к ролям пользователя
                        OpenIddictConstants.Scopes.OfflineAccess, // Позволяет выдавать Refresh Tokens
                        "api1"
                    );

                    /* --- ИНТЕГРАЦИЯ С ASP.NET CORE --- */

                    options.UseAspNetCore()
                           .EnableAuthorizationEndpointPassthrough()
                           .EnableTokenEndpointPassthrough()
                           .EnableUserInfoEndpointPassthrough()
                           .EnableEndSessionEndpointPassthrough();
                })
                .AddValidation(options =>
                {
                    // Настройка валидации токенов внутри этого же приложения (API и Auth Server совмещены)
                    options.UseLocalServer();
                    options.UseAspNetCore();
                    // КРИТИЧНО: Заставляет бэкенд при каждом запросе сверяться с Redis.
                    // Как только статус токена изменится на Revoked, запрос вернет 401.
                    options.EnableTokenEntryValidation();

                });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
        });

        // Регистрируем реализацию ITicketStore в контейнере зависимостей (DI).
        services.AddSingleton<ITicketStore, RedisTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
                .Configure<ITicketStore>(
                    (options, store) =>
                    {
                        // Теперь вместо того, чтобы сериализовать все Claims и токены в саму куку,
                        // ASP.NET отправит их в Redis через store.StoreAsync(), а в куку запишет только ID.
                        options.SessionStore = store;
                    }
                );

        // Глобальная настройка параметров куки Identity
        services.ConfigureApplicationCookie(options =>
        {
            // Запрашиваем наш TicketStore из DI контейнера
            var serviceProvider = services.BuildServiceProvider();
            options.SessionStore = serviceProvider.GetRequiredService<ITicketStore>();



            // Безопасное имя куки. Префикс __Host- запрещает передачу куки через HTTP (только HTTPS)
            // и ограничивает её использование только тем доменом, который её установил.
            //options.Cookie.Name = "__Host-AuthSystem-Session";
            // Для локальной разработки без прокси уберите префикс __Host-, иначе браузер не сохранит куку
            options.Cookie.Name = isDevelopment
                ? "AuthSystem-Session-Dev"
                : "__Host-AuthSystem-Session";

            // Защита от XSS: JavaScript на фронтенде не сможет прочитать эту куку.
            options.Cookie.HttpOnly = true;

            // Путь "/" в сочетании с префиксом __Host- гарантирует доступность куки для всего приложения.
            options.Cookie.Path = "/";

            // SameSiteMode.Lax: кука будет отправлена при навигации пользователя (GET) с внешних сайтов,
            // но заблокирована при выполнении POST-запросов со сторонних ресурсов (защита от CSRF).
            options.Cookie.SameSite = SameSiteMode.Lax;

            // CookieSecurePolicy.Always: кука никогда не будет отправлена по незащищенному соединению.
            // ВАЖНО: Требует наличия SSL (HTTPS) даже на локальной машине при разработке.
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromHours(8);

            options.SlidingExpiration = true;

            options.Cookie.IsEssential = true;

            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/access-denied";

            /* --- ОБРАБОТКА СОБЫТИЙ (Events) --- */

            // OnRedirectToLogin: вызывается, когда анонимный пользователь пытается зайти в защищенную зону.
            options.Events.OnRedirectToLogin = context =>
            {
                // Оставляем оригинальный тип PathString вместо string
                var requestPath = context.Request.Path;

                // 1. Проверяем, идет ли запрос к API, GraphQL или токенам OpenIddict
                var isApiOrTokenRequest = requestPath.StartsWithSegments("/authsystem_api")
                                       || requestPath.StartsWithSegments("/connect/token");

                if (isApiOrTokenRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                // 2. КРИТИЧНО: Если неавторизованный пользователь зашел на /connect/authorize
                if (requestPath.StartsWithSegments("/connect/authorize"))
                {
                    // Базовый URL вашего фронтенда из конфигурации
                    var reactAppUrl = "https://localhost:63554";

                    // Формируем returnUrl, который содержит весь исходный OIDC запрос (/connect/authorize?client_id=...)
                    var returnUrl = context.Request.Path + context.Request.QueryString;

                    // Формируем адрес вашей React-формы авторизации
                    var loginRedirectUrl = $"{reactAppUrl}/auth?returnUrl={Uri.EscapeDataString(returnUrl)}";

                    // Перенаправляем браузер на фронтенд Vite
                    context.Response.Redirect(loginRedirectUrl);
                    return Task.CompletedTask;
                }

                // Для всех остальных страниц бэкенда выполняем стандартное поведение
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };


            // OnRedirectToAccessDenied: вызывается, когда пользователь залогинен, но у него нет прав (роли).
            options.Events.OnRedirectToAccessDenied = context => {
                var requestPath = context.Request.Path;

                // Проверяем все типы API и OIDC эндпоинтов
                var isApiOrOidcRequest = requestPath.StartsWithSegments("/authsystem_api")
                                      || requestPath.StartsWithSegments("/connect")
                                      || (context.Request.Headers["Accept"].Any(x => x.Contains("application/json")));

                if (isApiOrOidcRequest)
                {
                    // Возвращаем чистый 403 Forbidden для фронтенда вместо редиректа
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }
                return Task.CompletedTask;
            };

        });
    }
}

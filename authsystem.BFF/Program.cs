using AuthCommon.Redis.Extension;
using AuthSystem.BFF.Service;
using AuthSystem.BFF.Service.Abstraction.IService;
using AuthSystem.Service.Abstraction.IService;
using Common.OpenIdDict.Extension;
using Common.Serilog.Extension;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;
using Scalar.AspNetCore;
using Serilog;

#region Builder

var builder = WebApplication.CreateBuilder(args);

// Подключаем Serilog, читая настройки из builder.Configuration
builder.Host.AddSerilogExt();

builder.Services.AddOpenApi();

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

builder.Services.AddRedisCacheForAuthCheckExt(builder.Configuration);

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



// --- БИЗНЕС-ЛОГИКА ---
// Регистрация контроллеров
builder.Services.AddControllers();

builder.Services.AddScoped<IUserWindowsAuthService, UserWindowsAuthService>();
builder.Services.AddScoped<IUserCredentialsAuthService, UserCredentialsAuthService>();

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

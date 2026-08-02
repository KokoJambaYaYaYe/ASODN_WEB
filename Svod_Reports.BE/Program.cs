using AuthCommon.Redis.Extension;
using Common.Serilog.Extension;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Подключаем Serilog, читая настройки из builder.Configuration
builder.Host.AddSerilogExt();

// ==========================================
// 1. РЕГИСТРАЦИЯ СЕРВИСОВ (DI)
// ==========================================
builder.Services.AddControllers();
builder.Services.AddRazorPages(); // Обязательно для активации статики из Nuget-библиотек (_content)

// Генерирует OpenAPI (Swagger) спецификацию средствами Microsoft
builder.Services.AddOpenApi();
builder.Services.AddFastReport();

// Настройка политики CORS для интеграции с React
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://localhost:61572",
            "https://localhost:63554",
            "https://asod.mod.com"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials() // Важно для FastReport и передачи кук!
        // КРИТИЧНО ДЛЯ СКАЧИВАНИЯ ФАЙЛОВ: Открываем заголовки для fetch/axios
        .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
    });
});

// Регистрируем ваш кэш, DataProtection и ITicketStore
builder.Services.AddRedisCacheForAuthCheckExt(builder.Configuration);

// ИСПРАВЛЕНИЕ ОШИБКИ: Настраиваем параметры куки через AddOptions с автоматическим внедрением ITicketStore из DI
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<ITicketStore>((options, ticketStore) =>
    {
        var isDevelopment = builder.Environment.IsDevelopment();

        // Имя куки должно строго совпадать с тем, что в вашем конфиге сервера авторизации
        options.Cookie.Name = isDevelopment ? "AuthSystem-Session-Dev" : "__Host-AuthSystem-Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Внедряем хранилище сессий без вызова BuildServiceProvider()
        options.SessionStore = ticketStore;

        // КРИТИЧНО ДЛЯ ДИАГНОСТИКИ:
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context => {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context => {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },

            // Добавьте этот обработчик:
            OnValidatePrincipal = context =>
            {
                // Если мы попали сюда, кука УСПЕШНО расшифровалась и сессия найдена
                Log.Information("Кука успешно валидирована для пользователя: {User}", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Важно: на Linux очищаем списки известных сетей, так как прокси локальный
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ==========================================
// 2. НАСТРОЙКА MIDDLEWARE (ПОРЯДОК КРИТИЧЕН!)
// ==========================================

// Пробрасываем реальные IP и протоколы (HTTP/HTTPS) от Nginx
app.UseForwardedHeaders();

// ВАЖНО: Статические файлы физической папки wwwroot
app.UseStaticFiles();

// Логирование HTTP-запросов через Serilog
app.UseSerilogRequestLogging();

// Маршрутизация должна отрабатывать ДО вызова CORS, Аутентификации и контроллеров
app.UseRouting();

// CORS должен быть подключен строго МЕЖДУ UseRouting() и UseFastReport()
app.UseCors();

// Интегрируем FastReport ПОСЛЕ CORS, чтобы заголовки доступа применялись к скриптам отчета
app.UseFastReport();

if (app.Environment.IsDevelopment())
{
    // JSON спецификации (/openapi/v1.json)
    app.MapOpenApi();
    // Красивый интерфейс Scalar
    app.MapScalarApiReference();
}

// ИСПРАВЛЕНИЕ: Перед UseAuthorization ОБЯЗАТЕЛЬНО должен идти UseAuthentication, 
// иначе бэкенд не будет пытаться расшифровать куку и извлечь пользователя!
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 3. МАППИНГ ЭНДПОИНТОВ И КОНТРОЛЛЕРОВ
// ==========================================
app.MapControllers();

// Маппинг RazorPages должен регистрироваться на уровне эндпоинтов,
// именно он заставляет .NET собирать виртуальные папки "_content/" для FastReport.Web
app.MapRazorPages();

app.Run();

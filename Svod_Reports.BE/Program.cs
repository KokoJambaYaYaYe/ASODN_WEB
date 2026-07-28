using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. РЕГИСТРАЦИЯ СЕРВИСОВ (DI)
// ==========================================
builder.Services.AddControllers();
builder.Services.AddRazorPages(); // Обязательно для активации статики из Nuget-библиотек (_content)

// Генерирует OpenAPI (Swagger) спецификацию средствами Microsoft
builder.Services.AddOpenApi();
builder.Services.AddFastReport();

// Настройка политики CORS для интеграции с React
// Настройка политики CORS для интеграции с React
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "https://localhost:61572", // Старый порт микрофронтенда
                "https://localhost:63554"  // ИСПРАВЛЕНИЕ: Новый фактический порт из вашей консоли!
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Важно для FastReport
                // КРИТИЧНО ДЛЯ СКАЧИВАНИЯ ФАЙЛОВ: Открываем заголовки для fetch/axios
        .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                               Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
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

// Маршрутизация должна отрабатывать ДО вызова CORS и контроллеров
app.UseRouting();

// CORS должен быть подключен строго МЕЖДУ UseRouting() и UseFastReport()
app.UseCors();

// ИСПРАВЛЕНИЕ: Интегрируем FastReport ПОСЛЕ CORS, чтобы заголовки доступа применялись к скриптам отчета
app.UseFastReport();

if (app.Environment.IsDevelopment())
{
    // JSON спецификации (/openapi/v1.json)
    app.MapOpenApi();
    // Красивый интерфейс Scalar
    app.MapScalarApiReference();
}

//app.UseHttpsRedirection();

// Авторизация подключается строго после роутинга
app.UseAuthorization();

// ==========================================
// 3. МАППИНГ ЭНДПОИНТОВ И КОНТРОЛЛЕРОВ
// ==========================================
app.MapControllers();

// ИСПРАВЛЕНИЕ: Маппинг RazorPages должен регистрироваться на уровне эндпоинтов,
// именно он заставляет .NET собирать виртуальные папки "_content/" для FastReport.Web
app.MapRazorPages();

app.Run();

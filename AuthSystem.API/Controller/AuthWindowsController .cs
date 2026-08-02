using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Novell.Directory.Ldap;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace AuthSystem.API.Controller;

[ApiController]
[Route("authsystem_api/[controller]")]
public class AuthWindowsController : ControllerBase
{
    private readonly IUserWindowsAuthService _userWindowsAuthService;
    private readonly ILogger<AuthWindowsController> _logger;

    public AuthWindowsController(IUserWindowsAuthService userWindowsAuthService, ILogger<AuthWindowsController> logger) {
        _userWindowsAuthService = userWindowsAuthService;
        _logger = logger;
    }

    // 2. Вход для пользователей Active Directory
    // Сюда вешаем созданную нами политику. Она принудительно запустит Negotiate handshake
    [Authorize(Policy = "WindowsAuthPolicy")]
    [HttpGet("negotiate")]
    public async Task<IActionResult> NegotiateLogin([FromQuery] string returnUrl = "/")
    {
        var windowsName = User.Identity?.Name;
        if (string.IsNullOrEmpty(windowsName)) return Unauthorized();

        // Синхронизируем с вашей локальной системой
        var internalUser = await _userWindowsAuthService.FindOrCreateWindowsUserAsync(windowsName);

        // Создаем Identity для OpenIddict
        var identity = new ClaimsIdentity(
            IdentityConstants.ApplicationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        // Клеймы, которые запишутся в Redis
        // Пример явного указания мест назначения для клеймов
        // Конфликтов не будет, так как метод принадлежит самому identity
        identity.AddClaim(OpenIddictConstants.Claims.Subject, internalUser.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, internalUser.WindowsIdentity);
        identity.AddClaim("display_name", internalUser.DisplayName);

        identity.AddClaim(new Claim("amr", "wia")); // "wia" — Windows Integrated Authentication

        foreach (var role in internalUser.Roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
        }



        try
        {
            // 1. ИСПРАВЛЕНИЕ: Корректно вырезаем чистый логин из форматов DOMAIN\user и user@DOMAIN.COM
            string samAccountName = windowsName;

            if (samAccountName.Contains('\\'))
            {
                samAccountName = samAccountName.Split('\\')[1];
            }
            else if (samAccountName.Contains('@'))
            {
                samAccountName = samAccountName.Split('@')[0]; // Для lenovo@MOD.COM возьмет просто lenovo
            }

            var roles = new List<string>();

            // Приказывает системной библиотеке Linux libldap игнорировать проверку сертификата
            Environment.SetEnvironmentVariable("LDAPTLS_REQCERT", "never");

            using var connection = new LdapConnection();
            connection.SecureSocketLayer = false;

            // Подключаемся к Samba AD
            await connection.ConnectAsync("debiantechserve.mod.com", 389);

            // Вы используете встроенного Администратора домена — это сработает штатно
            await connection.BindAsync("MOD\\administrator", "Asodn123!");

            // Ищем пользователя в каталоге
            var search = await connection.SearchAsync(
                "DC=mod,DC=com",
                LdapConnection.ScopeSub,
                $"(sAMAccountName={samAccountName})", // Теперь здесь будет чистый "lenovo"
                new[] { "memberOf", "displayName", "mail" },
                false
            );

            if (!await search.HasMoreAsync())
            {
                _logger.LogWarning("Пользователь {User} не найден в AD", samAccountName);
                return Unauthorized("Пользователь не найден в AD");
            }

            var entry = await search.NextAsync();
            var memberOf = entry.GetAttributeSet()["memberOf"];

            if (memberOf != null)
            {
                foreach (var groupDn in memberOf.StringValueArray)
                {
                    // Получаем CN группы (поддерживаем любой регистр "CN=" или "cn=")
                    var cn = groupDn.Split(',')[0]
                                    .Replace("CN=", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("cn=", "", StringComparison.OrdinalIgnoreCase);
                    roles.Add(cn);
                }
            }

            // Выводим найденные группы в Serilog (Information), чтобы они гарантированно попали в логи
            foreach (var role in roles)
            {
                _logger.LogInformation("Успешно найдена AD группа пользователя: {Role}", role);
            }

            foreach (var role in roles)
            {
                identity.AddClaim(OpenIddictConstants.Claims.Role, role);
            }
        }
        catch (Exception ex)
        {
            // ИСПРАВЛЕНИЕ: Обязательно пишите ex.Message в лог, чтобы видеть РЕАЛЬНУЮ причину падения в catch
            _logger.LogError(ex, "ОШИБКА ПОДКЛЮЧЕНИЯ ИЛИ ПОИСКА В SAMBA AD!");
        }


















        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(new[] {
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles
        });

        // Назначаем дестинации через хелпер OpenIddict
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Role => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            "display_name" => [OpenIddictConstants.Destinations.AccessToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });

        // Вызываем стандартный SignInAsync для записи сессионной куки в браузер
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);


        _logger.LogInformation("AuthenticationType: {Type}", principal.Identity?.AuthenticationType);
        _logger.LogInformation("IsAuthenticated: {Auth}", principal.Identity?.IsAuthenticated);
        _logger.LogInformation("Name: {Name}", principal.Identity?.Name);

        foreach (var claim in principal.Claims)
        {
            _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
        }



        return Redirect(returnUrl);
    }

}

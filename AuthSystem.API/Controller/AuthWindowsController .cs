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
            var samAccountName = windowsName.Contains('\\')
                ? windowsName.Split('\\')[1]
                : windowsName;

            var roles = new List<string>();

            using var connection = new LdapConnection();

            // Включаем шифрование SSL/TLS
            connection.SecureSocketLayer = true;

            // Отключаем строгую проверку сертификата (для тестов с самоподписанными сертификатами Samba)
            //connection.UserDefinedServerCertValidationDelegate += (sender, certificate, chain, sslPolicyErrors) => true;

            // Подключаемся к Samba AD через безопасный порт 636
            await connection.ConnectAsync("debiantechserve.mod.com", 636);

            // Теперь Bind пройдет успешно
            await connection.BindAsync("MOD\\admin", "Asodn123!");

            // Ищем пользователя в каталоге
            var search = await connection.SearchAsync(
                "DC=mod,DC=com",
                LdapConnection.ScopeSub,
                $"(sAMAccountName={samAccountName})",
                new[]
                {
        "memberOf",
        "displayName",
        "mail"
                },
                false);

            if (!await search.HasMoreAsync())
                return Unauthorized("Пользователь не найден в AD");

            // Берем первую найденную запись
            var entry = await search.NextAsync();

            // Получаем группы пользователя
            var memberOf = entry.GetAttributeSet()["memberOf"];

            if (memberOf != null)
            {
                foreach (var groupDn in memberOf.StringValueArray)
                {
                    // Например:
                    // CN=ReportAdmins,OU=Groups,DC=mod,DC=com

                    var cn = groupDn.Split(',')[0]
                                    .Replace("CN=", "");

                    roles.Add(cn);
                }
            }

            // Для проверки выведем найденные группы
            foreach (var role in roles)
            {
                Console.WriteLine($"AD group: {role}");
            }

            foreach (var role in roles)
            {
                identity.AddClaim(OpenIddictConstants.Claims.Role, role);
            }
        }
        catch (Exception)
        {

            //throw;
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

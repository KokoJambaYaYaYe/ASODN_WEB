using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace AuthSystem.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class AuthWindowsController : ControllerBase
{
    private readonly IUserWindowsAuthService _userWindowsAuthService;

    public AuthWindowsController(IUserWindowsAuthService userWindowsAuthService) {
        _userWindowsAuthService = userWindowsAuthService;
    } 


    [HttpGet("negotiate")]
    [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)]
    public async Task<IActionResult> NegotiateLogin([FromQuery] string returnUrl = "/")
    {
        var windowsName = User.Identity?.Name;
        if (string.IsNullOrEmpty(windowsName)) return Unauthorized();

        // Синхронизируем с вашей локальной системой
        var internalUser = await _userWindowsAuthService.FindOrCreateWindowsUserAsync(windowsName);

        // Создаем Identity для OpenIddict
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        // Клеймы, которые запишутся в Redis
        // Пример явного указания мест назначения для клеймов
        // Конфликтов не будет, так как метод принадлежит самому identity
        identity.AddClaim(OpenIddictConstants.Claims.Subject, internalUser.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, internalUser.WindowsUsername);
        identity.AddClaim("display_name", internalUser.DisplayName);

        identity.AddClaim(new Claim("amr", "wia")); // "wia" — Windows Integrated Authentication

        foreach (var role in internalUser.Roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
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

        // Локальный редирект обратно на страницу авторизации OpenIddict, которая запрашивала вход
        return Redirect(returnUrl);
    }

}

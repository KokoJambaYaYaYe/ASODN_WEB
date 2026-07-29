using AuthCommon.Models.EntityModels.AuthModels;
using AuthCommon.Models.Models;
using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace AuthSystem.API.Controller;

[ApiController]
[Route("authsystem_api/[controller]")]
public class AuthCredentialsController : ControllerBase
{
    private readonly IUserWindowsAuthService _userWindowsAuthService;
    // Используем стандартные менеджеры ASP.NET Identity
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AuthCredentialsController(IUserWindowsAuthService userWindowsAuthService, SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _userWindowsAuthService = userWindowsAuthService;
        _signInManager = signInManager;
        _userManager = userManager;
    }


    [HttpPost("login")]
    [AllowAnonymous] // КРИТИЧНО: Отключаем Windows-аутентификацию конкретно для этого метода
    public async Task<IActionResult> PasswordLogin([FromBody] AuthLoginPassRequestModel model)
    {
        if (string.IsNullOrEmpty(model.Login) || string.IsNullOrEmpty(model.Password))
        {
            return BadRequest(new { error = "Логин и пароль обязательны для заполнения" });
        }

        // 1. Ищем пользователя по логину/email
        var user = await _userManager.FindByNameAsync(model.Login);
        if (user == null)
        {
            return BadRequest(new { error = "Неверный логин или пароль" });
        }

        // 2. Проверяем корректность пароля без автоматического входа
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = "Неверный логин или пароль" });
        }

        // 3. Получаем роли пользователя для токена
        var roles = await _userManager.GetRolesAsync(user);

        // 4. Создаем ClaimsIdentity, ПОЛНОСТЬЮ СОВМЕСТИМЫЙ С OPENIDDICT (как в Windows Auth)
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        // Наполняем клеймами, которые улетят в RedisTicketStore
        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, user.UserName);
        identity.AddClaim("display_name", user.FullName ?? user.UserName);

        identity.AddClaim(new Claim("amr", "pwd")); // "pwd" — стандартное OIDC обозначение для пароля

        foreach (var role in roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
        }

        var principal = new ClaimsPrincipal(identity);

        // Назначаем Scopes
        principal.SetScopes(new[] {
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles
        });

        // Назначаем Destinations (места назначения), чтобы OpenIddict знал, куда их писать
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Role => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            "display_name" => [OpenIddictConstants.Destinations.AccessToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });

        // 5. Записываем сессию в RedisTicketStore под схемой Identity (сработает ваш Singleton store)
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        // Возвращаем фронтенду URL для редиректа обратно на /connect/authorize
        return Ok(new { redirectUrl = model.ReturnUrl });
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace AuthSystem.BFF.API.Controller;

[ApiController]
//[Route("authsystem_api/[controller]")]
public class AuthLogoutController : ControllerBase
{
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;

    public AuthLogoutController(
        IOpenIddictTokenManager tokenManager,
        IOpenIddictAuthorizationManager authorizationManager)
    {
        _tokenManager = tokenManager;
        _authorizationManager = authorizationManager;
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout([FromQuery] string returnUrl = "/")
    {
        // ЯВНО просим ASP.NET Core прочитать нашу сессионную куку
        var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        // Если кука валидна и пользователь найден
        if (authResult.Succeeded && authResult.Principal != null)
        {
            var userPrincipal = authResult.Principal;

            // Извлекаем Subject ID пользователя (его уникальный идентификатор)
            var userId = userPrincipal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
                         ?? userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value; // На всякий случай проверяем стандартный claim

            if (!string.IsNullOrEmpty(userId))
            {
                // Находим и физически удаляем все активные токены этого пользователя из Redis
                var tokens = _tokenManager.FindBySubjectAsync(userId);
                await foreach (var token in tokens)
                {
                    await _tokenManager.DeleteAsync(token);
                }

                // Удаляем связанные авторизации
                var authorizations = _authorizationManager.FindBySubjectAsync(userId);
                await foreach (var auth in authorizations)
                {
                    await _authorizationManager.DeleteAsync(auth);
                }
            }
        }
        // 2. Стираем Cookie-сессию из браузера
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        // 3. Завершаем сессию в OpenIddict и возвращаем пользователя на React
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = returnUrl
            });
    }
}

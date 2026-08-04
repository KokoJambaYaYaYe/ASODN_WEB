using AuthCommon.Models.EntityModels.AuthModels;
using AuthCommon.Models.Models;
using AuthSystem.BFF.Service.Abstraction.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace AuthSystem.BFF.Service;

public class UserCredentialsAuthService: IUserCredentialsAuthService
{
    // Используем стандартные менеджеры ASP.NET Identity
    private readonly UserManager<User> _userManager;
    private readonly ILogger<UserCredentialsAuthService> _logger;


    public UserCredentialsAuthService(
        UserManager<User> userManager,
        ILogger<UserCredentialsAuthService> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _logger = logger;
    }



    public async Task<AuthResult> CredentialsAuthAsync(User user)
    {
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

        return new AuthResult
        {
            IsSuccess = true,
            Principal = principal,
        };

    }
}

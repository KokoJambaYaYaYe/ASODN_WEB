using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Principal;

namespace AuthSystem.API.Controller;

[ApiController]
[Route("authsystem_api/[controller]")]
public class AuthWindowsController : ControllerBase
{
    private readonly IUserWindowsAuthService _userWindowsAuthService;
    private readonly ILogger<AuthWindowsController> _logger;

    public AuthWindowsController(
        IUserWindowsAuthService userWindowsAuthService, 
        ILogger<AuthWindowsController> logger) 
    {
        _userWindowsAuthService = userWindowsAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Вход для пользователей Active Directory
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [Authorize(Policy = "WindowsAuthPolicy")]
    [HttpGet("negotiate")]
    public async Task<IActionResult> NegotiateLogin([FromQuery] string returnUrl = "/")
    {
        var windowsName = HttpContext.User.Identity?.Name;

        if (string.IsNullOrEmpty(windowsName))
            return Unauthorized();

        var authResult = await _userWindowsAuthService.WindowsAuthAsync(windowsName);

        if (authResult.IsSuccess)
        {
            // Вызываем стандартный SignInAsync для записи сессионной куки в браузер
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, authResult.Principal);

            _logger.LogInformation("AuthenticationType: {Type}", authResult.Principal.Identity?.AuthenticationType);
            _logger.LogInformation("IsAuthenticated: {Auth}", authResult.Principal.Identity?.IsAuthenticated);
            _logger.LogInformation("Name: {Name}", authResult.Principal.Identity?.Name);
            foreach (var claim in authResult.Principal.Claims)
            {
                _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
            }


            return new RedirectResult(returnUrl);
        }
        else
        {
            return BadRequest(new { error = "Ошибка при попытке авторизации" });
        }

    }

}

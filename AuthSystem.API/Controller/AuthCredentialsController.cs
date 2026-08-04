using AuthCommon.Models.EntityModels.AuthModels;
using AuthCommon.Models.Models;
using AuthSystem.BFF.Service.Abstraction.IService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthSystem.API.Controller;

[ApiController]
[Route("authsystem_api/[controller]")]
public class AuthCredentialsController : ControllerBase
{
    private IUserCredentialsAuthService _userCredentialsAuthService;

    // Используем стандартные менеджеры ASP.NET Identity
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AuthCredentialsController(IUserCredentialsAuthService userCredentialsAuthService, SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userCredentialsAuthService = userCredentialsAuthService;
    }


    [HttpPost("login")]
    [AllowAnonymous] // КРИТИЧНО
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


        var authResult = await _userCredentialsAuthService.CredentialsAuthAsync(user);

        if (authResult.IsSuccess)
        {
            // аписываем сессию в RedisTicketStore под схемой Identity (сработает ваш Singleton store)
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, authResult.Principal);

            // Возвращаем фронтенду URL для редиректа обратно на /connect/authorize
            return Ok(new { redirectUrl = model.ReturnUrl });
        }
        else
        {
            return BadRequest(new { error = "Ошибка при попытке авторизации" });
        }

    }
}

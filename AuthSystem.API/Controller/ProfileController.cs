using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthSystem.BFF.API.Controller;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Метод защищен, кука обязательна
public class ProfileController: ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetCurrentUserInfo()
    {
        // Читаем значение клейма "amr"
        var authMethod = User.FindFirstValue("amr");

        if (authMethod == "wia")
        {
            return Ok(new { loginMethod = "Windows (Negotiate)", user = User.Identity?.Name });
        }

        if (authMethod == "pwd")
        {
            return Ok(new { loginMethod = "Логин и Пароль", user = User.Identity?.Name });
        }

        return Ok(new { loginMethod = "Неизвестно", user = User.Identity?.Name });
    }
}

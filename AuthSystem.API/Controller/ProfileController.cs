using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthSystem.BFF.API.Controller;

[ApiController]
[Route("authsystem_api/[controller]")]
[Authorize] // Метод защищен, кука обязательна
public class ProfileController: ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetCurrentUserInfo()
    {
        // 1. Получаем все роли и объединяем их в строку через запятую
        var roles = string.Join(", ", User.Claims
                            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                            .Select(c => c.Value));

        // Читаем значение клейма "amr"
        var authMethod = User.FindFirstValue("amr");

        // Формируем базовое название метода авторизации
        string loginMethod = authMethod switch
        {
            "wia" => "Windows (Negotiate)",
            "pwd" => "Логин и Пароль",
            _ => "Неизвестно"
        };

        // Возвращаем результат вместе со списком ролей
        return Ok(new
        {
            loginMethod = loginMethod,
            user = User.Identity?.Name,
            roles = roles // Добавили переменную в ответ
        });
    }
}

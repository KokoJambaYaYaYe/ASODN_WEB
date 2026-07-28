using AuthCommon.Models.Models;
using AuthCommon.Models.EntityModels.AuthModels;
using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Identity;

namespace AuthSystem.Service.Service;

public class UserWindowsAuthService : IUserWindowsAuthService
{
    // Используем стандартные менеджеры ASP.NET Identity
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public UserWindowsAuthService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }







    public Task<UserWindowsAuthModel?> FindByIdAsync(Guid userId)
    {
        throw new NotImplementedException();
    }

    public async Task<UserWindowsAuthModel> FindOrCreateWindowsUserAsync(string windowsUsername)
    {
        var userName = ExtractWindowsUserName(windowsUsername);

        // 1. Ищем пользователя в ASP.NET Identity по UserName
        var user = await _userManager.FindByNameAsync(userName);

        // 2. Если пользователя нет — регистрируем его в системе
        if (user == null)
        {
            var email = ExtractEmailPlaceholder(userName);

            user = new User
            {
                UserName = userName,
                Email = email, // Identity часто требует Email
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new Exception($"Не удалось создать Windows-пользователя: {errors}");
            }

            // Опционально: Добавляем дефолтную роль "User", если она создана в системе
            const string defaultRole = "User";
            if (await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _userManager.AddToRoleAsync(user, defaultRole);
            }
        }

        // 3. Получаем список ролей пользователя из ASP.NET Identity
        var roles = await _userManager.GetRolesAsync(user);

        // 4. Возвращаем заполненную доменную модель для вашего контроллера
        return new UserWindowsAuthModel
        {
            // Приводим string из IdentityUser.Id к типу Guid (или оставьте string, если у вас модель на string)
            Id = user.Id,
            WindowsUsername = user.UserName ?? windowsUsername,
            // Так как в стандартном IdentityUser нет поля DisplayName, 
            // используем очищенный UserName (или берите из AD, как в примере выше)
            DisplayName = ExtractDisplayName(windowsUsername),
            Roles = roles.ToList()
        };
    }

    private string ExtractWindowsUserName(string windowsUsername)
    {
        if (string.IsNullOrEmpty(windowsUsername))
            return "Unknown";

        // Разделяем строку по символу обратного слэша
        var parts = windowsUsername.Split('\\');

        // Если слэш был найден, берем последний элемент (индекс 1 или последний в массиве)
        // Для "DESKTOP-1EAMEAL\Lenovo" вернет "Lenovo"
        return parts.Length > 1 ? parts[^1] : parts[0];
    }

    private string ExtractDisplayName(string windowsUsername)
    {
        if (string.IsNullOrEmpty(windowsUsername)) return "Unknown";
        var parts = windowsUsername.Split('\\');
        return parts.Length > 1 ? parts[1] : parts[0];
    }

    private string ExtractEmailPlaceholder(string windowsUsername)
    {
        // Генерируем временный email, если поле обязательно в вашей конфигурации Identity
        var cleanName = ExtractDisplayName(windowsUsername).Replace(" ", ".");
        return $"{cleanName}@local.domain";
    }

    public Task<bool> IsUserActiveAsync(Guid userId)
    {
        throw new NotImplementedException();
    }
}

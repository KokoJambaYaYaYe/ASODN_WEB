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

    public async Task<UserWindowsAuthModel> FindOrCreateWindowsUserAsync(string windowsIdentity)
    {
        var identity = ParseWindowsIdentity(windowsIdentity);

        var user = await _userManager.FindByNameAsync(identity.UserName);

        if (user == null)
        {
            user = new User
            {
                UserName = identity.UserName,
                Email = $"{identity.UserName}@local.domain",
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new Exception($"Не удалось создать Windows-пользователя: {errors}");
            }

            const string defaultRole = "User";

            if (await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _userManager.AddToRoleAsync(user, defaultRole);
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserWindowsAuthModel
        {
            Id = user.Id,
            UserName = identity.UserName,
            WindowsIdentity = identity.FullIdentity,
            Domain = identity.Domain,
            DisplayName = identity.UserName,
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

    private static WindowsIdentityInfo ParseWindowsIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("Windows identity is empty.", nameof(identity));

        identity = identity.Trim();

        // DOMAIN\User
        if (identity.Contains('\\'))
        {
            var parts = identity.Split('\\', 2);

            return new WindowsIdentityInfo
            {
                UserName = parts[1],
                Domain = parts[0],
                FullIdentity = identity
            };
        }

        // user@DOMAIN.COM
        if (identity.Contains('@'))
        {
            var parts = identity.Split('@', 2);

            return new WindowsIdentityInfo
            {
                UserName = parts[0],
                Domain = parts[1],
                FullIdentity = identity
            };
        }

        return new WindowsIdentityInfo
        {
            UserName = identity,
            Domain = string.Empty,
            FullIdentity = identity
        };
    }
}

using AuthCommon.Models.Models;

namespace AuthSystem.Service.Abstraction.IService;

/// <summary>
/// Контракт для работы с локальными пользователями при Windows-аутентификации
/// </summary>
public interface IUserWindowsAuthService
{
    /// <summary>
    /// Ищет пользователя по Windows-логину. Если его нет — создает в локальной БД.
    /// </summary>
    /// <param name="windowsUsername">Имя пользователя из User.Identity.Name (например, "DOMAIN\username")</param>
    Task<UserWindowsAuthModel> FindOrCreateWindowsUserAsync(string windowsUsername);

    /// <summary>
    /// Получить пользователя по его внутреннему идентификатору (полезно при валидации токенов)
    /// </summary>
    Task<UserWindowsAuthModel?> FindByIdAsync(Guid userId);

    /// <summary>
    /// Проверить, активен ли пользователь и имеет ли доступ к системе
    /// </summary>
    Task<bool> IsUserActiveAsync(Guid userId);
}

using AuthCommon.Models.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthSystem.Service.Abstraction.IService;

/// <summary>
/// Контракт для работы с локальными пользователями при Windows-аутентификации
/// </summary>
public interface IUserWindowsAuthService
{
    Task<AuthResult> WindowsAuthAsync(string windowsName);
}

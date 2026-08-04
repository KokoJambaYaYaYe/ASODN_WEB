using AuthCommon.Models.EntityModels.AuthModels;
using AuthCommon.Models.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthSystem.BFF.Service.Abstraction.IService;

public interface IUserCredentialsAuthService
{
    Task<AuthResult> CredentialsAuthAsync(User user);
}

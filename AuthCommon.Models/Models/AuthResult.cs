using System.Security.Claims;

namespace AuthCommon.Models.Models;

public class AuthResult
{
    public bool IsSuccess { get; set; }
    public ClaimsPrincipal Principal { get; set; }
    public string ErrorMessage { get; set; }
    public string Token { get; set; } // Например, если возвращаете JWT
}

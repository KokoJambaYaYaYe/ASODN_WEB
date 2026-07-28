using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class RoleClaim : IdentityRoleClaim<long>
{
    public Role Role { get; set; } = null!;
}

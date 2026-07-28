using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class Role : IdentityRole<long>
{
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RoleClaim> RoleClaims { get; set; } = new List<RoleClaim>();
}

using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class UserRole : IdentityUserRole<long>
{
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

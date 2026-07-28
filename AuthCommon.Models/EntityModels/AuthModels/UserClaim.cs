using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class UserClaim : IdentityUserClaim<long>
{
    public User User { get; set; } = null!;
}

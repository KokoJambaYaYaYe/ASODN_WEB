using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class UserToken : IdentityUserToken<long>
{
    public User User { get; set; } = null!;
}

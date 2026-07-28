using Microsoft.AspNetCore.Identity;

namespace AuthCommon.Models.EntityModels.AuthModels;

public class UserLogin : IdentityUserLogin<long>
{
    public User User { get; set; } = null!;
}

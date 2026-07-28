namespace AuthCommon.Models.Models;

public class AuthLoginPassRequestModel
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/";
}

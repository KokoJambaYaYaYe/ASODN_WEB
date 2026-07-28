namespace AuthCommon.Models.Models;

public class UserWindowsAuthModel
{
    public long Id { get; set; }
    public string WindowsUsername { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
}

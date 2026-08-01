namespace AuthCommon.Models.Models;

public class UserWindowsAuthModel
{
    public long Id { get; set; }

    /// <summary>
    /// Логин пользователя (например: lenovo)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Полная Kerberos/Windows идентичность
    /// Например:
    /// MOD\lenovo
    /// или
    /// lenovo@MOD.COM
    /// </summary>
    public string WindowsIdentity { get; set; } = string.Empty;

    /// <summary>
    /// Домен
    /// Например MOD.COM
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

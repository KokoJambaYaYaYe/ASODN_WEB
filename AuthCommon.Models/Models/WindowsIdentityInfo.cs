using System;
using System.Collections.Generic;
using System.Text;

namespace AuthCommon.Models.Models;

 /// <summary>
 /// Класс данных Windows учетной записи пользователя
 /// </summary>
public sealed class WindowsIdentityInfo
{
    public string UserName { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string FullIdentity { get; init; } = string.Empty;
}

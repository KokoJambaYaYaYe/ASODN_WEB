using AuthCommon.Models.EntityModels.AuthModels;
using AuthCommon.Models.Models;
using AuthSystem.Service.Abstraction.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using OpenIddict.Abstractions;
using System.Security.Claims;


namespace AuthSystem.BFF.Service;

public class UserWindowsAuthService : IUserWindowsAuthService
{
    // Используем стандартные менеджеры ASP.NET Identity
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserWindowsAuthService> _logger;


    public UserWindowsAuthService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<UserWindowsAuthService> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }


    #region Private
    /// <summary>
    /// Метод получения данных учетной записи пользователя
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static WindowsIdentityInfo ParseWindowsIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("Windows identity is empty.", nameof(identity));

        identity = identity.Trim();

        // DOMAIN\User
        if (identity.Contains('\\'))
        {
            var parts = identity.Split('\\', 2);

            return new WindowsIdentityInfo
            {
                UserName = parts[1],
                Domain = parts[0],
                FullIdentity = identity
            };
        }

        // user@DOMAIN.COM
        if (identity.Contains('@'))
        {
            var parts = identity.Split('@', 2);

            return new WindowsIdentityInfo
            {
                UserName = parts[0],
                Domain = parts[1],
                FullIdentity = identity
            };
        }

        return new WindowsIdentityInfo
        {
            UserName = identity,
            Domain = string.Empty,
            FullIdentity = identity
        };
    }

    /// <summary>
    /// Ищет пользователя по Windows-логину. Если его нет — создает в локальной БД.
    /// </summary>
    /// <param name="windowsUsername">Имя пользователя из User.Identity.Name (например, "DOMAIN\username")</param>
    private async Task<UserWindowsAuthModel> FindOrCreateWindowsUserAsync(string windowsIdentity)
    {
        var identity = ParseWindowsIdentity(windowsIdentity);

        var user = await _userManager.FindByNameAsync(identity.UserName);

        if (user == null)
        {
            user = new User
            {
                UserName = identity.UserName,
                Email = $"{identity.UserName}@local.domain",
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new Exception($"Не удалось создать Windows-пользователя: {errors}");
            }

            const string defaultRole = "User";

            if (await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _userManager.AddToRoleAsync(user, defaultRole);
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserWindowsAuthModel
        {
            Id = user.Id,
            UserName = identity.UserName,
            WindowsIdentity = identity.FullIdentity,
            Domain = identity.Domain,
            DisplayName = identity.UserName,
            Roles = roles.ToList()
        };
    }

    /// <summary>
    /// Дополняем identity пользователя данными из Active Directory
    /// </summary>
    /// <param name="windowsName"></param>
    /// <param name="identity"></param>
    /// <returns></returns>
    private async Task AddUserActiveDirectoryDataAsync(string windowsName, ClaimsIdentity identity)
    {
        /*
         SSL сертификат чтобы достучаться по закрытому порту 636
         */
        try
        {
            // Корректно вырезаем чистый логин из форматов DOMAIN\user и user@DOMAIN.COM
            string samAccountName = windowsName;

            if (samAccountName.Contains('\\'))
            {
                samAccountName = samAccountName.Split('\\')[1];
            }
            else if (samAccountName.Contains('@'))
            {
                samAccountName = samAccountName.Split('@')[0]; // Для lenovo@MOD.COM возьмет просто lenovo
            }

            var roles = new List<string>();

            using var connection = new LdapConnection();
            connection.SecureSocketLayer = true;

            string controllerActiveDirectory = _configuration["ActiveDirectoryConnectionParams:DomainController"];
            // Подключаемся к Samba AD
            await connection.ConnectAsync(controllerActiveDirectory, 636);

            string login = _configuration["ActiveDirectoryConnectionParams:DC_User"];
            string pswd = _configuration["ActiveDirectoryConnectionParams:DC_User_Pswd"];
            string domainParams = _configuration["ActiveDirectoryConnectionParams:DC_Params"];
            await connection.BindAsync($"CN={login},CN=Users,{domainParams}", $"{pswd}");

            // Ищем пользователя в каталоге
            var search = await connection.SearchAsync(
                $"{domainParams}",
                LdapConnection.ScopeSub,
                $"(sAMAccountName={samAccountName})", // Теперь здесь будет чистый "username"
                new[] { "memberOf", "displayName", "mail" },
                false
            );

            if (!await search.HasMoreAsync())
            {
                _logger.LogWarning("Пользователь {User} не найден в AD", samAccountName);
                //return new UnauthorizedObjectResult("Пользователь не найден в AD");
            }

            var entry = await search.NextAsync();
            var memberOf = entry.GetAttributeSet()["memberOf"];

            if (memberOf != null)
            {
                foreach (var groupDn in memberOf.StringValueArray)
                {
                    // Получаем CN группы (поддерживаем любой регистр "CN=" или "cn=")
                    var cn = groupDn.Split(',')[0]
                                    .Replace("CN=", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("cn=", "", StringComparison.OrdinalIgnoreCase);
                    roles.Add(cn);
                }
            }

            // Выводим найденные группы в Serilog (Information), чтобы они гарантированно попали в логи
            foreach (var role in roles)
            {
                _logger.LogInformation("Успешно найдена AD группа пользователя: {Role}", role);
            }

            foreach (var role in roles)
            {
                identity.AddClaim(OpenIddictConstants.Claims.Role, role);
            }
        }
        catch (Exception ex)
        {
            // Обязательно пишите ex.Message в лог, чтобы видеть РЕАЛЬНУЮ причину падения в catch
            _logger.LogError(ex, "ОШИБКА ПОДКЛЮЧЕНИЯ ИЛИ ПОИСКА В AD!");
        }
    }
    #endregion Private


    #region Methods
    /// <summary>
    /// Метод авторизации пользователя через Windows доменную учетку
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    public async Task<AuthResult> WindowsAuthAsync(string windowsName)
    {
        // Синхронизируем с вашей локальной системой
        var internalUser = await FindOrCreateWindowsUserAsync(windowsName);

        // Создаем Identity для OpenIddict
        var identity = new ClaimsIdentity(
            IdentityConstants.ApplicationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        // Клеймы, которые запишутся в Redis
        // Пример явного указания мест назначения для клеймов
        // Конфликтов не будет, так как метод принадлежит самому identity
        identity.AddClaim(OpenIddictConstants.Claims.Subject, internalUser.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, internalUser.WindowsIdentity);
        identity.AddClaim("display_name", internalUser.DisplayName);

        identity.AddClaim(new Claim("amr", "wia")); // "wia" — Windows Integrated Authentication

        foreach (var role in internalUser.Roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
        }


        await AddUserActiveDirectoryDataAsync(windowsName, identity);


        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(new[] {
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles
        });

        // Назначаем дестинации через хелпер OpenIddict
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Role => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            "display_name" => [OpenIddictConstants.Destinations.AccessToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });

        return new AuthResult
        {
            IsSuccess = true,
            Principal = principal,
        };

    }
    #endregion Methods

}

using System.Reflection;
using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace AuthCommon.Database.Infrastructure;

/// <summary>
/// Контекст базы данных системы авторизации.
/// Наследуется от IdentityDbContext с полной спецификацией типов для поддержки ключа типа long.
/// </summary>
public class AuthSystemDataBaseDBContext
    : IdentityDbContext<
        User, // 1. TUser
        Role, // 2. TRole
        long, // 3. TKey
        UserClaim, // 4. TUserClaim
        UserRole, // 5. TUserRole
        UserLogin, // 6. TUserLogin
        RoleClaim, // 7. TRoleClaim
        UserToken // 8. TUserToken
    >
{
    public AuthSystemDataBaseDBContext(DbContextOptions<AuthSystemDataBaseDBContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity (dbo)

        // 1. Регистрируем стандартные сущности OpenIddict
        modelBuilder.UseOpenIddict<long>();

        // 2. Явно переносим таблицы OpenIddict в отдельную схему "OIDC"
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreApplication<long>>().ToTable("Applications", "OIDC");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreAuthorization<long>>().ToTable("Authorizations", "OIDC");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreScope<long>>().ToTable("Scopes", "OIDC");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreToken<long>>().ToTable("Tokens", "OIDC");

        // 3. Остальные ваши конфигурации
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

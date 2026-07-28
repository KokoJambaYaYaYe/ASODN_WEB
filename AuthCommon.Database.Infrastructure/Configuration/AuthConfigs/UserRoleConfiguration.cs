using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        // Таблица связей User ↔ Role
        builder.ToTable("UserRoles", "Auth");

        // Композитный ключ
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        // Создаем связь между Admin User (Id=1) и Admin Role (Id=1)
        builder.HasData(new UserRole { UserId = 1, RoleId = 1 });
    }
}

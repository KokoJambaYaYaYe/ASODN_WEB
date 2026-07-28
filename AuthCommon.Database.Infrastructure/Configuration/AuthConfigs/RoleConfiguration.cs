using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Указываем таблицу в схеме Auth
        builder.ToTable("Roles", "Auth");

        builder.HasKey(x => x.Id);

        // Связь: Role → UserRoles (многие-ко-многим через промежуточную таблицу)
        builder
            .HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .IsRequired();

        // Связь: Role → RoleClaims (один-ко-многим)
        builder
            .HasMany(r => r.RoleClaims)
            .WithOne(c => c.Role)
            .HasForeignKey(c => c.RoleId)
            .IsRequired();

        // Данные роли по умолчанию
        var adminRole = new Role
        {
            Id = 1,
            Name = "SuperAdminRole",
            NormalizedName = "SUPERADMINROLE", // Обязательно CAPS для работы RoleManager
            // Статичный GUID предотвращает бесконечные миграции
            ConcurrencyStamp = "C4031674-70A5-4E7B-B433-2895E57B7F61",
        };

        builder.HasData(adminRole);
    }
}

using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    public void Configure(EntityTypeBuilder<RoleClaim> builder)
    {
        // 1. Указываем таблицу и схему
        builder.ToTable("RoleClaims", "Auth");

        builder.HasKey(x => x.Id);

        // 2. Настраиваем связь: RoleClaim принадлежит одной Role
        builder
            .HasOne(rc => rc.Role)
            .WithMany(r => r.RoleClaims)
            .HasForeignKey(rc => rc.RoleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade); // При удалении роли удаляются и её права

        // 3. Добавляем права (Claims) для роли SuperAdmin (Id = 1)
        builder.HasData(
            new RoleClaim
            {
                Id = 1, // Обязательно указываем Id для HasData
                RoleId = 1,
                ClaimType = "IsSuperAdmin",
                ClaimValue = "true",
            }
        );
    }
}

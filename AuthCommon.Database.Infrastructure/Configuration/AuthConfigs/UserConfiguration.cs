using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Таблица пользователей в схеме auth
        builder.ToTable("Users", "Auth");

        builder.HasKey(x => x.Id);

        // Ограничения на длину строк
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);

        // Значение по умолчанию для бизнес-флагов
        builder.Property(u => u.IsBlocked).HasDefaultValue(false);

        // Автоматическая установка даты создания
        builder.Property(u => u.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Связь: User → UserRoles (многие ко многим через таблицу UserRoles)
        builder
            .HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();

        // Связь: User → Claims (один ко многим)
        builder.HasMany(u => u.UserClaims).WithOne(c => c.User).HasForeignKey(c => c.UserId);

        // Связь: User → Logins (один ко многим)
        builder.HasMany(u => u.UserLogins).WithOne(l => l.User).HasForeignKey(l => l.UserId);

        // Связь: User → Tokens (один ко многим)
        builder.HasMany(u => u.UserTokens).WithOne(t => t.User).HasForeignKey(t => t.UserId);

        var adminUser = new User
        {
            Id = 1, // Так как используется long
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@example.com",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Admin",
            IsBlocked = false,
            // ИСПОЛЬЗУЙТЕ СТАТИЧЕСКИЙ GUID
            SecurityStamp = "B4031674-70A5-4E7B-B433-2895E57B7F61",
            ConcurrencyStamp = "D0C07028-090F-44A7-B747-9752981997F4",
            CreatedAtUtc = new DateTime(0001, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            //Password - $$$Developer$$$12345
            PasswordHash =
                "AQAAAAIAAYagAAAAEL8nBdV8h1Z9nLYzFw44v9tYIDqd7jmUHckgkf5lgs0SA78emXgGu6CvPRXd19Fw8w==",
        };

        builder.HasData(adminUser);
    }
}

using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        // Таблица внешних логинов
        builder.ToTable("UserLogins", "Auth");

        // Композитный ключ
        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });
    }
}

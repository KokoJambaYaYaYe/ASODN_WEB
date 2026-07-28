using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        // Таблица токенов пользователя
        builder.ToTable("UserTokens", "Auth");

        // Композитный ключ
        builder.HasKey(t => new
        {
            t.UserId,
            t.LoginProvider,
            t.Name,
        });
    }
}

using AuthCommon.Models.EntityModels.AuthModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthCommon.Database.Infrastructure.Configuration.AuthConfigs;

public class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> builder)
    {
        // Таблица клеймов пользователя
        builder.ToTable("UserClaims", "Auth");

        builder.HasKey(x => x.Id);
    }
}

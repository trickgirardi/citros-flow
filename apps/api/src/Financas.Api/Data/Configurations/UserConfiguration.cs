using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Email).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // E-mail é gravado já normalizado: o check garante isso e o índice único
        // passa a ser, na prática, case-insensitive.
        b.HasIndex(x => x.Email).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("ck_users_email_normalized", "email = lower(btrim(email))"));
    }
}

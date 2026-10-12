using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Role).IsRequired();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);

        // NULLS NOT DISTINCT: impede papel global duplicado (board_id nulo) para o mesmo usuário.
        b.HasIndex(x => new { x.UserId, x.BoardId }).IsUnique().AreNullsDistinct(false);
        b.HasIndex(x => x.BoardId);

        b.ToTable(t => t.HasCheckConstraint("ck_user_roles_role", "role in ('admin', 'tesoureiro', 'viewer')"));
    }
}

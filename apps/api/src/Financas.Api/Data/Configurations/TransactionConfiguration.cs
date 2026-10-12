using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.Category).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);

        // Mês e saldo anterior filtram por board + data; sugestões usam categoria e descrição.
        b.HasIndex(x => new { x.BoardId, x.Date });
        b.HasIndex(x => new { x.BoardId, x.Category });
        b.HasIndex(x => new { x.BoardId, x.Description });
        b.HasIndex(x => x.CreatedBy);
        b.HasIndex(x => x.UpdatedBy);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_transactions_type", "type in ('entrada', 'saida')");
            t.HasCheckConstraint("ck_transactions_amount", "amount > 0");
        });
    }
}

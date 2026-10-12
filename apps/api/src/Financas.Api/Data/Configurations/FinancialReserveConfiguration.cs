using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class FinancialReserveConfiguration : IEntityTypeConfiguration<FinancialReserve>
{
    public void Configure(EntityTypeBuilder<FinancialReserve> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.BoardId);
        b.HasIndex(x => x.CreatedBy);

        b.ToTable(t => t.HasCheckConstraint("ck_financial_reserves_amount", "amount > 0"));
    }
}

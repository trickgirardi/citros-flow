using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> b)
    {
        b.ToTable("stock_items", t =>
        {
            t.HasCheckConstraint("ck_stock_items_status",
                "status in ('em_estoque', 'a_caminho', 'comprar', 'comprar_com_urgencia')");
            t.HasCheckConstraint("ck_stock_items_price", "price is null or price >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.Price).HasColumnType("numeric(12,2)");
        b.Property(x => x.Details).IsRequired().HasDefaultValue("");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BoardId, x.Status });
    }
}

using Financas.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Data.Configurations;

public class BoardTaskConfiguration : IEntityTypeConfiguration<BoardTask>
{
    public void Configure(EntityTypeBuilder<BoardTask> b)
    {
        b.ToTable("tasks", t =>
        {
            t.HasCheckConstraint("ck_tasks_status", "status in ('a_fazer', 'em_andamento', 'concluida')");
            t.HasCheckConstraint("ck_tasks_priority", "priority is null or priority in ('baixa', 'media', 'alta', 'urgente')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.Description).IsRequired().HasDefaultValue("");
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.Tags).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BoardId, x.Status, x.Position });
    }
}

public class TaskAssigneeConfiguration : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> b)
    {
        b.ToTable("task_assignees");
        b.HasKey(x => new { x.TaskId, x.UserId });

        b.HasOne<BoardTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

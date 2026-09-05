using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

internal static class EngineerNotesModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EngineerNoteEntity>(entity =>
        {
            entity.ToTable("EngineerNotes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.RecordedByKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RecordedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RecordedByRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(AddEngineerNote.MaximumLength).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.RecordedAtUtc, item.Id })
                .IsDescending(false, true, true);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

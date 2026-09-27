using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One saved e-mail template (FRD-17 E-mail templates): the body an
/// Administrator last saved for a purpose, its version, and who saved it and
/// when. A purpose with no row uses its built-in body at version 0.
/// </summary>
internal sealed class EmailTemplateEntity
{
    public required string Purpose { get; set; }
    public required string Body { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public required string UpdatedBy { get; set; }
    public required string OperationKey { get; set; }
}

internal static class EmailTemplateModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EmailTemplateEntity>(entity =>
        {
            entity.ToTable("EmailTemplates");
            entity.HasKey(item => item.Purpose);
            entity.Property(item => item.Purpose).HasMaxLength(64);
            entity.Property(item => item.Body).HasMaxLength(Pegasus.Core.Operations.EmailTemplates.MaximumBodyLength).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.UpdatedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
        });
    }
}

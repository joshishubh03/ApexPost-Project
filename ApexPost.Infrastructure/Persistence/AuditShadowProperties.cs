using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// Names and configuration of the shadow properties every audited table carries. They exist in the
/// EF model and the database, but not on the CLR row or domain types.
/// </summary>
public static class AuditShadowProperties
{
    public const string CreatedAtUtc = nameof(CreatedAtUtc);
    public const string CreatedBy = nameof(CreatedBy);
    public const string ModifiedAtUtc = nameof(ModifiedAtUtc);
    public const string ModifiedBy = nameof(ModifiedBy);
    public const string RowVersion = nameof(RowVersion);

    public static void ConfigureAuditColumns<TRow>(this EntityTypeBuilder<TRow> entity)
        where TRow : class, IAuditedRow
    {
        entity.Property<DateTime>(CreatedAtUtc)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property<Guid?>(CreatedBy);
        entity.Property<DateTime?>(ModifiedAtUtc).HasColumnType("datetime2(3)");
        entity.Property<Guid?>(ModifiedBy);
    }

    public static void ConfigureRowVersion<TRow>(this EntityTypeBuilder<TRow> entity)
        where TRow : class =>
        entity.Property<byte[]>(RowVersion).IsRowVersion().IsRequired();

    /// <summary>Records who created a staged row (the timestamp is filled by <see cref="AuditColumnsInterceptor"/>).</summary>
    public static void SetCreatedBy(this DbContext context, object row, Guid? staffId, DateTimeOffset? createdAt = null)
    {
        var entry = context.Entry(row);
        entry.Property(CreatedBy).CurrentValue = staffId == Guid.Empty ? null : staffId;
        if (createdAt is { } value)
        {
            entry.Property(CreatedAtUtc).CurrentValue = value.UtcDateTime;
        }
    }

    public static void SetModifiedBy(this DbContext context, object row, Guid? staffId) =>
        context.Entry(row).Property(ModifiedBy).CurrentValue = staffId == Guid.Empty ? null : staffId;
}

/// <summary>
/// Fills CreatedAtUtc / ModifiedAtUtc for every audited row at SaveChanges time, so repositories and the
/// domain never deal with audit timestamps.
/// </summary>
public sealed class AuditColumnsInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IAuditedRow>())
        {
            if (entry.State == EntityState.Added)
            {
                var created = entry.Property(AuditShadowProperties.CreatedAtUtc);
                if (created.CurrentValue is DateTime value && value == default)
                {
                    created.CurrentValue = now;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(AuditShadowProperties.ModifiedAtUtc).CurrentValue = now;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideLog.Domain.Analysis;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class MonthlyAnalysisConfiguration : IEntityTypeConfiguration<MonthlyAnalysis>
{
    public void Configure(EntityTypeBuilder<MonthlyAnalysis> builder)
    {
        builder.HasKey(analysis => analysis.Id);

        // Rider, month and language identify an analysis, and that is also the entire quota: a second
        // one for the same quadruple is what the service refuses, and the index is what makes the
        // refusal cheap to decide and impossible to race past (#187).
        builder
            .HasIndex(analysis => new { analysis.UserId, analysis.Year, analysis.Month, analysis.Language })
            .IsUnique();

        builder.Property(analysis => analysis.Language).HasMaxLength(32);
        builder.Property(analysis => analysis.Model).HasMaxLength(128);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Infrastructure.Data;

/// <summary>应用数据库上下文。</summary>
public sealed class SoftCopyrightDbContext(DbContextOptions<SoftCopyrightDbContext> options)
    : DbContext(options)
{
    public DbSet<ApplicationCase> Cases => Set<ApplicationCase>();

    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();

    public DbSet<Evidence> Evidences => Set<Evidence>();

    public DbSet<ProjectAnalysis> Analyses => Set<ProjectAnalysis>();

    public DbSet<ApplicantInfo> ApplicantInfos => Set<ApplicantInfo>();

    public DbSet<CodeSelection> CodeSelections => Set<CodeSelection>();

    public DbSet<CodeSelectionItem> CodeSelectionItems => Set<CodeSelectionItem>();

    public DbSet<ConfirmationRecord> Confirmations => Set<ConfirmationRecord>();

    public DbSet<GeneratedArtifact> Artifacts => Set<GeneratedArtifact>();

    public DbSet<BackgroundJob> Jobs => Set<BackgroundJob>();

    public DbSet<CaseDeletionAudit> DeletionAudits => Set<CaseDeletionAudit>();

    public DbSet<InventionDisclosure> InventionDisclosures => Set<InventionDisclosure>();

    public DbSet<PatentClaim> PatentClaims => Set<PatentClaim>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ApplicationCase>(e =>
        {
            e.ToTable("ApplicationCases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SoftwareFullName).HasMaxLength(200);
            e.Property(x => x.SoftwareVersion).HasMaxLength(100);
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.StatusMessage).HasMaxLength(1000);
            e.Property(x => x.FailureReason).HasMaxLength(2000);
            e.Property(x => x.UploadedFileName).HasMaxLength(260);
            e.Property(x => x.SourceRepositoryUrl).HasMaxLength(600);
            e.Property(x => x.SourceCommit).HasMaxLength(64);
            e.Property(x => x.MaterialKind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DataDirectory).HasMaxLength(600);
            e.Property(x => x.RulesVersion).HasMaxLength(50);
            e.Property(x => x.AnalyzerVersion).HasMaxLength(50);
            e.HasIndex(x => x.IsDeleted);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<ApplicationUser>(e =>
        {
            e.ToTable("ApplicationUsers");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(60).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();

            // 模型接入设置以密文保存，长度取决于 AES-GCM 输出（Base64）。
            e.Property(x => x.EncryptedAiBaseUrl).HasMaxLength(1000);
            e.Property(x => x.EncryptedAiModel).HasMaxLength(1000);
            e.Property(x => x.EncryptedAiApiKey).HasMaxLength(1000);

            e.HasIndex(x => x.UserName).IsUnique();
        });

        b.Entity<ProjectFile>(e =>
        {
            e.ToTable("ProjectFiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.RelativePath).HasMaxLength(600).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.DetectedEncoding).HasMaxLength(40);
            e.Property(x => x.DetectedLanguage).HasMaxLength(60);
            e.Property(x => x.ExclusionReason).HasMaxLength(300);
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SecretRisk).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.CaseId);
        });

        b.Entity<Evidence>(e =>
        {
            e.ToTable("Evidences");
            e.HasKey(x => x.Id);
            e.Property(x => x.RelativePath).HasMaxLength(600).IsRequired();
            e.Property(x => x.EvidenceType).HasMaxLength(60);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.Excerpt).HasMaxLength(2000);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.CaseId);
        });

        b.Entity<ProjectAnalysis>(e =>
        {
            e.ToTable("ProjectAnalyses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CaseId).IsUnique();
            e.Property(x => x.AnalyzerVersion).HasMaxLength(50);
            e.Property(x => x.RulesVersion).HasMaxLength(50);
        });

        b.Entity<ApplicantInfo>(e =>
        {
            e.ToTable("ApplicantInfos");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CaseId).IsUnique();
            e.Property(x => x.SoftwareFullName).HasMaxLength(200);
            e.Property(x => x.SoftwareAbbreviation).HasMaxLength(200);
            e.Property(x => x.VersionNumber).HasMaxLength(100);
            e.Property(x => x.CopyrightOwner).HasMaxLength(300);
            e.Property(x => x.DevelopmentMode).HasMaxLength(200);
            e.Property(x => x.RightsAcquisitionMethod).HasMaxLength(200);
            e.Property(x => x.CompletionDate).HasMaxLength(40);
            e.Property(x => x.FirstPublicationStatus).HasMaxLength(40);
            e.Property(x => x.FirstPublicationDate).HasMaxLength(40);
            e.Property(x => x.DevelopmentEnvironment).HasMaxLength(1000);
            e.Property(x => x.RuntimeEnvironment).HasMaxLength(1000);
            e.Property(x => x.ProgrammingLanguages).HasMaxLength(500);
            e.Property(x => x.SourceProgramAmount).HasMaxLength(200);
            e.Property(x => x.FunctionSummary).HasMaxLength(4000);
            e.Property(x => x.RulesVersion).HasMaxLength(50);
        });

        b.Entity<CodeSelection>(e =>
        {
            e.ToTable("CodeSelections");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CaseId).IsUnique();
            e.Property(x => x.ConfirmedHash).HasMaxLength(128);
        });

        b.Entity<CodeSelectionItem>(e =>
        {
            e.ToTable("CodeSelectionItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.RelativePath).HasMaxLength(600).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasIndex(x => new { x.CaseId, x.RelativePath }).IsUnique();
        });

        b.Entity<ConfirmationRecord>(e =>
        {
            e.ToTable("ConfirmationRecords");
            e.HasKey(x => x.Id);
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ConfirmedBy).HasMaxLength(100);
            e.Property(x => x.DataHash).HasMaxLength(128);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.InvalidatedReason).HasMaxLength(500);
            e.HasIndex(x => x.CaseId);
        });

        b.Entity<GeneratedArtifact>(e =>
        {
            e.ToTable("GeneratedArtifacts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.RelativePath).HasMaxLength(800);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.HasIndex(x => x.CaseId);
        });

        b.Entity<BackgroundJob>(e =>
        {
            e.ToTable("BackgroundJobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.ErrorMessage).HasMaxLength(4000);
            e.HasIndex(x => x.CaseId);
            e.HasIndex(x => new { x.Status, x.NextRunAt });
        });

        b.Entity<CaseDeletionAudit>(e =>
        {
            e.ToTable("CaseDeletionAudits");
            e.HasKey(x => x.Id);
            e.Property(x => x.CaseName).HasMaxLength(200);
        });

        b.Entity<InventionDisclosure>(e =>
        {
            e.ToTable("InventionDisclosures");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CaseId).IsUnique();
            e.Property(x => x.Title).HasMaxLength(300);
            // 长文本列必须用 TEXT：MySQL 单行上限 65535 字节，多个 varchar 累加会超限。
            e.Property(x => x.TechnicalField).HasColumnType("text");
            e.Property(x => x.BackgroundTechnology).HasColumnType("text");
            e.Property(x => x.ProblemStatement).HasColumnType("text");
            e.Property(x => x.TechnicalSolution).HasColumnType("text");
            e.Property(x => x.BeneficialEffects).HasColumnType("text");
            e.Property(x => x.DrawingDescription).HasColumnType("text");
            e.Property(x => x.PriorArt).HasColumnType("text");
            e.Property(x => x.PriorArtSources).HasColumnType("text");
            e.Property(x => x.Abstract).HasColumnType("longtext");
            e.Property(x => x.TerminologyJson).HasColumnType("longtext");
            e.Property(x => x.TechnicalFeaturesJson).HasColumnType("longtext");
            e.Property(x => x.EmbodimentsJson).HasColumnType("longtext");
            e.Property(x => x.ApplicantName).HasMaxLength(300);
            e.Property(x => x.InventorNames).HasMaxLength(600);
            e.Property(x => x.PatentType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RulesVersion).HasMaxLength(50);
        });

        b.Entity<PatentClaim>(e =>
        {
            e.ToTable("PatentClaims");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CaseId, x.Number }).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Text).HasColumnType("longtext");
            e.Property(x => x.FeaturesJson).HasColumnType("longtext");
        });

        b.Entity<ApplicationCase>()
            .HasMany(x => x.ProjectFiles).WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasMany(x => x.Evidences).WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasMany(x => x.Confirmations).WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasMany(x => x.Artifacts).WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasMany(x => x.Jobs).WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasOne<ProjectAnalysis>().WithOne(x => x.Case!).HasForeignKey<ProjectAnalysis>(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasOne<ApplicantInfo>().WithOne(x => x.Case!).HasForeignKey<ApplicantInfo>(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasOne<CodeSelection>().WithOne(x => x.Case!).HasForeignKey<CodeSelection>(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasOne<InventionDisclosure>().WithOne(x => x.Case!).HasForeignKey<InventionDisclosure>(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApplicationCase>()
            .HasMany<PatentClaim>().WithOne(x => x.Case!).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CodeSelection>()
            .HasMany(x => x.Items).WithOne(x => x.Selection!).HasForeignKey(x => x.SelectionId).OnDelete(DeleteBehavior.Cascade);

        ApplyDateTimeOffsetConverters(b);
    }

    /// <summary>
    /// SQLite 不支持按 DateTimeOffset 排序与比较，这里统一转换为 UTC DateTime 存储。
    /// </summary>
    private static void ApplyDateTimeOffsetConverters(ModelBuilder b)
    {
        var converter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.UtcDateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        var nullableConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
            value => value.HasValue ? value.Value.UtcDateTime : null,
            value => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null);

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableConverter);
                }
            }
        }
    }
}

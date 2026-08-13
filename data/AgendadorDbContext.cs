using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.Data;

public sealed class AgendadorDbContext(
    DbContextOptions<AgendadorDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyUser> FamilyUsers => Set<FamilyUser>();
    public DbSet<FamilySettings> FamilySettings => Set<FamilySettings>();
    public DbSet<TelegramSettings> TelegramSettings => Set<TelegramSettings>();
    public DbSet<ContaEntity> Contas => Set<ContaEntity>();
    public DbSet<PagamentoEntity> Pagamentos => Set<PagamentoEntity>();
    public DbSet<LembreteEnviadoEntity> LembretesEnviados => Set<LembreteEnviadoEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureFamilies(builder);
        ConfigureContas(builder);
        ConfigureNotifications(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("app_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("app_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("app_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("app_user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("app_role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("app_user_tokens");
    }

    private static void ConfigureFamilies(ModelBuilder builder)
    {
        builder.Entity<Family>(entity =>
        {
            entity.ToTable("families");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name);
        });

        builder.Entity<FamilyUser>(entity =>
        {
            entity.ToTable("family_users", table =>
                table.HasCheckConstraint("ck_family_users_role", "\"Role\" IN ('Owner', 'Admin', 'Member')"));
            entity.HasKey(x => new { x.FamilyId, x.UserId });
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IsActive });
            entity.HasOne(x => x.Family).WithMany(x => x.Users).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany(x => x.Families).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FamilySettings>(entity =>
        {
            entity.ToTable("family_settings", table =>
            {
                table.HasCheckConstraint("ck_family_settings_hour", "\"ReminderHour\" BETWEEN 0 AND 23");
                table.HasCheckConstraint("ck_family_settings_minute", "\"ReminderMinute\" BETWEEN 0 AND 59");
            });
            entity.HasKey(x => x.FamilyId);
            entity.Property(x => x.DefaultCurrency).HasConversion<string>().HasMaxLength(3).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Family).WithOne(x => x.Settings).HasForeignKey<FamilySettings>(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TelegramSettings>(entity =>
        {
            entity.ToTable("telegram_settings");
            entity.HasKey(x => x.FamilyId);
            entity.Property(x => x.ChatId).HasMaxLength(100);
            entity.Property(x => x.BotTokenSecretReference).HasMaxLength(200);
            entity.HasOne(x => x.Family).WithOne(x => x.TelegramSettings).HasForeignKey<TelegramSettings>(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureContas(ModelBuilder builder)
    {
        builder.Entity<ContaEntity>(entity =>
        {
            entity.ToTable("contas", table =>
            {
                table.HasCheckConstraint("ck_contas_valor", "\"Valor\" > 0");
                table.HasCheckConstraint("ck_contas_dia_vencimento", "\"DiaVencimento\" BETWEEN 1 AND 31");
                table.HasCheckConstraint("ck_contas_duracao", "\"DuracaoMeses\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.FamilyId, x.Id });
            entity.Property(x => x.Nome).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Valor).HasPrecision(18, 2);
            entity.Property(x => x.Country).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
            entity.Property(x => x.Observacoes).HasMaxLength(300);
            entity.HasIndex(x => new { x.FamilyId, x.Ativa, x.DiaVencimento });
            entity.HasOne(x => x.Family).WithMany(x => x.Contas).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PagamentoEntity>(entity =>
        {
            entity.ToTable("pagamentos", table =>
            {
                table.HasCheckConstraint("ck_pagamentos_ano", "\"Ano\" BETWEEN 1 AND 9999");
                table.HasCheckConstraint("ck_pagamentos_mes", "\"Mes\" BETWEEN 1 AND 12");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FamilyId, x.ContaId, x.Ano, x.Mes }).IsUnique();
            entity.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Conta).WithMany(x => x.Pagamentos)
                .HasForeignKey(x => new { x.FamilyId, x.ContaId })
                .HasPrincipalKey(x => new { x.FamilyId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotifications(ModelBuilder builder)
    {
        builder.Entity<LembreteEnviadoEntity>(entity =>
        {
            entity.ToTable("lembretes_enviados");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.FamilyId, x.LocalDate, x.Channel }).IsUnique();
            entity.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

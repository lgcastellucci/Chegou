using Chegou.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chegou.Infrastructure;

public class ChegouDbContext(DbContextOptions<ChegouDbContext> options) : DbContext(options)
{
    public DbSet<Condominio> Condominios => Set<Condominio>();
    public DbSet<Unidade> Unidades => Set<Unidade>();
    public DbSet<Morador> Moradores => Set<Morador>();
    public DbSet<MoradorApelido> MoradorApelidos => Set<MoradorApelido>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Encomenda> Encomendas => Set<Encomenda>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Condominio>().ToTable("Condominio");

        b.Entity<Unidade>(e =>
        {
            e.ToTable("Unidade");
            e.HasIndex(x => new { x.CondominioId, x.BlocoNorm, x.NumeroNorm }).IsUnique();
        });

        b.Entity<Morador>(e =>
        {
            e.ToTable("Morador");
            e.HasMany(x => x.Apelidos).WithOne().HasForeignKey(a => a.MoradorId);
        });

        b.Entity<MoradorApelido>(e =>
        {
            e.ToTable("MoradorApelido");
            e.HasIndex(x => new { x.MoradorId, x.ApelidoNorm }).IsUnique();
        });

        b.Entity<Usuario>(e =>
        {
            e.ToTable("Usuario");
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Encomenda>(e =>
        {
            e.ToTable("Encomenda");
            // grava como RECEBIDA / NOTIFICADA / ... (igual ao db/schema-encomendas.sql)
            e.Property(x => x.Status)
                .HasConversion(v => v.ToString().ToUpperInvariant(),
                               s => Enum.Parse<StatusEncomenda>(s, true))
                .HasMaxLength(20);
            e.Property(x => x.Confianca).HasPrecision(3, 2);
            e.Property(x => x.ScoreMatch).HasPrecision(3, 2);
            e.Property(x => x.CodigoRetirada).HasMaxLength(6).IsFixedLength();
            e.HasOne(x => x.Unidade).WithMany().HasForeignKey(x => x.UnidadeId);
            e.HasOne(x => x.Morador).WithMany().HasForeignKey(x => x.MoradorId);
            e.HasIndex(x => new { x.CondominioId, x.Status, x.RecebidaEm });
            e.HasIndex(x => new { x.CondominioId, x.Rastreio });
        });
    }
}

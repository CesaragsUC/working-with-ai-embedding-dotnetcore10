using Microsoft.EntityFrameworkCore;

namespace Demo.Embedding.Web;

public class AppEmbeddingDbContext : DbContext
{
    public AppEmbeddingDbContext(DbContextOptions<AppEmbeddingDbContext> options) : base(options)
    {}

    public DbSet<Clubes> Clubes { get; set; }
    public DbSet<SobreClubes> SobreClubes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Clubes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.ToTable("clubes");
        });

        modelBuilder.Entity<SobreClubes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("Embedding").IsRequired().HasColumnType("vector(1024)");
            entity.ToTable("sobreclubes");

        });

        modelBuilder.HasPostgresExtension("vector");
    }
}


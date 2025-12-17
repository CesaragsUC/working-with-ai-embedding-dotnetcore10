using Microsoft.EntityFrameworkCore;

namespace Demo.Embedding.Web;

public class AppEmbeddingDbContext : DbContext
{
    public AppEmbeddingDbContext(DbContextOptions<AppEmbeddingDbContext> options) : base(options)
    {}

    public DbSet<Clubes> Clubes { get; set; }
    public DbSet<SobreClubes> SobreClubes { get; set; }
    public DbSet<Products> Products { get; set; }
    public DbSet<ProductsRecomendation> ProductsRecomendation { get; set; }
    public DbSet<MyDemoDocumentRecomendation> MyDemoDocumentRecomendation { get; set; }
    public DbSet<MyDemoDocument> MyDemoDocuments { get; set; }

    public DbSet<ProductSearchResult> ProductSearchResults { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Clubes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Country).HasColumnName("Country").IsRequired();
            entity.Property(e => e.State).HasColumnName("State").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.ToTable("clubes");
        });

        modelBuilder.Entity<SobreClubes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Country).HasColumnName("Country").IsRequired();
            entity.Property(e => e.State).HasColumnName("State").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("Embedding").IsRequired().HasColumnType("vector(1024)");
            entity.ToTable("sobreclubes");

        });

        modelBuilder.Entity<Products>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Category).HasColumnName("Category").IsRequired();
            entity.Property(e => e.Price).HasColumnName("Price").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.ToTable("products");
        });

        modelBuilder.Entity<ProductsRecomendation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired();
            entity.Property(e => e.Category).HasColumnName("Category").IsRequired();
            entity.Property(e => e.Price).HasColumnName("Price").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("Embedding").IsRequired().HasColumnType("vector(768)");
            entity.Property(e => e.EmbeddingLLM).HasColumnName("EmbeddingLLM").IsRequired().HasColumnType("vector(1024)");
            entity.ToTable("product_recomendation");
        });

        modelBuilder.Entity<MyDemoDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Title).HasColumnName("Title").IsRequired();
            entity.Property(e => e.DocText).HasColumnName("DocText").IsRequired();
            entity.ToTable("mydocuments");
        });

        modelBuilder.Entity<MyDemoDocumentRecomendation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").HasColumnType("uuid");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Title).HasColumnName("Title").IsRequired();
            entity.Property(e => e.DocText).HasColumnName("DocText").IsRequired();
            entity.Property(e => e.DocumentId).HasColumnName("DocumentId").HasColumnType("uuid").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("Embedding").IsRequired().HasColumnType("vector(768)");
            entity.ToTable("documents_recomendation");
        });

        modelBuilder.Entity<ProductSearchResult>().HasNoKey(); // usado para consultas sem chave primaria


        modelBuilder.HasPostgresExtension("vector");
    }
}


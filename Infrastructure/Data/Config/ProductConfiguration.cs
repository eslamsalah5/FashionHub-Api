using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            // Set table name
            builder.ToTable("Products");
            
            // Configure primary key
            builder.HasKey(p => p.Id);
            
            // Configure required fields
            builder.Property(p => p.Name).IsRequired().HasMaxLength(255);
            builder.Property(p => p.Price).IsRequired().HasColumnType("decimal(18,2)");
            builder.Property(p => p.DiscountPrice).HasColumnType("decimal(18,2)");
            builder.Property(p => p.AverageRating).HasColumnType("decimal(3,2)");
            
            // Configure string length constraints
            builder.Property(p => p.Description).HasMaxLength(2000);
            builder.Property(p => p.SKU).HasMaxLength(100);
            builder.Property(p => p.AvailableSizes).HasMaxLength(255);
            builder.Property(p => p.AvailableColors).HasMaxLength(255);
            builder.Property(p => p.Brand).HasMaxLength(100);
            builder.Property(p => p.MainImageUrl).HasMaxLength(500);
            builder.Property(p => p.AdditionalImageUrls).HasMaxLength(2000);
            builder.Property(p => p.Slug).HasMaxLength(300);
            builder.Property(p => p.Tags).HasMaxLength(500);
            
            // Configure indexes
            builder.HasIndex(p => p.Name);
            builder.HasIndex(p => p.SKU).IsUnique().HasFilter("[SKU] IS NOT NULL AND [SKU] != ''");
            builder.HasIndex(p => p.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Slug] != ''");
            
            // Configure fields with default values
            builder.Property(p => p.IsActive).HasDefaultValue(true);
            builder.Property(p => p.IsOnSale).HasDefaultValue(false);
            builder.Property(p => p.IsFeatured).HasDefaultValue(false);
            builder.Property(p => p.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            
            // Configure soft delete filter
            builder.HasQueryFilter(p => !p.IsDeleted);
        }
    }
}

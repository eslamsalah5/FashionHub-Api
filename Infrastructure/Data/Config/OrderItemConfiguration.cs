using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.HasKey(oi => oi.Id);
            
            builder.Property(oi => oi.ProductName)
                .IsRequired()
                .HasMaxLength(255);
                
            builder.Property(oi => oi.UnitPrice)
                .IsRequired()
                .HasColumnType("decimal(18,2)");
                
            builder.Property(oi => oi.Subtotal)
                .IsRequired()
                .HasColumnType("decimal(18,2)");
                
            builder.Property(oi => oi.Quantity)
                .IsRequired();
                
            builder.Property(oi => oi.ProductSKU)
                .HasMaxLength(100);
                
            builder.Property(oi => oi.SelectedSize)
                .HasMaxLength(50);
                
            builder.Property(oi => oi.SelectedColor)
                .HasMaxLength(50);
                
            // Configure relationship with Order
            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure relationship with Product
            builder.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

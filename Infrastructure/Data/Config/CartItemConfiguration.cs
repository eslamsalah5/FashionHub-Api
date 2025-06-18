using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
    {
        public void Configure(EntityTypeBuilder<CartItem> builder)
        {
            builder.HasKey(ci => ci.Id);

            builder.HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
                
            builder.Property(ci => ci.Quantity)
                .IsRequired();
                  builder.Property(ci => ci.PriceAtAddition)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
                
            builder.Property(ci => ci.SelectedSize)
                .HasMaxLength(50);
                
            builder.Property(ci => ci.SelectedColor)
                .HasMaxLength(50);
        }
    }
}

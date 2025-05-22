using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class AdminConfiguration : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            // Configure one-to-one relationship with AppUser
            builder.HasOne(a => a.AppUser)
                .WithOne(u => u.Admin)
                .HasForeignKey<Admin>(a => a.Id)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
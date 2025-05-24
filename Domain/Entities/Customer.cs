namespace Domain.Entities
{
    public class Customer : BaseEntity<string>
    {
        // Navigation properties
        public AppUser AppUser { get; set; } = null!;
        
        // Cart navigation property
        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        // public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
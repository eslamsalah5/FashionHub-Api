namespace Domain.Entities
{
    public class Customer : BaseEntity<string>
    {


        // Navigation properties

        public AppUser AppUser { get; set; } = null!;

        
        // public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        // public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();




    }
}
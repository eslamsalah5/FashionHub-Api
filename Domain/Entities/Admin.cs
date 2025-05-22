namespace Domain.Entities
{
    public class Admin : BaseEntity<string>
    {

        public DateTime HireDate { get; set; }

        
            
        // Navigation properties
        public AppUser AppUser { get; set; } = null!;

        
    }
}
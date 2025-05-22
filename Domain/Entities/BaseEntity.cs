using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class BaseEntity<T>
    {
        [Key]
        public T Id { get; set; } = default!;
        public bool IsDeleted { get; set; } = false;
    }
}
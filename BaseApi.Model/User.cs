using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseApi.Model
{
    public class User
    {
        [Key] // This marks the property as the primary key in your PostgreSQL database
        public Guid UserId { get; set; } = Guid.NewGuid();

        [Required] // This ensures that the EmailId is not null
        [EmailAddress] // This validates that the string is a valid email format
        public string? EmailId { get; set; }

        [Required] // This ensures that the Username is not null
        [MaxLength(50)] // This limits the length of the username
        public string? Username { get; set; }

        [Required] // This ensures that the Password is not null
        [MinLength(6)] // This enforces a minimum password length for security
        public string? Password { get; set; }
    }
}

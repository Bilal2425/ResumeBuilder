using System;
using System.ComponentModel.DataAnnotations;
namespace BaseApi.Model
{
    public class PasswordResetToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string? UserEmail { get; set; }
        [Required]
        public string? Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; } = false;
    }
}

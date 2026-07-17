using System.ComponentModel.DataAnnotations;
namespace BaseApi.Model.DTOs
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
    }
}

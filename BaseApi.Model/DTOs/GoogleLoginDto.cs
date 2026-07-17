using System.ComponentModel.DataAnnotations;
namespace BaseApi.Model.DTOs
{
    public class GoogleLoginDto
    {
        [Required]
        public string? Credential { get; set; }
    }
}

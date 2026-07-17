using BaseApi.Model;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BaseApi.Service
{
    public class AuthService
    {
        private readonly JwtSettings _jwtSettings;

        public AuthService(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }


        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _jwtSettings.SecretKey ?? "Default_Temporary_Secret_Key_For_Development_Only_123456";
            var key = Encoding.UTF8.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
              Subject = new ClaimsIdentity(new Claim[] 
              {
              new Claim(ClaimTypes.Email, user.EmailId ?? string.Empty)

              }),
              Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes ?? 60),
              Issuer = _jwtSettings.Issuer,
              Audience = _jwtSettings.Audience,
              SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)

            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using healthapp.Interfaces;
using healthapp.Models;

namespace healthapp.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        public TokenService(IConfiguration config) => _config = config;

        public string GenerateToken(User user, bool isAccessToken = true)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                isAccessToken ? _config["JWT_ACCESS_SECRET"]! : _config["JWT_REFRESH_SECRET"]!));
            
            var claims = new List<Claim> {
                new Claim("id", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(isAccessToken ? 15 : 1440),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.CreateEncodedJwt(tokenDescriptor);
        }
    }
}
using Core.Application.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Identity
{
    public class JwtTokenService : ITokenService
    {
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtTokenService()
        {
            // Esto idealmente vendría inyectado desde tus configuraciones seguras
            _secret = "ClaveSecretaSuperSeguraDe32Bytes";
            _issuer = "BillingServiceEmisor";
            _audience = "BillingServiceClientes";
        }

        public string GenerateToken(string rucEmpresa, string rol)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, rucEmpresa),
                new Claim(ClaimTypes.Role, rol)
            }),
                Expires = DateTime.UtcNow.AddDays(365), // Tokens de larga duración estilo API-Key para tus clientes
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        public record LoginDto(string? UserName = "dev", string? Role = "seller");

        [HttpPost("token")]
        [AllowAnonymous]
        public ActionResult GetToken([FromBody] LoginDto dto, [FromServices] IConfiguration cfg)
        {
            var jwtKey = cfg["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey)) // A validação de tamanho já ocorre no Program.cs
                throw new InvalidOperationException("A chave JWT (Jwt:Key) não está configurada.");

            var key = jwtKey;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, dto.UserName ?? "dev"),
                new Claim(ClaimTypes.Role, dto.Role ?? "seller")
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { access_token = jwt });
        }
    }
}
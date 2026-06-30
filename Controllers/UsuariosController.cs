using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/usuarios")]
    public class UsuariosController : ControllerBase
    {
        private readonly DbManager _db;
        private readonly IConfiguration _config;

        public UsuariosController(DbManager db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM usuarios WHERE usuario = @usuario AND password = @password",
                new MySqlParameter("@usuario", request.usuario),
                new MySqlParameter("@password", request.password));

            if (rows.Count == 0)
                return Unauthorized(new { mensaje = "Credenciales incorrectas" });

            var row = rows[0];

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, row["id_usuario"].ToString()!),
                new Claim(ClaimTypes.Name, row["usuario"].ToString()!),
                new Claim(ClaimTypes.Role, row["rol"].ToString()!)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expira = token.ValidTo,
                id_usuario = row["id_usuario"],
                nombre = row["nombre"],
                usuario = row["usuario"],
                rol = row["rol"]
            });
        }
    }
}

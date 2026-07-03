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
                "SELECT * FROM usuarios WHERE usuario = @usuario",
                new MySqlParameter("@usuario", request.usuario));

            if (rows.Count == 0)
                return Unauthorized(new { mensaje = "Credenciales incorrectas" });

            var row = rows[0];
            string storedPassword = row["password"].ToString()!;

            bool passwordValida = storedPassword.StartsWith("$2")
                ? BCrypt.Net.BCrypt.Verify(request.password, storedPassword)
                : storedPassword == request.password;

            if (!passwordValida)
                return Unauthorized(new { mensaje = "Credenciales incorrectas" });

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

        [Authorize(Roles = "ADMIN")]
        [HttpPut("{id}/password")]
        public IActionResult CambiarPassword(int id, [FromBody] CambiarPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.nueva_password) || request.nueva_password.Length < 6)
                return BadRequest(new { mensaje = "La contraseña debe tener al menos 6 caracteres" });

            string hash = BCrypt.Net.BCrypt.HashPassword(request.nueva_password);

            int afectados = _db.ExecuteNonQuery(
                "UPDATE usuarios SET password = @password WHERE id_usuario = @id",
                new MySqlParameter("@password", hash),
                new MySqlParameter("@id", id));

            if (afectados == 0)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            return Ok(new { mensaje = "Contraseña actualizada correctamente" });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        public IActionResult Listar()
        {
            var rows = _db.ExecuteQuery("SELECT id_usuario, nombre, usuario, rol FROM usuarios");
            return Ok(rows.Select(r => new
            {
                id = r["id_usuario"],
                nombre = r["nombre"],
                usuario = r["usuario"],
                rol = r["rol"]
            }));
        }
    }

    public class CambiarPasswordRequest
    {
        public string nueva_password { get; set; } = "";
    }
}

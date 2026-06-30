using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DbManager _db;

        public AuthController(DbManager db) => _db = db;

        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login(string usuario, string password)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM usuarios WHERE usuario=@usuario AND password=@password",
                new MySqlParameter("@usuario", usuario),
                new MySqlParameter("@password", password));

            if (rows.Count > 0)
            {
                var row = rows[0];
                return Ok(new { mensaje = "Login correcto", usuario = row["usuario"], rol = row["rol"] });
            }

            return Unauthorized("Credenciales incorrectas");
        }
    }
}

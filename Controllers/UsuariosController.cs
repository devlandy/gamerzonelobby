using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/usuarios")]
    public class UsuariosController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        // 🔐 LOGIN
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"
                SELECT * FROM usuarios
                WHERE usuario = @usuario
                AND password = @password";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue(
                    "@usuario",
                    request.usuario);

                cmd.Parameters.AddWithValue(
                    "@password",
                    request.password);

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return Ok(new
                    {
                        id_usuario =
                        reader["id_usuario"],

                        nombre =
                        reader["nombre"],

                        usuario =
                        reader["usuario"],

                        rol =
                        reader["rol"]
                    });
                }

                return BadRequest(new
                {
                    mensaje = "Credenciales incorrectas"
                });
            }
        }
    }
}
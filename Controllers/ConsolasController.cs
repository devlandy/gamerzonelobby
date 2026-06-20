using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/consolas")]
    public class ConsolasController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        // =========================
        // LISTAR CONSOLAS
        // =========================
        [HttpGet]
        public IActionResult ObtenerConsolas()
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"
                SELECT *
                FROM consolas
                ORDER BY nombre";

                MySqlCommand cmd = new MySqlCommand(query, conn);

                var reader = cmd.ExecuteReader();

                List<object> lista = new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        id = reader["id_consola"],
                        nombre = reader["nombre"],
                        tipo = reader["tipo"],
                        precio = reader["precio_hora"],
                        estado = reader["estado"]
                    });
                }

                return Ok(lista);
            }
        }

        // =========================
        // CAMBIAR ESTADO
        // =========================
        [HttpPut("{id}/estado")]
        public IActionResult CambiarEstado(
            int id,
            [FromBody] EstadoRequest request)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"
                UPDATE consolas
                SET estado = @estado
                WHERE id_consola = @id";

                MySqlCommand cmd = new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@estado", request.estado);
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje = "Estado actualizado"
                });
            }
        }
    }

    // =========================
    // REQUEST
    // =========================
    public class EstadoRequest
    {
        public string estado { get; set; }
    }
}
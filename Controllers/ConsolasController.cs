using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/consolas")]
    public class ConsolasController : ControllerBase
    {
        private readonly DbManager _db;

        public ConsolasController(DbManager db) => _db = db;

        [HttpGet]
        public IActionResult ObtenerConsolas()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM consolas ORDER BY nombre");

            return Ok(rows.Select(r => new
            {
                id = r["id_consola"],
                nombre = r["nombre"],
                tipo = r["tipo"],
                precio = r["precio_hora"],
                estado = r["estado"]
            }));
        }

        [HttpPut("{id}/estado")]
        public IActionResult CambiarEstado(int id, [FromBody] EstadoRequest request)
        {
            _db.ExecuteNonQuery(
                "UPDATE consolas SET estado = @estado WHERE id_consola = @id",
                new MySqlParameter("@estado", request.estado),
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Estado actualizado" });
        }
    }

    public class EstadoRequest
    {
        public string estado { get; set; }
    }
}

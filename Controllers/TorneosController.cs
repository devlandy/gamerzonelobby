using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/torneos")]
    public class TorneosController : ControllerBase
    {
        private readonly DbManager _db;

        public TorneosController(DbManager db) => _db = db;

        [HttpPost]
        public IActionResult CrearTorneo([FromBody] TorneoRequest request)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            var transaction = conn.BeginTransaction();

            try
            {
                var cmdTorneo = new MySqlCommand(@"
                    INSERT INTO torneos (nombre, juego, premio, inscripcion, cupos)
                    VALUES (@nombre, @juego, @premio, @inscripcion, @cupos);
                    SELECT LAST_INSERT_ID();", conn, transaction);
                cmdTorneo.Parameters.AddWithValue("@nombre", request.nombre);
                cmdTorneo.Parameters.AddWithValue("@juego", request.juego);
                cmdTorneo.Parameters.AddWithValue("@premio", request.premio);
                cmdTorneo.Parameters.AddWithValue("@inscripcion", request.inscripcion);
                cmdTorneo.Parameters.AddWithValue("@cupos", request.cupos);

                int idTorneo = Convert.ToInt32(cmdTorneo.ExecuteScalar());

                foreach (var p in request.participantes)
                {
                    int puntos = p.posicion switch { 1 => 10, 2 => 5, 3 => 3, _ => 2 };

                    var cmdPart = new MySqlCommand(@"
                        INSERT INTO torneo_participantes (id_torneo, id_cliente, posicion)
                        VALUES (@torneo, @cliente, @posicion)", conn, transaction);
                    cmdPart.Parameters.AddWithValue("@torneo", idTorneo);
                    cmdPart.Parameters.AddWithValue("@cliente", p.id_cliente);
                    cmdPart.Parameters.AddWithValue("@posicion", p.posicion);
                    cmdPart.ExecuteNonQuery();

                    var cmdPuntos = new MySqlCommand(@"
                        INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                        VALUES (@cliente, 'JUEGO', @puntos, 'TORNEO')", conn, transaction);
                    cmdPuntos.Parameters.AddWithValue("@cliente", p.id_cliente);
                    cmdPuntos.Parameters.AddWithValue("@puntos", puntos);
                    cmdPuntos.ExecuteNonQuery();
                }

                transaction.Commit();
                return Ok(new { mensaje = "Torneo registrado con puntos asignados" });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("top10")]
        public IActionResult Top10()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT c.nombre, SUM(h.puntos) AS total_puntos
                FROM historial_puntos h
                INNER JOIN clientes c ON h.id_cliente = c.id_cliente
                WHERE h.tipo = 'JUEGO'
                GROUP BY h.id_cliente
                ORDER BY total_puntos DESC
                LIMIT 10");

            return Ok(rows.Select(r => new { nombre = r["nombre"].ToString(), puntos = r["total_puntos"] }));
        }

        [HttpPost("participante")]
        public IActionResult RegistrarParticipante(int id_torneo, int id_cliente)
        {
            _db.ExecuteNonQuery(@"
                INSERT INTO torneo_participantes (id_torneo, id_cliente, posicion) VALUES (@torneo, @cliente, 0)",
                new MySqlParameter("@torneo", id_torneo),
                new MySqlParameter("@cliente", id_cliente));

            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo) VALUES (@cliente, 'JUEGO', 2, 'TORNEO')",
                new MySqlParameter("@cliente", id_cliente));

            return Ok(new { mensaje = "Participante registrado" });
        }

        [HttpPost("campeon")]
        public IActionResult Campeon(int id_cliente)
        {
            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo) VALUES (@cliente, 'JUEGO', 10, 'CAMPEON')",
                new MySqlParameter("@cliente", id_cliente));

            return Ok(new { mensaje = "Puntos campeón asignados" });
        }

        [HttpGet]
        public IActionResult Listar()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM torneos ORDER BY fecha DESC");

            return Ok(rows.Select(r => new
            {
                id_torneo = r["id_torneo"],
                nombre = r["nombre"],
                juego = r["juego"],
                premio = r["premio"],
                inscripcion = r["inscripcion"],
                cupos = r["cupos"],
                estado = r["estado"]
            }));
        }
    }
}

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

        [HttpGet]
        public IActionResult Listar([FromQuery] string? tipo)
        {
            string where = string.IsNullOrEmpty(tipo) ? "" : $"WHERE tipo = '{tipo}'";
            var rows = _db.ExecuteQuery($"SELECT * FROM torneos {where} ORDER BY fecha DESC");
            return Ok(rows.Select(r => new
            {
                id_torneo   = r["id_torneo"],
                nombre      = r["nombre"],
                tipo        = r["tipo"],
                juego       = r["juego"],
                premio      = r["premio"],
                inscripcion = r["inscripcion"],
                cupos       = r["cupos"],
                estado      = r["estado"],
                fecha       = r["fecha"]
            }));
        }

        [HttpGet("{id}/participantes")]
        public IActionResult Participantes(int id)
        {
            var rows = _db.ExecuteQuery(@"
                SELECT tp.posicion, c.id_cliente, c.nombre, c.apodo
                FROM torneo_participantes tp
                JOIN clientes c ON tp.id_cliente = c.id_cliente
                WHERE tp.id_torneo = @id
                ORDER BY tp.posicion ASC, c.nombre ASC",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new
            {
                id_cliente = r["id_cliente"],
                nombre     = r["nombre"],
                apodo      = r["apodo"],
                posicion   = r["posicion"]
            }));
        }

        [HttpPost]
        public IActionResult Crear([FromBody] TorneoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.nombre) || string.IsNullOrWhiteSpace(request.juego))
                return BadRequest(new { error = "Nombre y juego son requeridos" });

            if (request.tipo != "MICROWIN" && request.tipo != "FLASH")
                return BadRequest(new { error = "Tipo debe ser MICROWIN o FLASH" });

            var id = _db.ExecuteScalar(@"
                INSERT INTO torneos (nombre, tipo, juego, premio, inscripcion, cupos)
                VALUES (@nombre, @tipo, @juego, @premio, @inscripcion, @cupos);
                SELECT LAST_INSERT_ID();",
                new MySqlParameter("@nombre",      request.nombre),
                new MySqlParameter("@tipo",        request.tipo),
                new MySqlParameter("@juego",       request.juego),
                new MySqlParameter("@premio",      request.premio),
                new MySqlParameter("@inscripcion", request.inscripcion),
                new MySqlParameter("@cupos",       request.cupos));

            return Ok(new { mensaje = "Torneo creado", id_torneo = Convert.ToInt32(id) });
        }

        [HttpPost("{id}/inscribir")]
        public IActionResult Inscribir(int id, [FromQuery] int id_cliente)
        {
            // Verificar que el torneo existe y está abierto
            var torneo = _db.ExecuteQuery("SELECT estado, cupos FROM torneos WHERE id_torneo=@id",
                new MySqlParameter("@id", id));
            if (torneo.Count == 0) return NotFound(new { error = "Torneo no encontrado" });
            if (torneo[0]["estado"].ToString() == "FINALIZADO")
                return BadRequest(new { error = "El torneo ya finalizó" });

            // Verificar que no esté ya inscrito
            var existe = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM torneo_participantes WHERE id_torneo=@t AND id_cliente=@c",
                new MySqlParameter("@t", id),
                new MySqlParameter("@c", id_cliente));
            if (Convert.ToInt32(existe) > 0)
                return BadRequest(new { error = "El jugador ya está inscrito" });

            // Inscribir con posicion=0 (sin definir aún)
            _db.ExecuteNonQuery(@"
                INSERT INTO torneo_participantes (id_torneo, id_cliente, posicion)
                VALUES (@torneo, @cliente, 0)",
                new MySqlParameter("@torneo",  id),
                new MySqlParameter("@cliente", id_cliente));

            // 2 puntos de juego por participar
            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                VALUES (@cliente, 'JUEGO', 2, 'TORNEO')",
                new MySqlParameter("@cliente", id_cliente));

            return Ok(new { mensaje = "Jugador inscrito (+2 pts)" });
        }

        [HttpPost("{id}/finalizar")]
        public IActionResult Finalizar(int id, [FromBody] List<PosicionRequest> posiciones)
        {
            var torneo = _db.ExecuteQuery("SELECT estado FROM torneos WHERE id_torneo=@id",
                new MySqlParameter("@id", id));
            if (torneo.Count == 0) return NotFound(new { error = "Torneo no encontrado" });
            if (torneo[0]["estado"].ToString() == "FINALIZADO")
                return BadRequest(new { error = "El torneo ya fue finalizado" });

            using var conn = _db.GetConnection();
            conn.Open();
            var tx = conn.BeginTransaction();
            try
            {
                foreach (var p in posiciones)
                {
                    // Puntos extra según posición (ya tienen los 2 de participar)
                    int ptsPosicion = p.posicion switch { 1 => 8, 2 => 3, 3 => 1, _ => 0 };

                    var cmdPos = new MySqlCommand(@"
                        UPDATE torneo_participantes SET posicion=@pos
                        WHERE id_torneo=@torneo AND id_cliente=@cliente",
                        conn, tx);
                    cmdPos.Parameters.AddWithValue("@pos",     p.posicion);
                    cmdPos.Parameters.AddWithValue("@torneo",  id);
                    cmdPos.Parameters.AddWithValue("@cliente", p.id_cliente);
                    cmdPos.ExecuteNonQuery();

                    if (ptsPosicion > 0)
                    {
                        string motivo = p.posicion switch { 1 => "CAMPEON", 2 => "FINALISTA", 3 => "SEMIFINALISTA", _ => "TORNEO" };
                        var cmdPts = new MySqlCommand(@"
                            INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                            VALUES (@cliente, 'JUEGO', @puntos, @motivo)",
                            conn, tx);
                        cmdPts.Parameters.AddWithValue("@cliente", p.id_cliente);
                        cmdPts.Parameters.AddWithValue("@puntos",  ptsPosicion);
                        cmdPts.Parameters.AddWithValue("@motivo",  motivo);
                        cmdPts.ExecuteNonQuery();
                    }
                }

                var cmdEstado = new MySqlCommand(
                    "UPDATE torneos SET estado='FINALIZADO' WHERE id_torneo=@id", conn, tx);
                cmdEstado.Parameters.AddWithValue("@id", id);
                cmdEstado.ExecuteNonQuery();

                tx.Commit();
                return Ok(new { mensaje = "Torneo finalizado y puntos asignados" });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("top10")]
        public IActionResult Top10()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT c.nombre, c.apodo, SUM(h.puntos) AS total_puntos
                FROM historial_puntos h
                INNER JOIN clientes c ON h.id_cliente = c.id_cliente
                WHERE h.tipo = 'JUEGO'
                GROUP BY h.id_cliente, c.nombre, c.apodo
                ORDER BY total_puntos DESC, MIN(h.fecha) ASC
                LIMIT 10");

            return Ok(rows.Select(r => new
            {
                nombre = r["nombre"].ToString(),
                apodo  = r["apodo"]?.ToString(),
                puntos = r["total_puntos"]
            }));
        }
    }

    public class PosicionRequest
    {
        public int id_cliente { get; set; }
        public int posicion   { get; set; }
    }
}

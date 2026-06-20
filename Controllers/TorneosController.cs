using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/torneos")]
    public class TorneosController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        [HttpPost]
        public IActionResult CrearTorneo([FromBody] TorneoRequest request)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();
                var transaction = conn.BeginTransaction();

                try
                {
                    // =========================
                    // CREAR TORNEO
                    // =========================
                    string torneoQuery = @"

INSERT INTO torneos
(
nombre,
juego,
premio,
inscripcion,
cupos
)

VALUES
(
@nombre,
@juego,
@premio,
@inscripcion,
@cupos
);

SELECT LAST_INSERT_ID();
";
                    MySqlCommand cmdTorneo = new MySqlCommand(torneoQuery, conn, transaction);
                    cmdTorneo.Parameters.AddWithValue("@nombre", request.nombre);
                    cmdTorneo.Parameters.AddWithValue("@juego",request.juego);
                    cmdTorneo.Parameters.AddWithValue("@premio",request.premio);

                    cmdTorneo.Parameters.AddWithValue(
                    "@inscripcion",
                    request.inscripcion);

                    cmdTorneo.Parameters.AddWithValue(
                    "@cupos",
                    request.cupos);



                    int idTorneo = Convert.ToInt32(cmdTorneo.ExecuteScalar());

                    // =========================
                    // PARTICIPANTES + PUNTOS
                    // =========================
                    foreach (var p in request.participantes)
                    {
                        int puntos = 0;

                        if (p.posicion == 1)
                            puntos = 10;
                        else if (p.posicion == 2)
                            puntos = 5;
                        else if (p.posicion == 3)
                            puntos = 3;
                        else
                            puntos = 2;

                        // guardar participante
                        string participanteQuery = @"

INSERT INTO torneo_participantes
(
id_torneo,
id_cliente,
posicion
)

VALUES
(
@torneo,
@cliente,
@posicion
)";

                        MySqlCommand cmdPart = new MySqlCommand(participanteQuery, conn, transaction);
                        cmdPart.Parameters.AddWithValue("@torneo", idTorneo);
                        cmdPart.Parameters.AddWithValue("@cliente", p.id_cliente);
                        cmdPart.Parameters.AddWithValue("@posicion", p.posicion);
                        cmdPart.ExecuteNonQuery();

                        // =========================
                        // GUARDAR PUNTOS (JUEGO)
                        // =========================
                        string puntosQuery = @"INSERT INTO historial_puntos
                        (id_cliente, tipo, puntos, motivo)
                        VALUES (@cliente, 'JUEGO', @puntos, 'TORNEO')";

                        MySqlCommand cmdPuntos = new MySqlCommand(puntosQuery, conn, transaction);
                        cmdPuntos.Parameters.AddWithValue("@cliente", p.id_cliente);
                        cmdPuntos.Parameters.AddWithValue("@puntos", puntos);
                        cmdPuntos.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    return Ok(new
                    {
                        mensaje = "Torneo registrado con puntos asignados"
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return BadRequest(ex.Message);
                }
            }

        }

        [HttpGet("top10")]
        public IActionResult Top10()
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"
        SELECT c.nombre, SUM(h.puntos) AS total_puntos
        FROM historial_puntos h
        INNER JOIN clientes c ON h.id_cliente = c.id_cliente
        WHERE h.tipo = 'JUEGO'
        GROUP BY h.id_cliente
        ORDER BY total_puntos DESC
        LIMIT 10";

                MySqlCommand cmd = new MySqlCommand(query, conn);
                var reader = cmd.ExecuteReader();

                List<object> lista = new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        nombre = reader["nombre"].ToString(),
                        puntos = reader["total_puntos"]
                    });
                }

                return Ok(lista);
            }
        }


        // ======================
        // REGISTRAR PARTICIPANTE
        // ======================
        [HttpPost("participante")]
        public IActionResult RegistrarParticipante(
            int id_torneo,
            int id_cliente)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

INSERT INTO torneo_participantes
(
id_torneo,
id_cliente,
posicion
)

VALUES
(
@torneo,
@cliente,
0
)";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue(
                    "@torneo",
                    id_torneo);

                cmd.Parameters.AddWithValue(
                    "@cliente",
                    id_cliente);

                cmd.ExecuteNonQuery();

                // 🔥 PUNTOS POR PARTICIPAR

                string puntos = @"

        INSERT INTO historial_puntos
        (id_cliente,tipo,puntos,motivo)

        VALUES
        (@cliente,'JUEGO',2,'TORNEO')";

                MySqlCommand cmdPuntos =
                new MySqlCommand(puntos, conn);

                cmdPuntos.Parameters.AddWithValue(
                    "@cliente",
                    id_cliente);

                cmdPuntos.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje =
                    "Participante registrado"
                });
            }
        }


        // ======================
        // CAMPEON
        // ======================
        [HttpPost("campeon")]
        public IActionResult Campeon(int id_cliente)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

        INSERT INTO historial_puntos
        (id_cliente,tipo,puntos,motivo)

        VALUES
        (@cliente,'JUEGO',10,'CAMPEON')";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue(
                    "@cliente",
                    id_cliente);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje =
                    "Puntos campeón asignados"
                });
            }
        }

        // ======================
        // LISTAR TORNEOS
        // ======================
        [HttpGet]
        public IActionResult Listar()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

SELECT *

FROM torneos

ORDER BY fecha DESC
";

                MySqlCommand cmd =
                new MySqlCommand(
                query, conn);

                var reader =
                cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        id_torneo =
                        reader["id_torneo"],

                        nombre =
                        reader["nombre"],

                        juego =
                        reader["juego"],

                        premio =
                        reader["premio"],

                        inscripcion =
                        reader["inscripcion"],

                        cupos =
                        reader["cupos"],

                        estado =
                        reader["estado"]
                    });
                }

                return Ok(lista);
            }
        }
    }
}

     


    

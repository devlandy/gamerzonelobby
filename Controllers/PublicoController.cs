using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using System.Net;
using System.Net.Sockets;

namespace GamerZoneAPI.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/publico")]
    public class PublicoController : ControllerBase
    {
        private readonly DbManager _db;
        public PublicoController(DbManager db) => _db = db;

        [HttpGet("host")]
        public IActionResult GetHost()
        {
            // Devuelve la IP local del servidor para que el QR apunte a la IP de red y no a localhost
            string ip = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString() ?? "localhost";

            return Ok(new { host = $"http://{ip}:5069" });
        }

        [HttpGet("cliente/{codigo}")]
        public IActionResult PuntosCliente(string codigo)
        {
            var clientes = _db.ExecuteQuery(@"
                SELECT id_cliente, nombre, apodo, codigo
                FROM clientes WHERE codigo = @codigo",
                new MySqlParameter("@codigo", codigo));

            if (clientes.Count == 0)
                return NotFound(new { error = "Cliente no encontrado" });

            var c = clientes[0];
            int idCliente = Convert.ToInt32(c["id_cliente"]);

            // Puntos de juego (para ranking Top 10)
            var ptsJuego = _db.ExecuteScalar(@"
                SELECT IFNULL(SUM(puntos), 0) FROM historial_puntos
                WHERE id_cliente = @id AND tipo = 'JUEGO'",
                new MySqlParameter("@id", idCliente));

            // Puntos de consumo (para canje de comida)
            var ptsConsumo = _db.ExecuteScalar(@"
                SELECT IFNULL(SUM(puntos), 0) FROM historial_puntos
                WHERE id_cliente = @id AND tipo = 'CONSUMO'",
                new MySqlParameter("@id", idCliente));

            // Historial de puntos reciente
            var historial = _db.ExecuteQuery(@"
                SELECT tipo, puntos, motivo, fecha
                FROM historial_puntos
                WHERE id_cliente = @id
                ORDER BY fecha DESC
                LIMIT 20",
                new MySqlParameter("@id", idCliente));

            // Posición en el ranking de juego
            var ranking = _db.ExecuteQuery(@"
                SELECT id_cliente, SUM(puntos) AS total
                FROM historial_puntos WHERE tipo = 'JUEGO'
                GROUP BY id_cliente
                ORDER BY total DESC");

            int posicion = ranking.FindIndex(r => Convert.ToInt32(r["id_cliente"]) == idCliente) + 1;

            return Ok(new
            {
                nombre       = c["nombre"]?.ToString(),
                apodo        = c["apodo"]?.ToString(),
                codigo       = c["codigo"]?.ToString(),
                puntos_juego   = Convert.ToDecimal(ptsJuego),
                puntos_consumo = Convert.ToDecimal(ptsConsumo),
                posicion_ranking = posicion > 0 ? posicion : (int?)null,
                historial = historial.Select(h => new
                {
                    tipo   = h["tipo"]?.ToString(),
                    puntos = Convert.ToDecimal(h["puntos"]),
                    motivo = h["motivo"]?.ToString(),
                    fecha  = h["fecha"]
                })
            });
        }
    }
}

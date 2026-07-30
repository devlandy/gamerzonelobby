using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly DbManager _db;

        public ClientesController(DbManager db) => _db = db;

        [HttpPost]
        public IActionResult CrearCliente([FromBody] ClienteRequest request)
        {
            string codigo = "CLI-" + DateTime.Now.Ticks.ToString().Substring(10);

            var idCliente = Convert.ToInt32(_db.ExecuteScalar(
                @"INSERT INTO clientes (nombre, telefono, apodo, codigo, estado) VALUES (@nombre, @telefono, @apodo, @codigo, 'ACTIVO');
                  SELECT LAST_INSERT_ID();",
                new MySqlParameter("@nombre",   request.nombre),
                new MySqlParameter("@telefono", request.telefono ?? ""),
                new MySqlParameter("@apodo",    request.apodo ?? ""),
                new MySqlParameter("@codigo",   codigo)));

            return Ok(new { mensaje = "Cliente creado correctamente", codigo, id = idCliente });
        }

        [HttpGet("buscar")]
        public IActionResult Buscar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return BadRequest("Debe ingresar texto para buscar");

            var rows = _db.ExecuteQuery(
                "SELECT * FROM clientes WHERE nombre LIKE @texto OR telefono LIKE @texto OR apodo LIKE @texto",
                new MySqlParameter("@texto", "%" + texto + "%"));

            return Ok(rows.Select(r => new
            {
                codigo = r["codigo"]?.ToString(),
                id = Convert.ToInt32(r["id_cliente"]),
                nombre = r["nombre"]?.ToString(),
                telefono = r["telefono"]?.ToString(),
                apodo = r["apodo"]?.ToString()
            }));
        }

        [HttpGet]
        public IActionResult Listar()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM clientes");

            return Ok(rows.Select(r => new
            {
                codigo = r["codigo"]?.ToString(),
                id = Convert.ToInt32(r["id_cliente"]),
                nombre = r["nombre"]?.ToString(),
                telefono = r["telefono"]?.ToString(),
                apodo = r["apodo"]?.ToString()
            }));
        }

        [HttpGet("qr/{codigo}")]
        public IActionResult GenerarQR(string codigo)
        {
            var count = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM clientes WHERE codigo = @codigo",
                new MySqlParameter("@codigo", codigo));

            if (Convert.ToInt32(count) == 0)
                return NotFound("Cliente no encontrado");

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(codigo, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var qrImage = qrCode.GetGraphic(20);
            using var ms = new MemoryStream();
            qrImage.Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        [HttpGet("{id}/puntos")]
        public IActionResult PuntosCliente(int id)
        {
            var ptsJuego = _db.ExecuteScalar(@"
                SELECT IFNULL(SUM(puntos), 0) FROM historial_puntos
                WHERE id_cliente = @id AND tipo = 'JUEGO'",
                new MySqlParameter("@id", id));

            var ptsConsumo = _db.ExecuteScalar(@"
                SELECT IFNULL(SUM(puntos), 0) FROM historial_puntos
                WHERE id_cliente = @id AND tipo = 'CONSUMO'",
                new MySqlParameter("@id", id));

            decimal juego = Convert.ToDecimal(ptsJuego);
            decimal consumo = Convert.ToDecimal(ptsConsumo);

            return Ok(new
            {
                puntos_juego = juego,
                puntos_consumo = consumo,
                puntos_total = juego + consumo
            });
        }

        [HttpGet("{id}/historial-puntos")]
        public IActionResult HistorialPuntos(int id)
        {
            var rows = _db.ExecuteQuery(@"
                SELECT tipo, puntos, motivo, fecha
                FROM historial_puntos
                WHERE id_cliente = @id
                ORDER BY fecha DESC
                LIMIT 50",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new
            {
                tipo   = r["tipo"]?.ToString(),
                puntos = Convert.ToDecimal(r["puntos"]),
                motivo = r["motivo"]?.ToString(),
                fecha  = r["fecha"]
            }));
        }

        [HttpGet("{id}/compras")]
        public IActionResult HistorialCompras(int id)
        {
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, v.total, v.metodo_pago, v.estado
                FROM ventas v
                WHERE v.id_cliente = @id AND v.estado != 'CANCELADO'
                ORDER BY v.fecha DESC",
                new MySqlParameter("@id", id));

            var resultado = ventas.Select(v =>
            {
                int idVenta = Convert.ToInt32(v["id_venta"]);
                var detalle = _db.ExecuteQuery(@"
                    SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre, d.cantidad, d.precio
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta = @id AND d.precio > 0",
                    new MySqlParameter("@id", idVenta));

                return new
                {
                    id_venta = idVenta,
                    fecha = v["fecha"],
                    total = v["total"],
                    metodo_pago = v["metodo_pago"],
                    estado = v["estado"],
                    items = detalle.Select(d => new
                    {
                        nombre = d["nombre"].ToString(),
                        cantidad = d["cantidad"],
                        precio = d["precio"]
                    })
                };
            });

            return Ok(resultado);
        }

        [HttpPost("recalcular-puntos")]
        public IActionResult RecalcularPuntos()
        {
            // Borrar puntos históricos para evitar duplicados, conservar solo los del día de hoy en adelante
            _db.ExecuteNonQuery(@"
                DELETE FROM historial_puntos
                WHERE motivo IN ('COMPRA', 'CONSOLA', 'RECALCULO_HISTORICO')");

            // CONSUMO: 0.05 pts por cada Q1 en productos (id_producto IS NOT NULL)
            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo, fecha)
                SELECT
                    v.id_cliente,
                    'CONSUMO',
                    ROUND(SUM(d.subtotal) * 0.05, 2),
                    'RECALCULO_HISTORICO',
                    v.fecha
                FROM ventas v
                JOIN detalle_ventas d ON d.id_venta = v.id_venta
                WHERE v.id_cliente IS NOT NULL
                  AND v.estado = 'PAGADO'
                  AND d.id_producto IS NOT NULL
                GROUP BY v.id_venta, v.id_cliente, v.fecha
                HAVING ROUND(SUM(d.subtotal) * 0.05, 2) > 0");

            // JUEGO: 5 pts por hora de consola — estimado desde subtotal / precio_hora
            // Usa LIKE para coincidir "Xbox #1" con "Xbox #1 (1h + 10min + 5min)"
            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo, fecha)
                SELECT
                    v.id_cliente,
                    'JUEGO',
                    ROUND((SUM(d.subtotal) / c.precio_hora) * 5, 2),
                    'RECALCULO_HISTORICO',
                    v.fecha
                FROM ventas v
                JOIN detalle_ventas d ON d.id_venta = v.id_venta
                JOIN consolas c ON d.nombre LIKE CONCAT('%', c.nombre, '%')
                WHERE v.id_cliente IS NOT NULL
                  AND v.estado = 'PAGADO'
                  AND d.id_producto IS NULL
                GROUP BY v.id_venta, v.id_cliente, v.fecha, c.precio_hora
                HAVING ROUND((SUM(d.subtotal) / c.precio_hora) * 5, 2) > 0");

            // Contar clientes afectados
            var total = _db.ExecuteScalar(
                "SELECT COUNT(DISTINCT id_cliente) FROM historial_puntos WHERE motivo = 'RECALCULO_HISTORICO'");

            return Ok(new { mensaje = "Puntos recalculados correctamente", clientes_actualizados = Convert.ToInt32(total) });
        }

        [HttpPost("{id}/ajustar-puntos")]
        public IActionResult AjustarPuntos(int id, [FromBody] AjustePuntosRequest req)
        {
            if (req.puntos == 0)
                return BadRequest(new { error = "Los puntos no pueden ser 0" });

            var existe = _db.ExecuteScalar("SELECT COUNT(*) FROM clientes WHERE id_cliente=@id",
                new MySqlParameter("@id", id));
            if (Convert.ToInt32(existe) == 0)
                return NotFound(new { error = "Cliente no encontrado" });

            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo, fecha)
                VALUES (@id, @tipo, @puntos, 'AJUSTE_MANUAL', NOW())",
                new MySqlParameter("@id", id),
                new MySqlParameter("@tipo", req.tipo ?? "JUEGO"),
                new MySqlParameter("@puntos", req.puntos));

            return Ok(new { mensaje = $"Puntos ajustados: {(req.puntos > 0 ? "+" : "")}{req.puntos}" });
        }
    }

    public class AjustePuntosRequest
    {
        public decimal puntos { get; set; }
        public string? tipo { get; set; }
        public string? descripcion { get; set; }
    }
}

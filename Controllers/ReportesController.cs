using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
        private readonly DbManager _db;

        public ReportesController(DbManager db) => _db = db;

        [HttpGet("ventas")]
        public IActionResult Ventas()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, v.total, v.forma_cobro, v.metodo_pago, v.estado,
                       c.nombre AS cliente, u.nombre AS usuario
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                LEFT JOIN usuarios u ON v.id_usuario = u.id_usuario
                ORDER BY v.id_venta DESC");

            return Ok(rows.Select(r => new
            {
                id = Convert.ToInt32(r["id_venta"]),
                fecha = r["fecha"],
                total = r["total"],
                cliente = r["cliente"] is DBNull ? null : r["cliente"]?.ToString(),
                usuario = r["usuario"] is DBNull ? null : r["usuario"]?.ToString(),
                forma_cobro = r["forma_cobro"]?.ToString(),
                metodo_pago = r["metodo_pago"]?.ToString(),
                estado = r["estado"]?.ToString()
            }));
        }

        [HttpGet("inventario")]
        public IActionResult Inventario()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT nombre, precio_compra, precio_venta, stock,
                       (precio_venta - precio_compra) AS ganancia_unitaria,
                       ((precio_venta - precio_compra) / NULLIF(precio_compra,0) * 100) AS margen_pct
                FROM productos
                WHERE controla_stock = 1 AND precio_venta > 0
                ORDER BY ganancia_unitaria DESC");

            return Ok(rows.Select(r => new
            {
                nombre = r["nombre"],
                precio_compra = r["precio_compra"],
                precio_venta = r["precio_venta"],
                stock = r["stock"],
                ganancia = r["ganancia_unitaria"],
                margen_pct = r["margen_pct"]
            }));
        }

        [HttpGet("top-productos")]
        public IActionResult TopProductos([FromQuery] string? mes)
        {
            string where = string.IsNullOrEmpty(mes)
                ? "WHERE DATE_FORMAT(v.fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m') AND v.estado != 'CANCELADO'"
                : $"WHERE DATE_FORMAT(v.fecha, '%Y-%m') = '{mes}' AND v.estado != 'CANCELADO'";

            var rows = _db.ExecuteQuery($@"
                SELECT p.nombre, SUM(d.cantidad) AS total_vendidos, SUM(d.subtotal) AS total_ingresos
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto = p.id_producto
                JOIN ventas v ON d.id_venta = v.id_venta
                {where} AND d.precio > 0
                GROUP BY p.id_producto, p.nombre
                ORDER BY total_vendidos DESC
                LIMIT 5");

            return Ok(rows.Select(r => new
            {
                nombre = r["nombre"],
                vendidos = r["total_vendidos"],
                ingresos = r["total_ingresos"]
            }));
        }

        [HttpGet("cancelaciones")]
        public IActionResult Cancelaciones([FromQuery] string? mes)
        {
            string where = string.IsNullOrEmpty(mes)
                ? "WHERE DATE_FORMAT(v.fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m') AND v.estado = 'CANCELADO'"
                : $"WHERE DATE_FORMAT(v.fecha, '%Y-%m') = '{mes}' AND v.estado = 'CANCELADO'";

            var rows = _db.ExecuteQuery($@"
                SELECT v.id_venta, v.fecha, v.total, v.observacion,
                       c.nombre AS cliente
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                {where}
                ORDER BY v.fecha DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_venta"],
                fecha = r["fecha"],
                total = r["total"],
                cliente = r["cliente"],
                observacion = r["observacion"]
            }));
        }

        [HttpGet("metodos-pago")]
        public IActionResult MetodosPago([FromQuery] string? mes)
        {
            string where = string.IsNullOrEmpty(mes)
                ? "WHERE DATE_FORMAT(fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m') AND metodo_pago IS NOT NULL AND estado != 'CANCELADO'"
                : $"WHERE DATE_FORMAT(fecha, '%Y-%m') = '{mes}' AND metodo_pago IS NOT NULL AND estado != 'CANCELADO'";

            var rows = _db.ExecuteQuery($@"
                SELECT metodo_pago, COUNT(*) AS cantidad, SUM(total) AS total
                FROM ventas
                {where}
                GROUP BY metodo_pago
                ORDER BY total DESC");

            return Ok(rows.Select(r => new
            {
                metodo = r["metodo_pago"],
                cantidad = r["cantidad"],
                total = r["total"]
            }));
        }

        [HttpGet("gastos")]
        public IActionResult Gastos()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM gastos ORDER BY fecha DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_gasto"],
                descripcion = r["descripcion"],
                monto = r["monto"],
                fecha = r["fecha"]
            }));
        }

        [HttpGet("cierre")]
        public IActionResult Cierre()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM cierre_diario ORDER BY fecha DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_cierre"],
                total_ventas = r["total_ventas"],
                total_gastos = r["total_gastos"],
                balance = r["balance"],
                estado = r["estado"],
                fecha = r["fecha"]
            }));
        }

        [HttpGet("ventas-mensuales")]
        public IActionResult VentasMensuales()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT DATE_FORMAT(fecha, '%Y-%m') AS mes, SUM(total) AS total
                FROM ventas WHERE estado != 'CANCELADO'
                GROUP BY mes
                ORDER BY mes DESC");

            return Ok(rows.Select(r => new { mes = r["mes"], total = r["total"] }));
        }
    }
}

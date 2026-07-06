using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
                SELECT v.id_venta, v.fecha, v.total, v.forma_cobro, v.metodo_pago,
                       c.nombre AS cliente, u.nombre AS usuario
                FROM ventas v
                JOIN clientes c ON v.id_cliente = c.id_cliente
                JOIN usuarios u ON v.id_usuario = u.id_usuario
                ORDER BY v.id_venta DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_venta"],
                fecha = r["fecha"],
                total = r["total"],
                cliente = r["cliente"],
                usuario = r["usuario"],
                forma_cobro = r["forma_cobro"],
                metodo_pago = r["metodo_pago"]
            }));
        }

        [HttpGet("inventario")]
        public IActionResult Inventario()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT nombre, precio_compra, precio_venta, stock,
                       (precio_venta - precio_compra) AS ganancia_unitaria
                FROM productos");

            return Ok(rows.Select(r => new
            {
                nombre = r["nombre"],
                precio_compra = r["precio_compra"],
                precio_venta = r["precio_venta"],
                stock = r["stock"],
                ganancia = r["ganancia_unitaria"]
            }));
        }

        [HttpGet("gastos")]
        public IActionResult Gastos()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM gastos");

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
            var rows = _db.ExecuteQuery("SELECT * FROM cierre_diario");

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

        [HttpGet("top-productos")]
        public IActionResult TopProductos()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT p.nombre, SUM(d.cantidad) AS total_vendidos
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto = p.id_producto
                GROUP BY p.nombre
                ORDER BY total_vendidos DESC
                LIMIT 10");

            return Ok(rows.Select(r => new
            {
                producto = r["nombre"],
                vendidos = r["total_vendidos"]
            }));
        }

        [HttpGet("ventas-mensuales")]
        public IActionResult VentasMensuales()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT DATE_FORMAT(fecha, '%Y-%m') AS mes, SUM(total) AS total
                FROM ventas
                GROUP BY mes
                ORDER BY mes DESC");

            return Ok(rows.Select(r => new { mes = r["mes"], total = r["total"] }));
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly DbManager _db;

        public DashboardController(DbManager db) => _db = db;

        [HttpGet]
        public IActionResult ObtenerDashboard()
        {
            decimal ventasDia = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(total),0) FROM ventas WHERE DATE(fecha) = CURDATE()"));
            int pendientes = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM ventas WHERE forma_cobro = 'PENDIENTE'"));
            int agotados = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM productos WHERE stock = 0"));
            int porTerminar = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM productos WHERE stock > 0 AND stock <= 5"));
            decimal gastosDia = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(monto),0) FROM gastos WHERE DATE(fecha) = CURDATE()"));
            int cierre = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM cierre_diario WHERE DATE(fecha) = CURDATE()"));
            int consolasPendientes = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM ventas WHERE tipo = 'CONSOLA' AND forma_cobro = 'PENDIENTE'"));

            return Ok(new
            {
                ventas_dia = ventasDia,
                pedidos_pendientes = pendientes,
                productos_agotados = agotados,
                productos_por_terminar = porTerminar,
                gastos_dia = gastosDia,
                balance = ventasDia - gastosDia,
                cierre_dia = cierre > 0 ? "REALIZADO" : "PENDIENTE",
                consolas_pendientes = consolasPendientes
            });
        }

        [HttpPost("cierre")]
        public IActionResult CerrarDia()
        {
            int existe = Convert.ToInt32(_db.ExecuteScalar("SELECT COUNT(*) FROM cierre_diario WHERE DATE(fecha) = CURDATE()"));
            if (existe > 0)
                return BadRequest("El cierre de hoy ya fue realizado");

            decimal ventasDia = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(total),0) FROM ventas WHERE DATE(fecha) = CURDATE()"));
            decimal gastosDia = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(monto),0) FROM gastos WHERE DATE(fecha) = CURDATE()"));
            decimal balance = ventasDia - gastosDia;

            _db.ExecuteNonQuery(@"
                INSERT INTO cierre_diario (total_ventas, total_gastos, balance, estado)
                VALUES (@ventas, @gastos, @balance, 'CERRADO')",
                new MySqlParameter("@ventas", ventasDia),
                new MySqlParameter("@gastos", gastosDia),
                new MySqlParameter("@balance", balance));

            return Ok(new { mensaje = "Cierre realizado correctamente", ventas = ventasDia, gastos = gastosDia, balance });
        }

        [HttpPost("puntos/juego")]
        public IActionResult PuntosJuego(int id_cliente, int puntos)
        {
            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo) VALUES (@cliente, 'JUEGO', @puntos, 'MANUAL')",
                new MySqlParameter("@cliente", id_cliente),
                new MySqlParameter("@puntos", puntos));

            return Ok(new { mensaje = "Puntos de juego agregados" });
        }

        [HttpPost("puntos/consumo")]
        public IActionResult PuntosConsumo(int id_cliente, decimal monto)
        {
            decimal puntos = monto * 0.05m;

            _db.ExecuteNonQuery(@"
                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo) VALUES (@cliente, 'CONSUMO', @puntos, 'COMPRA')",
                new MySqlParameter("@cliente", id_cliente),
                new MySqlParameter("@puntos", puntos));

            return Ok(new { mensaje = "Puntos de consumo agregados", puntos });
        }

        [HttpPost("venta-rapida")]
        public IActionResult VentaRapida([FromQuery] int id_cliente, [FromQuery] decimal total)
        {
            _db.ExecuteNonQuery(@"
                INSERT INTO ventas (id_cliente, total, forma_cobro, fecha) VALUES (@cliente, @total, 'CANCELADO', NOW())",
                new MySqlParameter("@cliente", id_cliente),
                new MySqlParameter("@total", total));

            return Ok(new { mensaje = "Venta registrada correctamente", cliente = id_cliente, total });
        }

        [HttpGet("top-clientes")]
        public IActionResult TopClientes()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT c.nombre, COUNT(v.id_venta) AS compras, SUM(v.total) AS total, IFNULL(SUM(hp.puntos),0) AS puntos
                FROM ventas v
                JOIN clientes c ON v.id_cliente = c.id_cliente
                LEFT JOIN historial_puntos hp ON c.id_cliente = hp.id_cliente
                GROUP BY c.id_cliente, c.nombre
                ORDER BY puntos DESC, total DESC
                LIMIT 5");

            return Ok(rows.Select(r => new
            {
                nombre = r["nombre"],
                compras = r["compras"],
                total = r["total"],
                puntos = r["puntos"]
            }));
        }

        [HttpGet("top-gamers")]
        public IActionResult TopGamers()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT c.nombre, c.apodo, SUM(h.puntos) AS puntos
                FROM historial_puntos h
                JOIN clientes c ON h.id_cliente = c.id_cliente
                WHERE h.tipo = 'JUEGO'
                GROUP BY c.id_cliente
                ORDER BY puntos DESC
                LIMIT 10");

            return Ok(rows.Select(r => new { nombre = r["nombre"], apodo = r["apodo"], puntos = r["puntos"] }));
        }

        [HttpPost("cierre-dia")]
        public IActionResult CierreDia()
        {
            decimal totalVentas = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(total),0) FROM ventas WHERE forma_cobro = 'PAGADO'"));
            decimal totalGastos = 0;
            decimal balance = totalVentas - totalGastos;

            _db.ExecuteNonQuery(@"
                INSERT INTO cierre_diario (total_ventas, total_gastos, balance, estado)
                VALUES (@ventas, @gastos, @balance, 'CERRADO')",
                new MySqlParameter("@ventas", totalVentas),
                new MySqlParameter("@gastos", totalGastos),
                new MySqlParameter("@balance", balance));

            return Ok(new { ventas = totalVentas, gastos = totalGastos, balance });
        }

        [HttpGet("historial-cierres")]
        public IActionResult HistorialCierres()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM cierre_diario ORDER BY fecha DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_cierre"],
                ventas = r["total_ventas"],
                gastos = r["total_gastos"],
                balance = r["balance"],
                estado = r["estado"],
                fecha = r["fecha"]
            }));
        }

        [HttpGet("grafica-ventas")]
        public IActionResult GraficaVentas()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT DATE(fecha) AS dia, SUM(total) AS total
                FROM ventas
                GROUP BY DATE(fecha)
                ORDER BY fecha");

            return Ok(rows.Select(r => new { dia = r["dia"], total = r["total"] }));
        }
    }
}

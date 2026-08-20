using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/cierres")]
    public class CierresController : ControllerBase
    {
        private readonly DbManager _db;

        public CierresController(DbManager db) => _db = db;

        private string GetDesdeStr()
        {
            var raw = _db.ExecuteScalar("SELECT IFNULL(MAX(fecha), DATE(NOW())) FROM cierre_diario");
            DateTime desde = (raw != null && raw != DBNull.Value) ? Convert.ToDateTime(raw) : DateTime.Today;
            return desde.ToString("yyyy-MM-dd HH:mm:ss");
        }

        [HttpGet("resumen")]
        public IActionResult Resumen()
        {
            string desde = GetDesdeStr();
            decimal ventas = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT IFNULL(SUM(total),0) FROM ventas WHERE estado != 'CANCELADO' AND fecha > @desde",
                new MySqlParameter("@desde", desde)));
            decimal gastos = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT IFNULL(SUM(monto),0) FROM gastos WHERE fecha > @desde",
                new MySqlParameter("@desde", desde)));

            var porMetodo = _db.ExecuteQuery(
                "SELECT IFNULL(metodo_pago,'Sin método') AS metodo, SUM(total) AS total FROM ventas WHERE estado != 'CANCELADO' AND fecha > @desde GROUP BY metodo_pago",
                new MySqlParameter("@desde", desde));

            var metodos = porMetodo.Select(r => new {
                metodo = r["metodo"]?.ToString() ?? "",
                total  = Convert.ToDecimal(r["total"])
            }).ToList();

            return Ok(new { ventas, gastos, balance = ventas - gastos, por_metodo = metodos });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Registrar([FromBody] CierreRequest request)
        {
            string desde = GetDesdeStr();
            decimal ventas = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT IFNULL(SUM(total),0) FROM ventas WHERE estado != 'CANCELADO' AND fecha > @desde",
                new MySqlParameter("@desde", desde)));
            decimal gastos = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT IFNULL(SUM(monto),0) FROM gastos WHERE fecha > @desde",
                new MySqlParameter("@desde", desde)));
            decimal balance = ventas - gastos;

            _db.ExecuteNonQuery(@"
                INSERT INTO cierre_diario (total_ventas, total_gastos, balance, estado, fecha, id_usuario, observacion)
                VALUES (@ventas, @gastos, @balance, 'CERRADO', NOW(), @usuario, @observacion)",
                new MySqlParameter("@ventas", ventas),
                new MySqlParameter("@gastos", gastos),
                new MySqlParameter("@balance", balance),
                new MySqlParameter("@usuario", request.id_usuario),
                new MySqlParameter("@observacion", request.observacion));

            return Ok(new { mensaje = "Cierre registrado" });
        }

        [HttpGet("detalle-ventas")]
        public IActionResult DetalleVentas()
        {
            string desde = GetDesdeStr();
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, IFNULL(c.nombre, 'Consumidor Final') AS cliente, v.total, IFNULL(v.metodo_pago,'—') AS metodo_pago
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE v.fecha > @desde AND v.estado != 'CANCELADO'
                ORDER BY v.fecha DESC",
                new MySqlParameter("@desde", desde));

            var ids = ventas.Select(v => Convert.ToInt32(v["id_venta"])).ToList();
            List<Dictionary<string, object>> detalles = new();
            if (ids.Count > 0)
            {
                string inClause = string.Join(",", ids);
                detalles = _db.ExecuteQuery($@"
                    SELECT d.id_venta, COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre, d.cantidad, d.precio, d.subtotal
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta IN ({inClause})
                    ORDER BY d.id_venta, d.id_detalle");
            }

            return Ok(ventas.Select(v => {
                int idV = Convert.ToInt32(v["id_venta"]);
                return new {
                    id_venta   = idV,
                    fecha      = v["fecha"],
                    cliente    = v["cliente"]?.ToString() ?? "",
                    total      = Convert.ToDecimal(v["total"]),
                    metodo_pago= v["metodo_pago"]?.ToString() ?? "",
                    productos  = detalles.Where(d => Convert.ToInt32(d["id_venta"]) == idV)
                                        .Select(d => new {
                                            nombre   = d["nombre"]?.ToString() ?? "",
                                            cantidad = Convert.ToInt32(d["cantidad"]),
                                            precio   = Convert.ToDecimal(d["precio"]),
                                            subtotal = Convert.ToDecimal(d["subtotal"])
                                        }).ToList()
                };
            }));
        }

        [HttpGet]
        public IActionResult Historial()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT c.*, u.nombre as usuario
                FROM cierre_diario c
                JOIN usuarios u ON c.id_usuario = u.id_usuario
                ORDER BY c.fecha DESC");

            return Ok(rows.Select(r => new
            {
                total_ventas = r["total_ventas"],
                total_gastos = r["total_gastos"],
                balance = r["balance"],
                fecha = r["fecha"],
                usuario = r["usuario"],
                observacion = r["observacion"]
            }));
        }
    }
}

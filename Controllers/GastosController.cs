using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using System.Text.RegularExpressions;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/gastos")]
    public class GastosController : ControllerBase
    {
        private readonly DbManager _db;
        public GastosController(DbManager db) => _db = db;

        // Valida que mes sea exactamente "YYYY-MM" para evitar SQL injection
        private static string MesSafe(string? mes) =>
            Regex.IsMatch(mes ?? "", @"^\d{4}-\d{2}$") ? mes! : "";

        [HttpGet("resumen")]
        public IActionResult Resumen([FromQuery] string? mes)
        {
            string mesFiltrado = MesSafe(mes);
            string filtroMes = string.IsNullOrEmpty(mesFiltrado)
                ? "DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"'{mesFiltrado}'";

            decimal ingresosVentas = Convert.ToDecimal(_db.ExecuteScalar($@"
                SELECT IFNULL(SUM(total), 0) FROM ventas
                WHERE DATE_FORMAT(fecha, '%Y-%m') = {filtroMes}
                AND estado != 'CANCELADO'"));

            decimal ingresosExtra = 0;
            try {
                ingresosExtra = Convert.ToDecimal(_db.ExecuteScalar($@"
                    SELECT IFNULL(SUM(monto), 0) FROM ingresos_extra
                    WHERE DATE_FORMAT(fecha, '%Y-%m') = {filtroMes}"));
            } catch { }

            decimal gastos = Convert.ToDecimal(_db.ExecuteScalar($@"
                SELECT IFNULL(SUM(monto), 0) FROM gastos
                WHERE DATE_FORMAT(fecha, '%Y-%m') = {filtroMes}"));

            return Ok(new
            {
                ingresos = ingresosVentas,
                ingresos_extra = ingresosExtra,
                gastos,
                ganancia = ingresosVentas + ingresosExtra - gastos
            });
        }

        [HttpGet("ingresos-extra")]
        public IActionResult ObtenerIngresosExtra([FromQuery] string? mes)
        {
            try
            {
                string mesFiltrado = MesSafe(mes);
                string where = string.IsNullOrEmpty(mesFiltrado)
                    ? "WHERE DATE_FORMAT(fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m')"
                    : $"WHERE DATE_FORMAT(fecha, '%Y-%m') = '{mesFiltrado}'";

                var rows = _db.ExecuteQuery($@"
                    SELECT id_ingreso, descripcion, monto, fecha
                    FROM ingresos_extra
                    {where}
                    ORDER BY fecha DESC");

                return Ok(rows.Select(r => new
                {
                    id = r["id_ingreso"],
                    descripcion = r["descripcion"],
                    monto = r["monto"],
                    fecha = r["fecha"]
                }));
            }
            catch { return Ok(Array.Empty<object>()); }
        }

        [HttpPost("ingresos-extra")]
        public IActionResult RegistrarIngresoExtra([FromBody] GastoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.descripcion) || request.monto <= 0)
                return BadRequest(new { error = "Descripción y monto requeridos" });

            _db.ExecuteNonQuery(@"
                INSERT INTO ingresos_extra (descripcion, monto, fecha)
                VALUES (@desc, @monto, NOW())",
                new MySqlParameter("@desc", request.descripcion),
                new MySqlParameter("@monto", request.monto));

            return Ok(new { mensaje = "Ingreso registrado" });
        }

        [HttpDelete("ingresos-extra/{id}")]
        public IActionResult EliminarIngresoExtra(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM ingresos_extra WHERE id_ingreso=@id",
                new MySqlParameter("@id", id));
            return Ok(new { mensaje = "Ingreso eliminado" });
        }

        [HttpGet]
        public IActionResult ObtenerGastos([FromQuery] string? mes)
        {
            string mesFiltrado = MesSafe(mes);
            string where = string.IsNullOrEmpty(mesFiltrado)
                ? "WHERE DATE_FORMAT(fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"WHERE DATE_FORMAT(fecha, '%Y-%m') = '{mesFiltrado}'";

            var rows = _db.ExecuteQuery($@"
                SELECT id_gasto, descripcion, monto, fecha
                FROM gastos
                {where}
                ORDER BY fecha DESC");

            return Ok(rows.Select(r => new
            {
                id = r["id_gasto"],
                descripcion = r["descripcion"],
                monto = r["monto"],
                fecha = r["fecha"]
            }));
        }

        [HttpGet("ingresos-por-dia")]
        public IActionResult IngresosPorDia([FromQuery] string? mes)
        {
            string mesFiltrado = MesSafe(mes);
            string filtroMes = string.IsNullOrEmpty(mesFiltrado)
                ? "DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"'{mesFiltrado}'";

            var rows = _db.ExecuteQuery($@"
                SELECT DATE(CONVERT_TZ(fecha, '+00:00', '-06:00')) AS dia, COUNT(*) AS ventas, SUM(total) AS total
                FROM ventas
                WHERE DATE_FORMAT(CONVERT_TZ(fecha, '+00:00', '-06:00'), '%Y-%m') = {filtroMes}
                AND estado != 'CANCELADO'
                GROUP BY DATE(CONVERT_TZ(fecha, '+00:00', '-06:00'))
                ORDER BY dia ASC");

            return Ok(rows.Select(r => new
            {
                dia = r["dia"],
                ventas = r["ventas"],
                total = r["total"]
            }));
        }

        [HttpGet("ventas-del-dia")]
        public IActionResult VentasDelDia([FromQuery] string dia)
        {
            if (string.IsNullOrWhiteSpace(dia)) return BadRequest(new { error = "Parámetro 'dia' requerido (YYYY-MM-DD)" });

            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, IFNULL(c.nombre,'Consumidor Final') AS cliente,
                       v.total, IFNULL(v.metodo_pago,'—') AS metodo_pago, v.estado
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE DATE(CONVERT_TZ(v.fecha, '+00:00', '-06:00')) = @dia AND v.estado != 'CANCELADO'
                ORDER BY v.fecha ASC",
                new MySqlParameter("@dia", dia));

            var ids = ventas.Select(v => Convert.ToInt32(v["id_venta"])).ToList();
            List<Dictionary<string, object>> detalles = new();
            if (ids.Count > 0)
            {
                string inClause = string.Join(",", ids);
                detalles = _db.ExecuteQuery($@"
                    SELECT d.id_venta, COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre,
                           d.cantidad, d.precio, d.subtotal
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta IN ({inClause})
                    ORDER BY d.id_venta, d.id_detalle");
            }

            return Ok(ventas.Select(v => {
                int idV = Convert.ToInt32(v["id_venta"]);
                return new {
                    id_venta    = idV,
                    fecha       = v["fecha"],
                    cliente     = v["cliente"]?.ToString() ?? "",
                    total       = Convert.ToDecimal(v["total"]),
                    metodo_pago = v["metodo_pago"]?.ToString() ?? "",
                    estado      = v["estado"]?.ToString() ?? "",
                    productos   = detalles.Where(d => Convert.ToInt32(d["id_venta"]) == idV)
                                         .Select(d => new {
                                             nombre   = d["nombre"]?.ToString() ?? "",
                                             cantidad = Convert.ToInt32(d["cantidad"]),
                                             precio   = Convert.ToDecimal(d["precio"]),
                                             subtotal = Convert.ToDecimal(d["subtotal"])
                                         }).ToList()
                };
            }));
        }

        [HttpPost]
        public IActionResult RegistrarGasto([FromBody] GastoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.descripcion) || request.monto <= 0)
                return BadRequest(new { error = "Descripción y monto requeridos" });

            _db.ExecuteNonQuery(@"
                INSERT INTO gastos (descripcion, monto, fecha)
                VALUES (@desc, @monto, NOW())",
                new MySqlParameter("@desc", request.descripcion),
                new MySqlParameter("@monto", request.monto));

            return Ok(new { mensaje = "Gasto registrado" });
        }

        [HttpDelete("{id}")]
        public IActionResult EliminarGasto(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM gastos WHERE id_gasto=@id",
                new MySqlParameter("@id", id));
            return Ok(new { mensaje = "Gasto eliminado" });
        }
    }

    public class GastoRequest
    {
        public string descripcion { get; set; }
        public decimal monto { get; set; }
    }
}

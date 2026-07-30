using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/gastos")]
    public class GastosController : ControllerBase
    {
        private readonly DbManager _db;
        public GastosController(DbManager db) => _db = db;

        [HttpGet("resumen")]
        public IActionResult Resumen([FromQuery] string? mes)
        {
            string filtroMes = string.IsNullOrEmpty(mes)
                ? "DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"'{mes}'";

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
                string where = string.IsNullOrEmpty(mes)
                    ? "WHERE DATE_FORMAT(fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m')"
                    : $"WHERE DATE_FORMAT(fecha, '%Y-%m') = '{mes}'";

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
            string where = string.IsNullOrEmpty(mes)
                ? "WHERE DATE_FORMAT(fecha, '%Y-%m') = DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"WHERE DATE_FORMAT(fecha, '%Y-%m') = '{mes}'";

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
            string filtroMes = string.IsNullOrEmpty(mes)
                ? "DATE_FORMAT(CURDATE(), '%Y-%m')"
                : $"'{mes}'";

            var rows = _db.ExecuteQuery($@"
                SELECT DATE(fecha) AS dia, COUNT(*) AS ventas, SUM(total) AS total
                FROM ventas
                WHERE DATE_FORMAT(fecha, '%Y-%m') = {filtroMes}
                AND estado != 'CANCELADO'
                GROUP BY DATE(fecha)
                ORDER BY dia ASC");

            return Ok(rows.Select(r => new
            {
                dia = r["dia"],
                ventas = r["ventas"],
                total = r["total"]
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

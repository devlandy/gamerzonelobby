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

        [HttpGet("resumen")]
        public IActionResult Resumen()
        {
            decimal ventas = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(total),0) FROM ventas WHERE estado='PAGADO'"));
            decimal gastos = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(monto),0) FROM gastos"));

            return Ok(new { ventas, gastos, balance = ventas - gastos });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Registrar([FromBody] CierreRequest request)
        {
            decimal ventas = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(total),0) FROM ventas WHERE estado='PAGADO'"));
            decimal gastos = Convert.ToDecimal(_db.ExecuteScalar("SELECT IFNULL(SUM(monto),0) FROM gastos"));
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

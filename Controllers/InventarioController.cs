using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/inventario")]
    public class InventarioController : ControllerBase
    {
        private readonly DbManager _db;

        public InventarioController(DbManager db) => _db = db;

        [HttpGet("alertas")]
        public IActionResult Alertas()
        {
            var agotados = _db.ExecuteQuery(
                "SELECT nombre, stock FROM productos WHERE stock <= 0 AND controla_stock = 1 AND activo = 1")
                .Select(r => new { nombre = r["nombre"], stock = r["stock"] });

            var bajos = _db.ExecuteQuery(
                "SELECT nombre, stock FROM productos WHERE stock > 0 AND stock <= 5 AND controla_stock = 1 AND activo = 1")
                .Select(r => new { nombre = r["nombre"], stock = r["stock"] });

            return Ok(new { agotados, por_terminar = bajos });
        }

        [HttpGet("historial")]
        public IActionResult Historial()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT p.nombre, h.tipo_movimiento, h.cantidad, h.observacion, h.fecha
                FROM historial_inventario h
                JOIN productos p ON h.id_producto = p.id_producto
                ORDER BY h.fecha DESC");

            return Ok(rows.Select(r => new
            {
                producto = r["nombre"],
                tipo = r["tipo_movimiento"],
                cantidad = r["cantidad"],
                observacion = r["observacion"],
                fecha = r["fecha"]
            }));
        }
    }
}

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
                "SELECT nombre, stock FROM productos WHERE stock <= 0 AND controla_stock = 1 AND activo = 1 AND precio_venta > 0")
                .Select(r => new { nombre = r["nombre"], stock = r["stock"] });

            var bajos = _db.ExecuteQuery(
                "SELECT nombre, stock FROM productos WHERE stock > 0 AND stock <= 5 AND controla_stock = 1 AND activo = 1 AND precio_venta > 0")
                .Select(r => new { nombre = r["nombre"], stock = r["stock"] });

            return Ok(new { agotados, por_terminar = bajos });
        }

        [HttpGet("historial")]
        public IActionResult Historial([FromQuery] string? producto, [FromQuery] string? tipo, [FromQuery] string? mes)
        {
            var where = new List<string>();
            var p = new List<MySql.Data.MySqlClient.MySqlParameter>();

            if (!string.IsNullOrEmpty(producto))
            {
                where.Add("p.nombre LIKE @prod");
                p.Add(new MySql.Data.MySqlClient.MySqlParameter("@prod", $"%{producto}%"));
            }
            if (!string.IsNullOrEmpty(tipo))
            {
                where.Add("h.tipo_movimiento = @tipo");
                p.Add(new MySql.Data.MySqlClient.MySqlParameter("@tipo", tipo));
            }
            if (!string.IsNullOrEmpty(mes))
            {
                where.Add("DATE_FORMAT(h.fecha, '%Y-%m') = @mes");
                p.Add(new MySql.Data.MySqlClient.MySqlParameter("@mes", mes));
            }

            string filtro = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            // Excluir preparados al momento (controla_stock = 0)
            if (where.Count > 0) filtro += " AND p.controla_stock = 1";
            else filtro = "WHERE p.controla_stock = 1";

            var rows = _db.ExecuteQuery($@"
                SELECT p.nombre, h.tipo_movimiento, h.cantidad, h.observacion, h.fecha, h.usuario
                FROM historial_inventario h
                JOIN productos p ON h.id_producto = p.id_producto
                {filtro}
                ORDER BY h.fecha DESC
                LIMIT 300",
                p.ToArray());

            return Ok(rows.Select(r => new
            {
                producto    = r["nombre"],
                tipo        = r["tipo_movimiento"],
                cantidad    = r["cantidad"],
                observacion = r["observacion"],
                usuario     = r["usuario"],
                fecha       = r["fecha"]
            }));
        }
    }
}

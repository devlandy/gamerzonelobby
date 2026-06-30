using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/factura")]
    public class FacturaController : ControllerBase
    {
        private readonly DbManager _db;

        public FacturaController(DbManager db) => _db = db;

        [HttpGet("{id}")]
        public IActionResult ObtenerFactura(int id)
        {
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, v.total, v.forma_cobro, v.metodo_pago, c.nombre AS cliente
                FROM ventas v
                JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE v.id_venta = @id",
                new MySqlParameter("@id", id));

            if (ventas.Count == 0)
                return NotFound("Venta no encontrada");

            var v = ventas[0];

            var productos = _db.ExecuteQuery(@"
                SELECT p.nombre, d.cantidad, d.precio, d.subtotal
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto = p.id_producto
                WHERE d.id_venta = @id",
                new MySqlParameter("@id", id))
                .Select(r => new
                {
                    nombre = r["nombre"],
                    cantidad = r["cantidad"],
                    precio = r["precio"],
                    subtotal = r["subtotal"]
                });

            return Ok(new
            {
                id = v["id_venta"],
                fecha = v["fecha"],
                cliente = v["cliente"],
                total = v["total"],
                forma_cobro = v["forma_cobro"],
                metodo_pago = v["metodo_pago"],
                productos
            });
        }

        [HttpPost]
        public IActionResult CrearFactura([FromBody] FacturaRequest request)
        {
            var idFactura = _db.ExecuteScalar(@"
                INSERT INTO facturas (id_venta, nit, nombre, direccion)
                VALUES (@id_venta, @nit, @nombre, @direccion);
                SELECT LAST_INSERT_ID();",
                new MySqlParameter("@id_venta", request.id_venta),
                new MySqlParameter("@nit", request.nit),
                new MySqlParameter("@nombre", request.nombre),
                new MySqlParameter("@direccion", request.direccion));

            return Ok(new { mensaje = "Factura guardada", id_factura = Convert.ToInt32(idFactura) });
        }

        [HttpGet]
        public IActionResult HistorialFacturas()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT f.id_factura, f.fecha, f.nit, f.nombre, v.total
                FROM facturas f
                JOIN ventas v ON f.id_venta = v.id_venta
                ORDER BY f.fecha DESC");

            return Ok(rows.Select(r => new
            {
                id_factura = r["id_factura"],
                fecha = r["fecha"],
                nit = r["nit"],
                nombre = r["nombre"],
                total = r["total"]
            }));
        }
    }
}

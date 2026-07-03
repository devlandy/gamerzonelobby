using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/ventas")]
    public class VentasController : ControllerBase
    {
        private readonly DbManager _db;

        public VentasController(DbManager db) => _db = db;

        [HttpPost]
        public IActionResult RegistrarVenta([FromBody] VentaRequest request)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            var transaction = conn.BeginTransaction();

            try
            {
                decimal total = request.productos.Sum(p => p.precio * p.cantidad);

                string estado = request.metodo_pago == "PENDIENTE" ? "PENDIENTE" : "PAGADO";
                string formaCobro = estado == "PENDIENTE" ? "PENDIENTE" : "PAGADO";

                var cmdVenta = new MySqlCommand(@"
                    INSERT INTO ventas (id_cliente, id_usuario, tipo, numero_orden, nombre_orden, forma_cobro, metodo_pago, total, estado, observacion, fecha)
                    VALUES (@cliente, @usuario, 'PRODUCTO', @numero, @nombre, @forma, @metodo, @total, @estado, @obs, NOW());
                    SELECT LAST_INSERT_ID();", conn, transaction);

                cmdVenta.Parameters.AddWithValue("@cliente", (object?)request.id_cliente ?? DBNull.Value);
                cmdVenta.Parameters.AddWithValue("@usuario", request.id_usuario);
                cmdVenta.Parameters.AddWithValue("@numero", request.numero_orden ?? "000");
                cmdVenta.Parameters.AddWithValue("@nombre", request.nombre_orden ?? "ORDEN POS");
                cmdVenta.Parameters.AddWithValue("@forma", formaCobro);
                cmdVenta.Parameters.AddWithValue("@metodo", request.metodo_pago);
                cmdVenta.Parameters.AddWithValue("@total", total);
                cmdVenta.Parameters.AddWithValue("@estado", estado);
                cmdVenta.Parameters.AddWithValue("@obs", request.observacion ?? "");

                int idVenta = Convert.ToInt32(cmdVenta.ExecuteScalar());

                foreach (var p in request.productos)
                {
                    // id_producto = 0 indica un servicio de consola (no es un producto del inventario)
                    object idProducto = p.id_producto > 0 ? p.id_producto : DBNull.Value;

                    var cmdDetalle = new MySqlCommand(@"
                        INSERT INTO detalle_ventas (id_venta, id_producto, nombre, cantidad, precio, subtotal)
                        VALUES (@venta, @producto, @nombre, @cantidad, @precio, @subtotal)", conn, transaction);
                    cmdDetalle.Parameters.AddWithValue("@venta", idVenta);
                    cmdDetalle.Parameters.AddWithValue("@producto", idProducto);
                    cmdDetalle.Parameters.AddWithValue("@nombre", (object?)p.nombre ?? DBNull.Value);
                    cmdDetalle.Parameters.AddWithValue("@cantidad", p.cantidad);
                    cmdDetalle.Parameters.AddWithValue("@precio", p.precio);
                    cmdDetalle.Parameters.AddWithValue("@subtotal", p.precio * p.cantidad);
                    cmdDetalle.ExecuteNonQuery();

                    if (p.id_producto > 0)
                    {
                        var cmdStock = new MySqlCommand(
                            "UPDATE productos SET stock = stock - @cantidad WHERE id_producto=@producto AND controla_stock = 1",
                            conn, transaction);
                        cmdStock.Parameters.AddWithValue("@cantidad", p.cantidad);
                        cmdStock.Parameters.AddWithValue("@producto", p.id_producto);
                        cmdStock.ExecuteNonQuery();

                        var cmdHist = new MySqlCommand(@"
                            INSERT INTO historial_inventario (id_producto, tipo_movimiento, cantidad, observacion, usuario, fecha)
                            VALUES (@producto, 'SALIDA', @cantidad, @obs, @usuario, NOW())", conn, transaction);
                        cmdHist.Parameters.AddWithValue("@producto", p.id_producto);
                        cmdHist.Parameters.AddWithValue("@cantidad", p.cantidad);
                        cmdHist.Parameters.AddWithValue("@obs", "Venta POS");
                        cmdHist.Parameters.AddWithValue("@usuario", request.id_usuario);
                        cmdHist.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return Ok(new { mensaje = "Venta registrada correctamente", id_venta = idVenta, total });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("pendientes")]
        public IActionResult VentasPendientes()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.numero_orden, v.nombre_orden, v.tipo, v.total, v.estado, v.fecha, c.nombre AS cliente
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE v.estado='PENDIENTE'
                ORDER BY v.fecha ASC");

            return Ok(rows.Select(r => new
            {
                id = r["id_venta"],
                numero = r["numero_orden"],
                nombre_orden = r["nombre_orden"],
                tipo = r["tipo"],
                total = r["total"],
                estado = r["estado"],
                cliente = r["cliente"],
                fecha = r["fecha"]
            }));
        }

        [HttpPut("{id}")]
        public IActionResult EditarVenta(int id, [FromBody] EditarVentaRequest request)
        {
            _db.ExecuteNonQuery(@"
                UPDATE ventas SET forma_cobro=@forma, metodo_pago=@metodo, estado='PAGADO', observacion=@obs
                WHERE id_venta=@id",
                new MySqlParameter("@forma", request.forma_cobro),
                new MySqlParameter("@metodo", request.metodo_pago),
                new MySqlParameter("@obs", request.observacion ?? ""),
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Venta actualizada" });
        }

        [HttpGet("{id}")]
        public IActionResult ObtenerVenta(int id)
        {
            var ventas = _db.ExecuteQuery("SELECT * FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));

            if (ventas.Count == 0)
                return NotFound();

            var v = ventas[0];

            var productos = _db.ExecuteQuery(@"
                SELECT p.nombre, d.cantidad, d.precio, d.subtotal
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto=p.id_producto
                WHERE d.id_venta=@id",
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
                venta = new
                {
                    id = v["id_venta"],
                    total = v["total"],
                    estado = v["estado"],
                    metodo = v["metodo_pago"],
                    fecha = v["fecha"]
                },
                productos
            });
        }
    }
}

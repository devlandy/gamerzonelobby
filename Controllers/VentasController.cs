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
                decimal subtotal = request.productos.Sum(p => p.precio * p.cantidad);
                decimal total = request.descuento_pct > 0
                    ? Math.Round(subtotal * (1 - request.descuento_pct / 100), 2)
                    : subtotal;

                string estado = request.metodo_pago == "PENDIENTE" ? "PENDIENTE" : "PAGADO";
                string formaCobro = estado == "PENDIENTE" ? "PENDIENTE" : "PAGADO";

                var cmdVenta = new MySqlCommand(@"
                    INSERT INTO ventas (id_cliente, id_usuario, tipo, numero_orden, nombre_orden, forma_cobro, metodo_pago, total, descuento_pct, estado, observacion, fecha)
                    VALUES (@cliente, @usuario, 'PRODUCTO', @numero, @nombre, @forma, @metodo, @total, @descuento, @estado, @obs, NOW());
                    SELECT LAST_INSERT_ID();", conn, transaction);

                cmdVenta.Parameters.AddWithValue("@cliente", (object?)request.id_cliente ?? DBNull.Value);
                cmdVenta.Parameters.AddWithValue("@usuario", request.id_usuario);
                cmdVenta.Parameters.AddWithValue("@numero", request.numero_orden ?? "000");
                cmdVenta.Parameters.AddWithValue("@nombre", request.nombre_orden ?? "ORDEN POS");
                cmdVenta.Parameters.AddWithValue("@forma", formaCobro);
                cmdVenta.Parameters.AddWithValue("@metodo", request.metodo_pago);
                cmdVenta.Parameters.AddWithValue("@total", total);
                cmdVenta.Parameters.AddWithValue("@descuento", request.descuento_pct);
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

                // Puntos automáticos si hay cliente vinculado y la venta está pagada
                if (request.id_cliente.HasValue && estado == "PAGADO")
                {
                    try
                    {
                        // CONSUMO: 0.05 puntos por cada Q1 gastado en comida/productos (NO consola)
                        decimal totalComida = request.productos
                            .Where(p => p.id_producto > 0)
                            .Sum(p => p.precio * p.cantidad);
                        decimal puntosConsumo = Math.Round(totalComida * 0.05m, 2);
                        if (puntosConsumo > 0)
                        {
                            _db.ExecuteNonQuery(@"
                                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                                VALUES (@cliente, 'CONSUMO', @puntos, 'COMPRA')",
                                new MySqlParameter("@cliente", request.id_cliente.Value),
                                new MySqlParameter("@puntos", puntosConsumo));
                        }

                        // JUEGO: 5 puntos por cada hora de consola pagada (proporcional por minutos)
                        int minutosJuego = request.productos
                            .Where(p => p.id_producto == 0)
                            .Sum(p => p.minutos ?? 0);
                        decimal puntosJuego = Math.Round((minutosJuego / 60m) * 5m, 2);
                        if (puntosJuego > 0)
                        {
                            _db.ExecuteNonQuery(@"
                                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                                VALUES (@cliente, 'JUEGO', @puntos, 'CONSOLA')",
                                new MySqlParameter("@cliente", request.id_cliente.Value),
                                new MySqlParameter("@puntos", puntosJuego));
                        }
                    }
                    catch { /* no interrumpir la venta si falla el registro de puntos */ }
                }

                return Ok(new { mensaje = "Venta registrada correctamente", id_venta = idVenta, total });
            }
            catch
            {
                transaction.Rollback();
                return BadRequest(new { error = "No se pudo registrar la venta. Intenta de nuevo." });
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
                id_venta = Convert.ToInt32(r["id_venta"]),
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

        [HttpPatch("{id}/cancelar")]
        public IActionResult CancelarVenta(int id, [FromBody] CancelarVentaRequest request)
        {
            var venta = _db.ExecuteQuery("SELECT estado FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));

            if (venta.Count == 0) return NotFound(new { error = "Venta no encontrada" });
            if (venta[0]["estado"].ToString() == "CANCELADO")
                return BadRequest(new { error = "La venta ya está cancelada" });

            _db.ExecuteNonQuery(@"
                UPDATE ventas SET estado='CANCELADO', observacion=@obs WHERE id_venta=@id",
                new MySqlParameter("@obs", request.motivo ?? ""),
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Venta cancelada" });
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

    public class CancelarVentaRequest
    {
        public string? motivo { get; set; }
    }
}

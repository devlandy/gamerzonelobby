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

            // Devolver stock de productos cancelados
            _db.ExecuteNonQuery(@"
                UPDATE productos p
                JOIN detalle_ventas d ON p.id_producto = d.id_producto
                SET p.stock = p.stock + d.cantidad
                WHERE d.id_venta = @id AND p.controla_stock = 1",
                new MySqlParameter("@id", id));

            // Descontar puntos al cliente si tiene uno asignado
            var ventaData = _db.ExecuteQuery("SELECT id_cliente FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));
            if (ventaData.Count > 0 && ventaData[0]["id_cliente"] != DBNull.Value && ventaData[0]["id_cliente"] != null)
            {
                int idCliente = Convert.ToInt32(ventaData[0]["id_cliente"]);
                // Obtener fecha de la venta para buscar puntos registrados en ese momento
                var fechaVenta = _db.ExecuteQuery("SELECT fecha FROM ventas WHERE id_venta=@id",
                    new MySqlParameter("@id", id));
                if (fechaVenta.Count > 0)
                {
                    string fechaStr = Convert.ToDateTime(fechaVenta[0]["fecha"]).ToString("yyyy-MM-dd HH:mm:ss");
                    var puntosVenta = _db.ExecuteQuery(@"
                        SELECT tipo, SUM(puntos) AS total FROM historial_puntos
                        WHERE id_cliente=@idC AND (motivo='COMPRA' OR motivo='CONSOLA')
                          AND ABS(TIMESTAMPDIFF(MINUTE, fecha, @fecha)) <= 2
                        GROUP BY tipo",
                        new MySqlParameter("@idC", idCliente),
                        new MySqlParameter("@fecha", fechaStr));

                    foreach (var p in puntosVenta)
                    {
                        decimal puntos = Convert.ToDecimal(p["total"]);
                        string tipo = p["tipo"]?.ToString() ?? "CONSUMO";
                        if (puntos > 0)
                            _db.ExecuteNonQuery(@"
                                INSERT INTO historial_puntos (id_cliente, tipo, puntos, motivo)
                                VALUES (@idC, @tipo, @puntos, @motivo)",
                                new MySqlParameter("@idC", idCliente),
                                new MySqlParameter("@tipo", tipo),
                                new MySqlParameter("@puntos", -puntos),
                                new MySqlParameter("@motivo", $"Cancelación venta #{id}"));
                    }
                }
            }

            return Ok(new { mensaje = "Venta cancelada" });
        }

        [HttpPatch("{id}/entregar")]
        public IActionResult EntregarOrden(int id)
        {
            var venta = _db.ExecuteQuery("SELECT estado FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));
            if (venta.Count == 0) return NotFound(new { error = "Orden no encontrada" });
            if (venta[0]["estado"].ToString() == "CANCELADO")
                return BadRequest(new { error = "La orden está cancelada" });
            _db.ExecuteNonQuery("UPDATE ventas SET entregado=1 WHERE id_venta=@id",
                new MySqlParameter("@id", id));
            return Ok(new { mensaje = "Orden marcada como entregada" });
        }

        [HttpGet("ordenes")]
        public IActionResult ListarOrdenes([FromQuery] string estado = "TODAS")
        {
            // Solo mostrar órdenes que tienen al menos un producto físico (no solo consola)
            // id_producto > 0 identifica productos del catálogo (comida, bebidas, etc.)
            string where = estado == "TODAS"
                ? "WHERE v.estado != 'CANCELADO' AND EXISTS (SELECT 1 FROM detalle_ventas d WHERE d.id_venta = v.id_venta AND d.id_producto > 0)"
                : "WHERE v.estado != 'CANCELADO' AND NOT (v.estado = 'PAGADO' AND v.entregado = 1) AND EXISTS (SELECT 1 FROM detalle_ventas d WHERE d.id_venta = v.id_venta AND d.id_producto > 0)";
            var rows = _db.ExecuteQuery($@"
                SELECT v.id_venta,
                       (SELECT COUNT(*) FROM ventas v2
                        WHERE v2.id_venta <= v.id_venta
                        AND EXISTS (SELECT 1 FROM detalle_ventas d2 WHERE d2.id_venta = v2.id_venta AND d2.id_producto > 0)
                       ) AS numero_orden,
                       v.nombre_orden, v.total, v.estado,
                       v.metodo_pago, v.fecha, v.entregado, IFNULL(c.nombre,'Sin cliente') AS cliente
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                {where}
                ORDER BY v.fecha DESC
                LIMIT 100",
                estado != "TODAS" ? new MySqlParameter[] { new MySqlParameter("@estado", estado) } : new MySqlParameter[0]);

            var ids = rows.Select(r => Convert.ToInt32(r["id_venta"])).ToList();
            var detalles = ids.Count > 0 ? _db.ExecuteQuery($@"
                SELECT d.id_detalle, d.id_venta, IFNULL(p.nombre, d.nombre) AS nombre, d.cantidad, d.precio, d.subtotal, d.entregado, d.cobrado
                FROM detalle_ventas d
                LEFT JOIN productos p ON d.id_producto = p.id_producto
                WHERE d.id_venta IN ({string.Join(",", ids)})") : new List<Dictionary<string, object>>();

            return Ok(rows.Select(r => new {
                id_venta     = Convert.ToInt32(r["id_venta"]),
                nombre_orden = r["nombre_orden"]?.ToString() ?? "",
                numero_orden = r["numero_orden"]?.ToString() ?? "",
                cliente      = r["cliente"]?.ToString() ?? "",
                total        = Convert.ToDecimal(r["total"]),
                estado       = r["estado"]?.ToString() ?? "",
                metodo_pago  = r["metodo_pago"]?.ToString() ?? "",
                fecha        = r["fecha"],
                entregado    = Convert.ToInt32(r["entregado"]) == 1,
                productos    = detalles
                    .Where(d => Convert.ToInt32(d["id_venta"]) == Convert.ToInt32(r["id_venta"]))
                    .Select(d => new {
                        id_detalle = Convert.ToInt32(d["id_detalle"]),
                        nombre     = d["nombre"]?.ToString() ?? "",
                        cantidad   = Convert.ToInt32(d["cantidad"]),
                        precio     = Convert.ToDecimal(d["precio"]),
                        subtotal   = Convert.ToDecimal(d["subtotal"]),
                        entregado  = Convert.ToInt32(d["entregado"]) == 1,
                        cobrado    = Convert.ToInt32(d["cobrado"]) == 1
                    })
            }));
        }

        [HttpPatch("{id}/detalle/{idDetalle}/entregar")]
        public IActionResult EntregarProducto(int id, int idDetalle)
        {
            _db.ExecuteNonQuery(
                "UPDATE detalle_ventas SET entregado = 1 WHERE id_detalle=@d AND id_venta=@v",
                new MySqlParameter("@d", idDetalle),
                new MySqlParameter("@v", id));

            // Si todos los productos están entregados, marcar la orden como entregada
            int pendientes = Convert.ToInt32(_db.ExecuteScalar(
                "SELECT COUNT(*) FROM detalle_ventas WHERE id_venta=@v AND entregado=0",
                new MySqlParameter("@v", id)));

            if (pendientes == 0)
                _db.ExecuteNonQuery("UPDATE ventas SET entregado=1 WHERE id_venta=@v",
                    new MySqlParameter("@v", id));

            return Ok(new { mensaje = "Producto entregado", orden_entregada = pendientes == 0 });
        }

        [HttpDelete("{id}/detalle/{idDetalle}")]
        public IActionResult EliminarProductoOrden(int id, int idDetalle)
        {
            var row = _db.ExecuteQuery(
                "SELECT subtotal FROM detalle_ventas WHERE id_detalle=@d AND id_venta=@v",
                new MySqlParameter("@d", idDetalle),
                new MySqlParameter("@v", id));

            if (row.Count == 0) return NotFound();
            decimal subtotal = Convert.ToDecimal(row[0]["subtotal"]);

            _db.ExecuteNonQuery(
                "DELETE FROM detalle_ventas WHERE id_detalle=@d",
                new MySqlParameter("@d", idDetalle));

            _db.ExecuteNonQuery(
                "UPDATE ventas SET total = GREATEST(0, total - @s) WHERE id_venta=@v",
                new MySqlParameter("@s", subtotal),
                new MySqlParameter("@v", id));

            return Ok(new { mensaje = "Producto eliminado" });
        }

        [HttpPost("{id}/agregar")]
        public IActionResult AgregarAOrden(int id, [FromBody] AgregarOrdenRequest req)
        {
            var venta = _db.ExecuteQuery("SELECT estado, total FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));
            if (venta.Count == 0) return NotFound(new { error = "Orden no encontrada" });
            if (venta[0]["estado"].ToString() == "CANCELADO")
                return BadRequest(new { error = "La orden está cancelada" });

            using var conn = _db.GetConnection();
            conn.Open();
            var tr = conn.BeginTransaction();
            try
            {
                // Si la orden ya estaba pagada, marcar productos existentes como cobrados
                if (venta[0]["estado"].ToString() == "PAGADO")
                {
                    var markCobrado = new MySqlCommand("UPDATE detalle_ventas SET cobrado=1 WHERE id_venta=@v", conn, tr);
                    markCobrado.Parameters.AddWithValue("@v", id);
                    markCobrado.ExecuteNonQuery();
                }

                decimal nuevoCosto = 0;
                foreach (var p in req.productos)
                {
                    object idProd = p.id_producto > 0 ? p.id_producto : DBNull.Value;
                    var cmd = new MySqlCommand(@"
                        INSERT INTO detalle_ventas (id_venta, id_producto, nombre, cantidad, precio, subtotal)
                        VALUES (@v, @p, @n, @c, @pr, @s)", conn, tr);
                    cmd.Parameters.AddWithValue("@v", id);
                    cmd.Parameters.AddWithValue("@p", idProd);
                    cmd.Parameters.AddWithValue("@n", p.nombre ?? "");
                    cmd.Parameters.AddWithValue("@c", p.cantidad);
                    cmd.Parameters.AddWithValue("@pr", p.precio);
                    cmd.Parameters.AddWithValue("@s", p.precio * p.cantidad);
                    cmd.ExecuteNonQuery();

                    if (p.id_producto > 0)
                    {
                        var upd = new MySqlCommand("UPDATE productos SET stock = stock - @c WHERE id_producto=@p AND controla_stock=1", conn, tr);
                        upd.Parameters.AddWithValue("@c", p.cantidad);
                        upd.Parameters.AddWithValue("@p", p.id_producto);
                        upd.ExecuteNonQuery();
                    }
                    nuevoCosto += p.precio * p.cantidad;
                }
                // Actualizar total y resetear entregado=0 para que vuelva a aparecer como POR ENTREGAR
                var upTotal = new MySqlCommand("UPDATE ventas SET total = total + @extra, entregado = 0, estado = 'PENDIENTE', forma_cobro = 'PENDIENTE' WHERE id_venta=@id", conn, tr);
                upTotal.Parameters.AddWithValue("@extra", nuevoCosto);
                upTotal.Parameters.AddWithValue("@id", id);
                upTotal.ExecuteNonQuery();
                tr.Commit();
                return Ok(new { mensaje = "Productos agregados a la orden" });
            }
            catch { tr.Rollback(); return BadRequest(new { error = "Error al agregar productos" }); }
        }

        [HttpPatch("{id}/cobrar")]
        public IActionResult CobrarOrden(int id, [FromBody] CobrarOrdenRequest req)
        {
            var venta = _db.ExecuteQuery("SELECT estado FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));
            if (venta.Count == 0) return NotFound(new { error = "Orden no encontrada" });
            if (venta[0]["estado"].ToString() == "CANCELADO")
                return BadRequest(new { error = "La orden está cancelada" });

            _db.ExecuteNonQuery(@"
                UPDATE ventas SET estado='PAGADO', forma_cobro='PAGADO', metodo_pago=@metodo WHERE id_venta=@id",
                new MySqlParameter("@metodo", req.metodo_pago ?? "EFECTIVO"),
                new MySqlParameter("@id", id));

            _db.ExecuteNonQuery("UPDATE detalle_ventas SET cobrado=1 WHERE id_venta=@id",
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Orden cobrada" });
        }

        [HttpPatch("{id}/reactivar")]
        public IActionResult ReactivarVenta(int id)
        {
            var venta = _db.ExecuteQuery("SELECT estado FROM ventas WHERE id_venta=@id",
                new MySqlParameter("@id", id));

            if (venta.Count == 0) return NotFound(new { error = "Venta no encontrada" });
            if (venta[0]["estado"].ToString() != "CANCELADO")
                return BadRequest(new { error = "La venta no está cancelada" });

            _db.ExecuteNonQuery(@"
                UPDATE ventas SET estado='PAGADO', observacion='' WHERE id_venta=@id",
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Venta reactivada" });
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

    public class AgregarOrdenRequest
    {
        public List<ProductoItem> productos { get; set; } = new();
    }

    public class ProductoItem
    {
        public int id_producto { get; set; }
        public string? nombre { get; set; }
        public int cantidad { get; set; }
        public decimal precio { get; set; }
    }

    public class CobrarOrdenRequest
    {
        public string? metodo_pago { get; set; }
    }
}

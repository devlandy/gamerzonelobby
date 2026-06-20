using GamerZoneAPI.Data;
using GamerZoneAPI.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/factura")]
    public class FacturaController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        [HttpGet("{id}")]
        public IActionResult ObtenerFactura(int id)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                // =========================
                // DATOS PRINCIPALES
                // =========================
                string ventaQuery = @"SELECT v.id_venta, v.fecha, v.total, v.forma_cobro, v.metodo_pago,
                                      c.nombre AS cliente
                                      FROM ventas v
                                      JOIN clientes c ON v.id_cliente = c.id_cliente
                                      WHERE v.id_venta = @id";

                MySqlCommand cmdVenta = new MySqlCommand(ventaQuery, conn);
                cmdVenta.Parameters.AddWithValue("@id", id);

                var readerVenta = cmdVenta.ExecuteReader();

                if (!readerVenta.Read())
                {
                    return NotFound("Venta no encontrada");
                }

                var factura = new
                {
                    id = readerVenta["id_venta"],
                    fecha = readerVenta["fecha"],
                    cliente = readerVenta["cliente"],
                    total = readerVenta["total"],
                    forma_cobro = readerVenta["forma_cobro"],
                    metodo_pago = readerVenta["metodo_pago"],
                    productos = new List<object>()
                };

                readerVenta.Close();

                // =========================
                // DETALLE PRODUCTOS
                // =========================
                string detalleQuery = @"SELECT p.nombre, d.cantidad, d.precio, d.subtotal
                                       FROM detalle_ventas d
                                       JOIN productos p ON d.id_producto = p.id_producto
                                       WHERE d.id_venta = @id";

                MySqlCommand cmdDetalle = new MySqlCommand(detalleQuery, conn);
                cmdDetalle.Parameters.AddWithValue("@id", id);

                var readerDetalle = cmdDetalle.ExecuteReader();

                List<object> listaProductos = new List<object>();

                while (readerDetalle.Read())
                {
                    listaProductos.Add(new
                    {
                        nombre = readerDetalle["nombre"],
                        cantidad = readerDetalle["cantidad"],
                        precio = readerDetalle["precio"],
                        subtotal = readerDetalle["subtotal"]
                    });
                }

                readerDetalle.Close();

                return Ok(new
                {
                    factura.id,
                    factura.fecha,
                    factura.cliente,
                    factura.total,
                    factura.forma_cobro,
                    factura.metodo_pago,
                    productos = listaProductos
                });
            }
        }


            // ======================
            // CREAR FACTURA
            // ======================
            [HttpPost]
            public IActionResult CrearFactura(
            [FromBody] FacturaRequest request)
            {
                using (var conn =
                conexion.GetConnection())
                {
                    conn.Open();

                    string query = @"

INSERT INTO facturas
(
id_venta,
nit,
nombre,
direccion
)

VALUES
(
@id_venta,
@nit,
@nombre,
@direccion
);

";

                    MySqlCommand cmd =
                    new MySqlCommand(query, conn);

                    cmd.Parameters.AddWithValue(
                    "@id_venta",
                    request.id_venta);

                    cmd.Parameters.AddWithValue(
                    "@nit",
                    request.nit);

                    cmd.Parameters.AddWithValue(
                    "@nombre",
                    request.nombre);

                    cmd.Parameters.AddWithValue(
                    "@direccion",
                    request.direccion);

                    cmd.ExecuteNonQuery();

                    return Ok(new
                    {
                        mensaje =
                        "Factura guardada"
                    });
                }
            }

        // ======================
        // HISTORIAL FACTURAS
        // ======================
        [HttpGet]
        public IActionResult HistorialFacturas()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

SELECT
f.id_factura,
f.fecha,
f.nit,
f.nombre,
v.total

FROM facturas f

JOIN ventas v
ON f.id_venta = v.id_venta

ORDER BY f.fecha DESC

";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                var reader =
                cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        id_factura =
                        reader["id_factura"],

                        fecha =
                        reader["fecha"],

                        nit =
                        reader["nit"],

                        nombre =
                        reader["nombre"],

                        total =
                        reader["total"]
                    });
                }

                return Ok(lista);
            }
        }
    }
}


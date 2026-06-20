using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/ventas")]
    public class VentasController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        // ======================
        // REGISTRAR VENTA POS
        // ======================
        [HttpPost]
        public IActionResult RegistrarVenta(
        [FromBody] VentaRequest request)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                var transaction =
                conn.BeginTransaction();

                try
                {
                    decimal total = 0;

                    // ======================
                    // CALCULAR TOTAL
                    // ======================

                    foreach (var p in request.productos)
                    {
                        total +=
                        p.precio * p.cantidad;
                    }

                    // ======================
                    // ESTADO
                    // ======================

                    string estado =
                    request.metodo_pago ==
                    "PENDIENTE"

                    ? "PENDIENTE"

                    : "PAGADO";

                    string formaCobro =
                    estado == "PENDIENTE"

                    ? "PENDIENTE"

                    : "PAGADO";

                    // ======================
                    // INSERTAR VENTA
                    // ======================

                    string ventaQuery = @"

INSERT INTO ventas
(
id_cliente,
id_usuario,
tipo,
numero_orden,
nombre_orden,
forma_cobro,
metodo_pago,
total,
estado,
observacion,
fecha
)

VALUES
(
@cliente,
@usuario,
'PRODUCTO',
@numero,
@nombre,
@forma,
@metodo,
@total,
@estado,
@obs,
NOW()
);

SELECT LAST_INSERT_ID();
";

                    MySqlCommand cmdVenta =
                    new MySqlCommand(
                    ventaQuery,
                    conn,
                    transaction);

                    cmdVenta.Parameters.AddWithValue(
                    "@cliente",
                    request.id_cliente);

                    cmdVenta.Parameters.AddWithValue(
                    "@usuario",
                    request.id_usuario);

                    cmdVenta.Parameters.AddWithValue(
                    "@numero",
                    request.numero_orden
                    ?? "000");

                    cmdVenta.Parameters.AddWithValue(
                    "@nombre",
                    request.nombre_orden
                    ?? "ORDEN POS");

                    cmdVenta.Parameters.AddWithValue(
                    "@forma",
                    formaCobro);

                    cmdVenta.Parameters.AddWithValue(
                    "@metodo",
                    request.metodo_pago);

                    cmdVenta.Parameters.AddWithValue(
                    "@total",
                    total);

                    cmdVenta.Parameters.AddWithValue(
                    "@estado",
                    estado);

                    cmdVenta.Parameters.AddWithValue(
                    "@obs",
                    request.observacion
                    ?? "");

                    int idVenta =
                    Convert.ToInt32(
                    cmdVenta.ExecuteScalar()
                    );

                    // ======================
                    // DETALLE PRODUCTOS
                    // ======================

                    foreach (var p
                    in request.productos)
                    {
                        string detalleQuery = @"

INSERT INTO detalle_ventas
(
id_venta,
id_producto,
cantidad,
precio,
subtotal
)

VALUES
(
@venta,
@producto,
@cantidad,
@precio,
@subtotal
)
";

                        MySqlCommand cmdDetalle =
                        new MySqlCommand(
                        detalleQuery,
                        conn,
                        transaction);

                        cmdDetalle.Parameters.AddWithValue(
                        "@venta",
                        idVenta);

                        cmdDetalle.Parameters.AddWithValue(
                        "@producto",
                        p.id_producto);

                        cmdDetalle.Parameters.AddWithValue(
                        "@cantidad",
                        p.cantidad);

                        cmdDetalle.Parameters.AddWithValue(
                        "@precio",
                        p.precio);

                        cmdDetalle.Parameters.AddWithValue(
                        "@subtotal",
                        p.precio * p.cantidad);

                        cmdDetalle.ExecuteNonQuery();

                        // ======================
                        // DESCONTAR STOCK
                        // ======================

                        string stockQuery = @"

UPDATE productos

SET stock = stock - @cantidad

WHERE id_producto=@producto AND controla_stock = 1
";

                        MySqlCommand cmdStock =
                        new MySqlCommand(
                        stockQuery,
                        conn,
                        transaction);

                        cmdStock.Parameters.AddWithValue(
                        "@cantidad",
                        p.cantidad);

                        cmdStock.Parameters.AddWithValue(
                        "@producto",
                        p.id_producto);

                        cmdStock.ExecuteNonQuery();

                        // ======================
                        // HISTORIAL INVENTARIO
                        // ======================

                        string historialQuery = @"

INSERT INTO historial_inventario
(
id_producto,
tipo_movimiento,
cantidad,
observacion,
usuario,
fecha
)

VALUES
(
@producto,
'SALIDA',
@cantidad,
@obs,
@usuario,
NOW()
)
";

                        MySqlCommand cmdHist =
                        new MySqlCommand(
                        historialQuery,
                        conn,
                        transaction);

                        cmdHist.Parameters.AddWithValue(
                        "@producto",
                        p.id_producto);

                        cmdHist.Parameters.AddWithValue(
                        "@cantidad",
                        p.cantidad);

                        cmdHist.Parameters.AddWithValue(
                        "@obs",
                        "Venta POS");

                        cmdHist.Parameters.AddWithValue(
                        "@usuario",
                        request.id_usuario);

                        cmdHist.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    return Ok(new
                    {
                        mensaje =
                        "Venta registrada correctamente",

                        id_venta =
                        idVenta,

                        total
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    return BadRequest(new
                    {
                        error =
                        ex.Message
                    });
                }
            }
        }

        // ======================
        // VENTAS PENDIENTES
        // ======================
        [HttpGet("pendientes")]
        public IActionResult VentasPendientes()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

SELECT

v.id_venta,

v.numero_orden,

v.tipo,

v.total,

v.estado,

v.fecha,

c.nombre AS cliente

FROM ventas v

LEFT JOIN clientes c
ON v.id_cliente = c.id_cliente

WHERE v.estado='PENDIENTE'

ORDER BY v.fecha ASC
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
                        id =
                        reader["id_venta"],

                        numero =
                        reader["numero_orden"],

                        tipo =
                        reader["tipo"],

                        total =
                        reader["total"],

                        estado =
                        reader["estado"],

                        cliente =
                        reader["cliente"],

                        fecha =
                        reader["fecha"]
                    });
                }

                return Ok(lista);
            }
        }

        // ======================
        // EDITAR VENTA
        // ======================
        [HttpPut("{id}")]
        public IActionResult EditarVenta(
        int id,

        [FromBody]
        EditarVentaRequest request)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string updateQuery = @"

UPDATE ventas

SET

forma_cobro=@forma,

metodo_pago=@metodo,

estado='PAGADO',

observacion=@obs

WHERE id_venta=@id
";

                MySqlCommand cmd =
                new MySqlCommand(
                updateQuery,
                conn);

                cmd.Parameters.AddWithValue(
                "@forma",
                request.forma_cobro);

                cmd.Parameters.AddWithValue(
                "@metodo",
                request.metodo_pago);

                cmd.Parameters.AddWithValue(
                "@obs",
                request.observacion
                ?? "");

                cmd.Parameters.AddWithValue(
                "@id",
                id);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje =
                    "Venta actualizada"
                });
            }
        }

        // ======================
        // OBTENER VENTA
        // ======================
        [HttpGet("{id}")]
        public IActionResult ObtenerVenta(int id)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string ventaQuery = @"

SELECT *

FROM ventas

WHERE id_venta=@id
";

                MySqlCommand cmdVenta =
                new MySqlCommand(
                ventaQuery,
                conn);

                cmdVenta.Parameters.AddWithValue(
                "@id",
                id);

                var readerVenta =
                cmdVenta.ExecuteReader();

                if (!readerVenta.Read())
                    return NotFound();

                var venta = new
                {
                    id =
                    readerVenta["id_venta"],

                    total =
                    readerVenta["total"],

                    estado =
                    readerVenta["estado"],

                    metodo =
                    readerVenta["metodo_pago"],

                    fecha =
                    readerVenta["fecha"]
                };

                readerVenta.Close();

                string detalleQuery = @"

SELECT

p.nombre,

d.cantidad,

d.precio,

d.subtotal

FROM detalle_ventas d

JOIN productos p
ON d.id_producto=p.id_producto

WHERE d.id_venta=@id
";

                MySqlCommand cmdDetalle =
                new MySqlCommand(
                detalleQuery,
                conn);

                cmdDetalle.Parameters.AddWithValue(
                "@id",
                id);

                var readerDetalle =
                cmdDetalle.ExecuteReader();

                List<object> productos =
                new List<object>();

                while (readerDetalle.Read())
                {
                    productos.Add(new
                    {
                        nombre =
                        readerDetalle["nombre"],

                        cantidad =
                        readerDetalle["cantidad"],

                        precio =
                        readerDetalle["precio"],

                        subtotal =
                        readerDetalle["subtotal"]
                    });
                }

                return Ok(new
                {
                    venta,
                    productos
                });
            }
        }
    }
}
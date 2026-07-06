using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly DbManager _db;

        public ClientesController(DbManager db) => _db = db;

        [HttpPost]
        public IActionResult CrearCliente([FromBody] ClienteRequest request)
        {
            string codigo = "CLI-" + DateTime.Now.Ticks.ToString().Substring(10);

            _db.ExecuteNonQuery(
                "INSERT INTO clientes (nombre, telefono, apodo, codigo, estado) VALUES (@nombre, @telefono, @apodo, @codigo, 'ACTIVO')",
                new MySqlParameter("@nombre", request.nombre),
                new MySqlParameter("@telefono", request.telefono),
                new MySqlParameter("@apodo", request.apodo),
                new MySqlParameter("@codigo", codigo));

            return Ok(new { mensaje = "Cliente creado correctamente", codigo });
        }

        [HttpGet("buscar")]
        public IActionResult Buscar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return BadRequest("Debe ingresar texto para buscar");

            var rows = _db.ExecuteQuery(
                "SELECT * FROM clientes WHERE nombre LIKE @texto OR telefono LIKE @texto OR apodo LIKE @texto",
                new MySqlParameter("@texto", "%" + texto + "%"));

            return Ok(rows.Select(r => new
            {
                codigo = r["codigo"]?.ToString(),
                id = r["id_cliente"],
                nombre = r["nombre"]?.ToString(),
                telefono = r["telefono"]?.ToString(),
                apodo = r["apodo"]?.ToString()
            }));
        }

        [HttpGet]
        public IActionResult Listar()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM clientes");

            return Ok(rows.Select(r => new
            {
                codigo = r["codigo"]?.ToString(),
                id = r["id_cliente"],
                nombre = r["nombre"]?.ToString(),
                telefono = r["telefono"]?.ToString(),
                apodo = r["apodo"]?.ToString()
            }));
        }

        [HttpGet("qr/{codigo}")]
        public IActionResult GenerarQR(string codigo)
        {
            var count = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM clientes WHERE codigo = @codigo",
                new MySqlParameter("@codigo", codigo));

            if (Convert.ToInt32(count) == 0)
                return NotFound("Cliente no encontrado");

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(codigo, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var qrImage = qrCode.GetGraphic(20);
            using var ms = new MemoryStream();
            qrImage.Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        [HttpGet("{id}/compras")]
        public IActionResult HistorialCompras(int id)
        {
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, v.total, v.metodo_pago, v.estado
                FROM ventas v
                WHERE v.id_cliente = @id AND v.estado != 'CANCELADO'
                ORDER BY v.fecha DESC",
                new MySqlParameter("@id", id));

            var resultado = ventas.Select(v =>
            {
                int idVenta = Convert.ToInt32(v["id_venta"]);
                var detalle = _db.ExecuteQuery(@"
                    SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre, d.cantidad, d.precio
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta = @id AND d.precio > 0",
                    new MySqlParameter("@id", idVenta));

                return new
                {
                    id_venta = idVenta,
                    fecha = v["fecha"],
                    total = v["total"],
                    metodo_pago = v["metodo_pago"],
                    estado = v["estado"],
                    items = detalle.Select(d => new
                    {
                        nombre = d["nombre"].ToString(),
                        cantidad = d["cantidad"],
                        precio = d["precio"]
                    })
                };
            });

            return Ok(resultado);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/productos")]
    public class ProductosController : ControllerBase
    {
        private readonly DbManager _db;

        public ProductosController(DbManager db) => _db = db;

        [HttpPost]
        public IActionResult CrearProducto([FromBody] ProductoRequest request)
        {
            _db.ExecuteNonQuery(@"
                INSERT INTO productos (nombre, id_categoria, id_subcategoria, precio_compra, precio_venta, stock, controla_stock)
                VALUES (@nombre, @cat, @sub, @compra, @venta, @stock, @controla)",
                new MySqlParameter("@nombre", request.nombre),
                new MySqlParameter("@cat", request.id_categoria),
                new MySqlParameter("@sub", request.id_subcategoria),
                new MySqlParameter("@compra", request.precio_compra),
                new MySqlParameter("@venta", request.precio_venta),
                new MySqlParameter("@stock", request.stock),
                new MySqlParameter("@controla", request.controla_stock));

            return Ok(new { mensaje = "Producto creado" });
        }

        [HttpPut("stock")]
        public IActionResult AgregarStock([FromBody] StockRequest request)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            var transaction = conn.BeginTransaction();

            try
            {
                var cmdUpdate = new MySqlCommand("UPDATE productos SET stock = stock + @cantidad WHERE id_producto=@id", conn, transaction);
                cmdUpdate.Parameters.AddWithValue("@cantidad", request.cantidad);
                cmdUpdate.Parameters.AddWithValue("@id", request.id_producto);
                cmdUpdate.ExecuteNonQuery();

                var cmdHist = new MySqlCommand(@"
                    INSERT INTO historial_inventario (id_producto, tipo_movimiento, cantidad, observacion)
                    VALUES (@id, 'ENTRADA', @cantidad, @obs)", conn, transaction);
                cmdHist.Parameters.AddWithValue("@id", request.id_producto);
                cmdHist.Parameters.AddWithValue("@cantidad", request.cantidad);
                cmdHist.Parameters.AddWithValue("@obs", request.observacion);
                cmdHist.ExecuteNonQuery();

                transaction.Commit();
                return Ok(new { mensaje = "Stock actualizado" });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public IActionResult ObtenerProductos()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM productos WHERE activo = 1");

            return Ok(rows.Select(r => new
            {
                id = r["id_producto"],
                nombre = r["nombre"],
                precio_venta = r["precio_venta"],
                stock = r["stock"],
                controla_stock = Convert.ToInt32(r["controla_stock"])
            }));
        }

        [HttpGet("alertas")]
        public IActionResult Alertas()
        {
            var rows = _db.ExecuteQuery(@"
                SELECT nombre, stock,
                CASE WHEN stock = 0 THEN 'AGOTADO' WHEN stock <= 5 THEN 'BAJO' ELSE 'OK' END AS estado
                FROM productos");

            return Ok(rows.Select(r => new { nombre = r["nombre"], stock = r["stock"], estado = r["estado"] }));
        }

        [HttpGet("categorias")]
        public IActionResult Categorias()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM categorias");
            return Ok(rows.Select(r => new { id = r["id_categoria"], nombre = r["nombre"] }));
        }

        [HttpGet("categoria/{id}")]
        public IActionResult Productos(int id)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM productos WHERE id_categoria=@id AND activo = 1",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new
            {
                id = r["id_producto"],
                nombre = r["nombre"],
                precio_venta = r["precio_venta"],
                stock = r["stock"],
                controla_stock = r["controla_stock"]
            }));
        }

        [HttpGet("subcategorias/{id}")]
        public IActionResult Subcategorias(int id)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM subcategorias WHERE id_categoria=@id AND activo = 1",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new { id_subcategoria = r["id_subcategoria"], nombre = r["nombre"] }));
        }

        [HttpGet("subcategoria/{id}")]
        public IActionResult ProductosSubcategoria(int id)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM productos WHERE id_subcategoria=@id",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new
            {
                id_producto = r["id_producto"],
                nombre = r["nombre"],
                precio_venta = r["precio_venta"],
                stock = r["stock"]
            }));
        }

        [HttpPut("{id}")]
        public IActionResult EditarProducto(int id, [FromBody] EditarProductoRequest req)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            var stockCmd = new MySqlCommand("SELECT stock FROM productos WHERE id_producto=@id", conn);
            stockCmd.Parameters.AddWithValue("@id", id);
            int stockActual = Convert.ToInt32(stockCmd.ExecuteScalar());

            var updateCmd = new MySqlCommand("UPDATE productos SET precio_venta=@precio, stock=@stock WHERE id_producto=@id", conn);
            updateCmd.Parameters.AddWithValue("@precio", req.precio_venta);
            updateCmd.Parameters.AddWithValue("@stock", req.stock);
            updateCmd.Parameters.AddWithValue("@id", id);
            updateCmd.ExecuteNonQuery();

            int diferencia = req.stock - stockActual;
            string tipo = diferencia >= 0 ? "ENTRADA" : "SALIDA";

            var histCmd = new MySqlCommand(@"
                INSERT INTO historial_inventario (id_producto, tipo_movimiento, cantidad, observacion, usuario, fecha)
                VALUES (@producto, @tipo, @cantidad, @obs, @usuario, NOW())", conn);
            histCmd.Parameters.AddWithValue("@producto", id);
            histCmd.Parameters.AddWithValue("@tipo", tipo);
            histCmd.Parameters.AddWithValue("@cantidad", Math.Abs(diferencia));
            histCmd.Parameters.AddWithValue("@obs", "Edición inventario");
            histCmd.Parameters.AddWithValue("@usuario", req.usuario);
            histCmd.ExecuteNonQuery();

            return Ok(new { mensaje = "Producto actualizado" });
        }

        [HttpDelete("{id}")]
        public IActionResult EliminarProducto(int id)
        {
            try
            {
                _db.ExecuteNonQuery("DELETE FROM productos WHERE id_producto=@id",
                    new MySqlParameter("@id", id));
                return Ok(new { mensaje = "Producto eliminado" });
            }
            catch (MySqlException)
            {
                _db.ExecuteNonQuery("UPDATE productos SET activo = 0 WHERE id_producto=@id",
                    new MySqlParameter("@id", id));
                return Ok(new { mensaje = "Producto desactivado (tenía ventas registradas)" });
            }
        }
    }
}

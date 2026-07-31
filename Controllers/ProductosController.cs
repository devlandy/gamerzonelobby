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
            catch
            {
                transaction.Rollback();
                return BadRequest(new { error = "No se pudo actualizar el stock. Intenta de nuevo." });
            }
        }

        [HttpGet]
        public IActionResult ObtenerProductos()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM productos WHERE activo = 1 ORDER BY nombre");

            return Ok(rows.Select(r => new
            {
                id             = Convert.ToInt32(r["id_producto"]),
                nombre         = r["nombre"],
                precio_compra  = r["precio_compra"],
                precio_venta   = r["precio_venta"],
                stock          = r["stock"],
                controla_stock = Convert.ToInt32(r["controla_stock"]),
                es_ingrediente = Convert.ToDecimal(r["precio_venta"]) == 0
            }));
        }

        [HttpPost("{id}/entrada")]
        public IActionResult EntradaStock(int id, [FromBody] EntradaStockRequest req)
        {
            var prod = _db.ExecuteQuery("SELECT nombre, precio_venta FROM productos WHERE id_producto=@id",
                new MySqlParameter("@id", id));
            if (prod.Count == 0) return NotFound(new { error = "Producto no encontrado" });

            bool esIngrediente = Convert.ToDecimal(prod[0]["precio_venta"]) == 0;
            string nombre = prod[0]["nombre"]?.ToString() ?? "";

            // Actualizar precio_compra si se envía uno nuevo
            if (req.precio_compra > 0)
                _db.ExecuteNonQuery("UPDATE productos SET precio_compra=@pc WHERE id_producto=@id",
                    new MySqlParameter("@pc", req.precio_compra),
                    new MySqlParameter("@id", id));

            _db.ExecuteNonQuery("UPDATE productos SET stock = stock + @cantidad WHERE id_producto=@id",
                new MySqlParameter("@cantidad", req.cantidad),
                new MySqlParameter("@id", id));

            _db.ExecuteNonQuery(@"
                INSERT INTO historial_inventario (id_producto, tipo_movimiento, cantidad, observacion, fecha)
                VALUES (@id, 'ENTRADA', @cantidad, @obs, NOW())",
                new MySqlParameter("@id", id),
                new MySqlParameter("@cantidad", req.cantidad),
                new MySqlParameter("@obs", req.observacion ?? "Compra de stock"));

            // Registrar costo como gasto en finanzas si se indica precio de compra
            if (req.precio_compra > 0)
            {
                decimal costoTotal = req.precio_compra * req.cantidad;
                string tipoDesc = esIngrediente ? "ingrediente" : "producto";
                _db.ExecuteNonQuery(@"
                    INSERT INTO gastos (descripcion, monto, fecha)
                    VALUES (@desc, @monto, NOW())",
                    new MySqlParameter("@desc", $"Compra {tipoDesc}: {nombre} x{req.cantidad}"),
                    new MySqlParameter("@monto", costoTotal));
            }

            return Ok(new { mensaje = "Stock actualizado", gasto_registrado = req.precio_compra > 0 });
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
            var rows = _db.ExecuteQuery("SELECT * FROM categorias ORDER BY nombre");
            return Ok(rows.Select(r => new { id = Convert.ToInt32(r["id_categoria"]), nombre = r["nombre"] }));
        }

        [HttpPost("categorias")]
        public IActionResult CrearCategoria([FromBody] NombreRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.nombre))
                return BadRequest(new { error = "El nombre es requerido" });

            _db.ExecuteNonQuery("INSERT INTO categorias (nombre) VALUES (@nombre)",
                new MySqlParameter("@nombre", req.nombre.Trim()));

            return Ok(new { mensaje = "Categoría creada" });
        }

        [HttpDelete("categorias/{id}")]
        public IActionResult EliminarCategoria(int id)
        {
            var count = Convert.ToInt32(_db.ExecuteScalar(
                "SELECT COUNT(*) FROM productos WHERE id_categoria=@id",
                new MySqlParameter("@id", id)));

            if (count > 0)
                return BadRequest(new { error = $"No se puede eliminar: tiene {count} producto(s) asociado(s)" });

            _db.ExecuteNonQuery("DELETE FROM subcategorias WHERE id_categoria=@id",
                new MySqlParameter("@id", id));
            _db.ExecuteNonQuery("DELETE FROM categorias WHERE id_categoria=@id",
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Categoría eliminada" });
        }

        [HttpPost("subcategorias")]
        public IActionResult CrearSubcategoria([FromBody] SubcategoriaRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.nombre))
                return BadRequest(new { error = "El nombre es requerido" });

            _db.ExecuteNonQuery(
                "INSERT INTO subcategorias (nombre, id_categoria, activo) VALUES (@nombre, @cat, 1)",
                new MySqlParameter("@nombre", req.nombre.Trim()),
                new MySqlParameter("@cat", req.id_categoria));

            return Ok(new { mensaje = "Subcategoría creada" });
        }

        [HttpDelete("subcategorias/{id}")]
        public IActionResult EliminarSubcategoria(int id)
        {
            var count = Convert.ToInt32(_db.ExecuteScalar(
                "SELECT COUNT(*) FROM productos WHERE id_subcategoria=@id",
                new MySqlParameter("@id", id)));

            if (count > 0)
                return BadRequest(new { error = $"No se puede eliminar: tiene {count} producto(s) asociado(s)" });

            _db.ExecuteNonQuery("DELETE FROM subcategorias WHERE id_subcategoria=@id",
                new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Subcategoría eliminada" });
        }

        [HttpGet("categoria/{id}")]
        public IActionResult Productos(int id)
        {
            var rows = _db.ExecuteQuery(
                "SELECT * FROM productos WHERE id_categoria=@id AND activo = 1",
                new MySqlParameter("@id", id));

            return Ok(rows.Select(r => new
            {
                id             = Convert.ToInt32(r["id_producto"]),
                nombre         = r["nombre"],
                precio_compra  = r["precio_compra"],
                precio_venta   = r["precio_venta"],
                stock          = r["stock"],
                controla_stock = Convert.ToInt32(r["controla_stock"]),
                es_ingrediente = Convert.ToDecimal(r["precio_venta"]) == 0
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/combos")]
    public class CombosController : ControllerBase
    {
        private readonly DbManager _db;
        public CombosController(DbManager db) => _db = db;

        [HttpGet]
        public IActionResult ListarCombos()
        {
            var combos = _db.ExecuteQuery(
                "SELECT id_combo, nombre, precio, descripcion FROM combos WHERE activo = 1");

            var resultado = combos.Select(c =>
            {
                int idCombo = Convert.ToInt32(c["id_combo"]);
                var detalle = _db.ExecuteQuery(
                    "SELECT id_producto, nombre_item, cantidad, es_seleccionable, id_categoria_seleccion FROM combo_detalle WHERE id_combo = @id",
                    new MySqlParameter("@id", idCombo));

                return new
                {
                    id_combo = idCombo,
                    nombre = c["nombre"].ToString(),
                    precio = c["precio"],
                    descripcion = c["descripcion"].ToString(),
                    items = detalle.Select(d => new
                    {
                        id_producto = d["id_producto"] == DBNull.Value ? (int?)null : Convert.ToInt32(d["id_producto"]),
                        nombre_item = d["nombre_item"].ToString(),
                        cantidad = Convert.ToInt32(d["cantidad"]),
                        es_seleccionable = Convert.ToInt32(d["es_seleccionable"]) == 1,
                        id_categoria_seleccion = d["id_categoria_seleccion"] == DBNull.Value ? (int?)null : Convert.ToInt32(d["id_categoria_seleccion"])
                    })
                };
            });

            return Ok(resultado);
        }
    }
}

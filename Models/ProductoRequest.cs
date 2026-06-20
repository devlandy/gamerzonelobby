namespace GamerZoneAPI.Models
{
    public class ProductoRequest
    {
        public string nombre { get; set; }
        public int id_categoria { get; set; }
        public int id_subcategoria { get; set; }
        public decimal precio_compra { get; set; }
        public decimal precio_venta { get; set; }
        public int stock { get; set; }
        public int controla_stock { get; set; } = 1;
    }
}
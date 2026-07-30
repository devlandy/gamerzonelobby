namespace GamerZoneAPI.Models
{
    public class NombreRequest
    {
        public string nombre { get; set; }
    }

    public class SubcategoriaRequest
    {
        public string nombre { get; set; }
        public int id_categoria { get; set; }
    }

    public class EntradaStockRequest
    {
        public int cantidad { get; set; }
        public decimal precio_compra { get; set; }
        public string? observacion { get; set; }
    }
}

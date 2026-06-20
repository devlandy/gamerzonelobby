namespace GamerZoneAPI.Models
{
    public class EditarProductoRequest
    {
        public decimal precio_venta { get; set; }

        public int stock { get; set; }

        public string usuario { get; set; }
    }
}
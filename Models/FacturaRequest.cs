namespace GamerZoneAPI.Models
{
    public class FacturaRequest
    {
        public int id_venta { get; set; }

        public string nit { get; set; }

        public string nombre { get; set; }

        public string direccion { get; set; }
    }
}
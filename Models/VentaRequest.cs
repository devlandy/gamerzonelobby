namespace GamerZoneAPI.Models
{
    public class VentaRequest
    {
        public int? id_cliente { get; set; }

        public int id_usuario { get; set; }

        public string nombre_orden { get; set; }

        public string numero_orden { get; set; }

        public string forma_cobro { get; set; }

        public string metodo_pago { get; set; }

        public decimal total { get; set; }

        public string observacion { get; set; }

        public decimal descuento_pct { get; set; }

        public List<ProductoVenta> productos { get; set; }
    }

    public class ProductoVenta
    {
        public int id_producto { get; set; }

        public string? nombre { get; set; }

        public int cantidad { get; set; }

        public decimal precio { get; set; }
    }
}
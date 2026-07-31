using System.ComponentModel.DataAnnotations;

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

        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100")]
        public decimal descuento_pct { get; set; }

        public List<ProductoVenta> productos { get; set; }
    }

    public class ProductoVenta
    {
        public int id_producto { get; set; }

        public string? nombre { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public int cantidad { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo")]
        public decimal precio { get; set; }

        public int? minutos { get; set; }
    }
}
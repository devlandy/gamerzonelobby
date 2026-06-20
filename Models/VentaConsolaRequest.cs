namespace GamerZoneAPI.Models
{
    public class VentaConsolaRequest
    {
        public int id_cliente { get; set; }
        public int id_usuario { get; set; }
        public string consola { get; set; }
        public int minutos { get; set; }
        public string forma_cobro { get; set; }
        public string metodo_pago { get; set; }
        public string observacion { get; set; }
    }
}

namespace GamerZoneAPI.Models
{
    public class ComboRequest
    {
        public int id_cliente { get; set; }
        public int id_usuario { get; set; }
        public int combo { get; set; }
        public List<int> bebidas_ids { get; set; } = new List<int>();
    }


}
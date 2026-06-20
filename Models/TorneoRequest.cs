namespace GamerZoneAPI.Models
{
    public class TorneoRequest
    {
        public string nombre { get; set; }

        public string juego { get; set; }

        public decimal premio { get; set; }

        public decimal inscripcion { get; set; }

        public int cupos { get; set; }

        public List<Participante> participantes
        { get; set; }
        = new List<Participante>();
    }

    public class Participante
    {
        public int id_cliente { get; set; }

        public int posicion { get; set; }
    }
}
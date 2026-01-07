namespace Checkers.Models
{
    public class MoveRequest
    {
        public string GameId { get; set; }
        // Початкова позиція
        public int FromRow { get; set; }
        public int FromCol { get; set; }

        // Кінцева позиція
        public int ToRow { get; set; }
        public int ToCol { get; set; }
    }
}

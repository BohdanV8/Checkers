using System.Text.Json.Serialization;

namespace Checkers.Models
{
    public class GameEnums
    {
        public enum Piece
        {
            Empty,
            White,
            Black,
            WhiteKing,
            BlackKing
        }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum GameStatus
        {
            WaitingForOpponent,
            InProgress,
            Completed
        }
    }
}

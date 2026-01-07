using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Checkers.Models
{
    public class Game
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string PlayerWhiteId { get; set; } = string.Empty;
        public string PlayerBlackId { get; set; } = string.Empty;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public GameEnums.GameStatus Status { get; set; } = GameEnums.GameStatus.WaitingForOpponent;
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public GameEnums.Piece CurrentTurn { get; set; } = GameEnums.Piece.White;
        public string? WinnerPlayerId { get; set; }
        public GameEnums.Piece[][] Board { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Game()
        {
            Board = InitializeBoard();
        }
        private GameEnums.Piece[][] InitializeBoard()
        {
            GameEnums.Piece[][] board = new GameEnums.Piece[8][];
            for (int i = 0; i < 8; i++)
            {
                board[i] = new GameEnums.Piece[8];
                for (int j = 0; j < 8; j++)
                {
                    if ((i + j) % 2 != 0)
                    {
                        if (i < 3)
                        {
                            board[i][j] = GameEnums.Piece.Black; // Black piece
                        }
                        else if (i > 4)
                        {
                            board[i][j] = GameEnums.Piece.White; // White piece
                        }
                        else
                        {
                            board[i][j] = GameEnums.Piece.Empty; // Empty square
                        }
                    }
                    else
                    {
                        board[i][j] = GameEnums.Piece.Empty; // Empty square
                    }
                }
            }
            return board;
        }
    }
}

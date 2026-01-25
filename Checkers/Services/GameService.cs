using Checkers.Hubs;
using Checkers.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace Checkers.Services
{
    public interface IGameService
    {
        Task<Game> CreateGameAsync(string hostUserId);
        Task<Game?> GetGameAsync(string gameId);
        Task<bool> JoinGameAsync(string gameId, string secondPlayerId);
        Task<List<Game>> GetOpenGamesAsync();
        Task<Game> MakeMoveAsync(string userId, MoveRequest move);
    }
    public class GameService : IGameService
    {
        private readonly Core.AppDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;
        public GameService(Core.AppDbContext context, IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
            _context = context;
        }
        public async Task<Game> CreateGameAsync(string hostUserId)
        {
            var game = new Game
            {
                PlayerWhiteId = hostUserId,
                Status = GameEnums.GameStatus.WaitingForOpponent,
                CurrentTurn = GameEnums.Piece.White
            };
            await _context.Games.InsertOneAsync(game);
            return game;
        }
        public async Task<Game?> GetGameAsync(string gameId)
        {
            return await _context.Games.Find(g => g.Id == gameId).FirstOrDefaultAsync();
        }
        public async Task<bool> JoinGameAsync(string gameId, string secondPlayerId)
        {
            var update = Builders<Game>.Update
                .Set(g => g.PlayerBlackId, secondPlayerId)
                .Set(g => g.Status, GameEnums.GameStatus.InProgress);
            var result = await _context.Games.UpdateOneAsync(g => g.Id == gameId && g.Status == GameEnums.GameStatus.WaitingForOpponent, update);
            bool success =  result.ModifiedCount > 0;
            if(success)
            {
                await _hubContext.Clients.Group(gameId).SendAsync("OpponentJoined", secondPlayerId);
            }
            return success;
        }
        public async Task<List<Game>> GetOpenGamesAsync()
        {
            return await _context.Games.Find(g => g.Status == GameEnums.GameStatus.WaitingForOpponent).ToListAsync();
        }
        private bool CanCaptureFrom(GameEnums.Piece[][] board, int row, int col, GameEnums.Piece currentPiece)
        {
            // Визначаємо, хто ворог
            bool isWhite = currentPiece == GameEnums.Piece.White || currentPiece == GameEnums.Piece.WhiteKing;
            var enemySimple = isWhite ? GameEnums.Piece.Black : GameEnums.Piece.White;
            var enemyKing = isWhite ? GameEnums.Piece.BlackKing : GameEnums.Piece.WhiteKing;

            // Всі 4 напрямки по діагоналі
            int[] rowDirs = { -1, -1, 1, 1 };
            int[] colDirs = { -1, 1, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int midRow = row + rowDirs[i];     // Клітинка, яку перестрибуємо
                int targetRow = row + rowDirs[i] * 2; // Клітинка, куди приземляємось
                int midCol = col + colDirs[i];
                int targetCol = col + colDirs[i] * 2;

                // 1. Перевіряємо межі дошки
                if (targetRow >= 0 && targetRow < 8 && targetCol >= 0 && targetCol < 8)
                {
                    var midPiece = board[midRow][midCol];
                    var targetPiece = board[targetRow][targetCol];

                    // 2. Якщо посередині ворог, а за ним пусто — можна бити!
                    if ((midPiece == enemySimple || midPiece == enemyKing) && targetPiece == GameEnums.Piece.Empty)
                    {
                        // Додаткова перевірка для звичайних шашок (не дамок):
                        // Якщо хочеш дозволити бити назад звичайним шашкам (міжнародні правила) - цей код ок.
                        // Якщо бити назад можуть тільки дамки - треба додати перевірку на напрямок.
                        // (Зазвичай у більшості правил бити назад МОЖНА всім).
                        return true;
                    }
                }
            }
            return false;
        }
        public async Task<Game> MakeMoveAsync(string userId, MoveRequest move)
        {
            Game? game = await GetGameAsync(move.GameId);
            if (game == null) {
                throw new Exception("Game not found");
            }
            if(game.Status != GameEnums.GameStatus.InProgress)
            {
                throw new Exception("Game is not in progress");
            }
            bool isWhite = game.PlayerWhiteId == userId;
            bool isBlack = game.PlayerBlackId == userId;
            if (!isBlack && !isWhite)
            {
                throw new Exception("You're not the player of this game");
            }
            if ((isWhite && game.CurrentTurn != GameEnums.Piece.White) ||
                (isBlack && game.CurrentTurn != GameEnums.Piece.Black))
            {
                throw new Exception("It's not your turn");
            }
            if (game.ContinueJumpFrom != null)
            {
                if (move.FromRow != game.ContinueJumpFrom.Row || move.FromCol != game.ContinueJumpFrom.Col)
                {
                    throw new Exception("You must continue capturing with the active piece!");
                }
            }
            GameEnums.Piece[][] board = game.Board;
            GameEnums.Piece piece = board[move.FromRow][move.FromCol];
            if(piece == GameEnums.Piece.Empty) throw new Exception("Selected empty cell");
            if((isWhite && (piece != GameEnums.Piece.White && piece != GameEnums.Piece.WhiteKing)) ||
               (isBlack && (piece != GameEnums.Piece.Black && piece != GameEnums.Piece.BlackKing)))
            {
                throw new Exception("You can only move your own pieces");
            }
            if (board[move.ToRow][move.ToCol] != GameEnums.Piece.Empty)
                throw new Exception("this cell is already setted"); 
            int RowDiff = move.ToRow - move.FromRow;
            int ColDiff = move.ToCol - move.FromCol; // Має бути 1 або -1 (по діагоналі)
            // Абсолютна різниця (модуль числа), бо нам байдуже вліво чи вправо
            int absRowDiff = Math.Abs(RowDiff);
            int absColDiff = Math.Abs(ColDiff);
            bool isCapture = absRowDiff == 2;
            if (absColDiff != absRowDiff) throw new Exception("Move must be diagonal");
            // --- ВАЛІДАЦІЯ ТИПУ ХОДУ ---
            if (game.ContinueJumpFrom != null && absRowDiff != 2)
            {
                throw new Exception("You must capture!");
            }
            // --- Логіка Звичайного Ходу (на 1 клітинку) ---
            if (absRowDiff == 1)
            {
                // Перевірка напрямку (тільки вперед для звичайних шашок)
                // Білі йдуть вгору (індекс зменшується: 7 -> 6), Чорні йдуть вниз (0 -> 1)
                if (piece == GameEnums.Piece.White && RowDiff > 0) throw new Exception("White must move up");
                if (piece == GameEnums.Piece.Black && RowDiff < 0) throw new Exception("Black must move down");

                // Виконуємо хід: переміщуємо шашку
                board[move.ToRow][move.ToCol] = piece;
                board[move.FromRow][move.FromCol] = GameEnums.Piece.Empty;
            }
            // --- Логіка Биття (на 2 клітинки) ---
            else if (absRowDiff == 2)
            {
                // Знаходимо координати "збитої" шашки (вона посередині між From і To)
                int midRow = (move.FromRow + move.ToRow) / 2;
                int midCol = (move.FromCol + move.ToCol) / 2;
                GameEnums.Piece midPiece = board[midRow][midCol];

                // Перевіряємо, чи є кого бити
                if (midPiece == GameEnums.Piece.Empty) throw new Exception("Cannot jump over empty space");
                if (isWhite && (midPiece == GameEnums.Piece.White || midPiece == GameEnums.Piece.WhiteKing)) throw new Exception("Cannot jump over own piece");
                if (isBlack && (midPiece == GameEnums.Piece.Black || midPiece == GameEnums.Piece.BlackKing)) throw new Exception("Cannot jump over own piece");

                // Виконуємо бій
                board[move.ToRow][move.ToCol] = piece; // Ставимо на нове місце
                board[move.FromRow][move.FromCol] = GameEnums.Piece.Empty; // Прибираємо зі старого
                board[midRow][midCol] = GameEnums.Piece.Empty; // Прибираємо збиту шашку
            }
            else
            {
                throw new Exception("Invalid move distance");
            }
            // 5. Перетворення в Дамку (King)
            // Якщо біла дійшла до рядка 0, або чорна до рядка 7
            if (piece == GameEnums.Piece.White && move.ToRow == 0)
                board[move.ToRow][move.ToCol] = GameEnums.Piece.WhiteKing;

            if (piece == GameEnums.Piece.Black && move.ToRow == 7)
                board[move.ToRow][move.ToCol] = GameEnums.Piece.BlackKing;


            // --- ГОЛОВНА МАГІЯ МУЛЬТИ-ДЖАМПА ---

            bool turnEnded = true; // За замовчуванням хід закінчується
            if (isCapture)
            {
                // Якщо це було биття, перевіряємо, чи можна бити далі
                // Важливий нюанс правил: якщо шашка стала дамкою, серія зазвичай закінчується (залежить від правил).
                // Але в міжнародних часто можна бити далі вже як дамка. Давай поки дозволимо бити далі.

                if (CanCaptureFrom(game.Board, move.ToRow, move.ToCol, piece))
                {
                    // Є серія!
                    game.ContinueJumpFrom = new Position { Row = move.ToRow, Col = move.ToCol };
                    turnEnded = false; // Хід НЕ переходить до іншого
                }
            }
            if (turnEnded)
            {
                game.ContinueJumpFrom = null; // Скидаємо серію
                game.CurrentTurn = game.CurrentTurn == GameEnums.Piece.White
                    ? GameEnums.Piece.Black
                    : GameEnums.Piece.White;
            }

           
            // 6. Зберігаємо зміни в базу
            var update = Builders<Game>.Update
                .Set(g => g.Board, board)
                .Set(g => g.CurrentTurn, game.CurrentTurn)
                .Set(g => g.ContinueJumpFrom, game.ContinueJumpFrom);

            await _context.Games.UpdateOneAsync(g => g.Id == game.Id, update);
            await _hubContext.Clients.Group(move.GameId).SendAsync("GameUpdated", game);
            return game;
        }
    }
}

using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Checkers.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class GameController : Controller
    {
        private readonly UserService _userService;
        private readonly IGameService _gameService;

        public GameController(UserService userService, IGameService gameService)
        {
            _userService = userService;
            _gameService = gameService;
        }
        [HttpPost("create")]
        public async Task<ActionResult<Game>> CreateGame()
        {
            string? email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (String.IsNullOrEmpty(email))
                return Unauthorized();
            User user = await _userService.GetUserByEmail(email);
            if (user == null)
                return Unauthorized("User not found in db");
            Game game = await _gameService.CreateGameAsync(user.Id);
            return Ok(game);
        }
        [HttpGet("getGame")]
        public async Task<ActionResult<Game>> GetGame([FromQuery] string gameId)
        {
            Game? game = await _gameService.GetGameAsync(gameId);
            if (game == null)
            {
                return NotFound("Game not found");
            }
            return Ok(game);
        }
        [HttpPost("join")]
        public async Task<ActionResult<Game>> JoinGame([FromQuery] string gameId)
        {
            string? email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (String.IsNullOrEmpty(email))
                return Unauthorized();
            User user = await _userService.GetUserByEmail(email);
            if (user == null)
                return Unauthorized("User not found in db");
            bool success = await _gameService.JoinGameAsync(gameId, user.Id);
            if (!success)
            {
                return NotFound("Game not found or already has two players");
            }
            return Ok(new { message = "Joined successfully", gameId = gameId });

        }
        [HttpGet("openGames")]
        public async Task<ActionResult<List<Game>>> GetOpenGames()
        {
            List<Game> openGames = await _gameService.GetOpenGamesAsync();
            return Ok(openGames);
        }
        [HttpPost("makeMove")]
        public async Task<ActionResult<Game>> MakeMove([FromBody] MoveRequest move)
        {
            string? email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (String.IsNullOrEmpty(email))
                return Unauthorized();
            User user = await _userService.GetUserByEmail(email);
            if (user == null)
                return Unauthorized("User not found in db");
            try
            {
                Game updatedGame = await _gameService.MakeMoveAsync(user.Id, move);
                return Ok(updatedGame);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

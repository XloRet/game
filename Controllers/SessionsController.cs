using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuizGameShow.Data;
using QuizGameShow.DTOs;
using QuizGameShow.Hubs;
using QuizGameShow.Models;

namespace QuizGameShow.Controllers;

/// <summary>
/// API for creating and managing game sessions (rooms).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SessionsController(
    QuizDbContext db,
    IHubContext<QuizHub> hubContext) : ControllerBase
{
    // GET api/sessions
    [HttpGet]
    [ProducesResponseType<List<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionDto>>> GetAll()
    {
        var sessions = await db.GameSessions
            .Include(s => s.Quiz)
            .Where(s => s.IsActive)
            .Select(s => new SessionDto(
                s.Id,
                s.RoomCode,
                s.QuizId,
                s.Quiz!.Title,
                s.IsActive))
            .ToListAsync();

        return Ok(sessions);
    }

    // GET api/sessions/ABC123
    [HttpGet("{roomCode}")]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDto>> GetByCode(string roomCode)
    {
        var session = await db.GameSessions
            .Include(s => s.Quiz)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode.ToUpper() && s.IsActive);

        if (session is null)
            return NotFound($"Кімнату '{roomCode}' не знайдено.");

        return Ok(new SessionDto(
            session.Id, session.RoomCode, session.QuizId,
            session.Quiz!.Title, session.IsActive));
    }

    // POST api/sessions
    /// <summary>
    /// Creates a new game session for a quiz and returns the room code.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<SessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDto>> Create([FromBody] CreateSessionDto dto)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == dto.QuizId);

        if (quiz is null)
            return NotFound($"Тест #{dto.QuizId} не знайдено.");

        if (!quiz.Questions.Any())
            return BadRequest("Тест не має питань. Додайте питання перед запуском.");

        // Generate unique room code (format: ABC-123)
        string roomCode;
        do
        {
            roomCode = GenerateRoomCode();
        } while (await db.GameSessions.AnyAsync(s => s.RoomCode == roomCode && s.IsActive));

        var session = new GameSession
        {
            RoomCode = roomCode,
            QuizId = dto.QuizId,
            IsActive = true,
            State = GameState.Lobby
        };

        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var result = new SessionDto(session.Id, roomCode, dto.QuizId, quiz.Title, true);
        return CreatedAtAction(nameof(GetByCode), new { roomCode }, result);
    }

    // DELETE api/sessions/ABC123
    /// <summary>
    /// Closes a game session and notifies all connected players.
    /// </summary>
    [HttpDelete("{roomCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(string roomCode)
    {
        var session = await db.GameSessions
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode.ToUpper() && s.IsActive);

        if (session is null)
            return NotFound($"Кімнату '{roomCode}' не знайдено.");

        session.IsActive = false;
        session.State = GameState.Finished;
        await db.SaveChangesAsync();

        // Notify all players via SignalR
        await hubContext.Clients.Group(roomCode)
            .SendAsync("SessionClosed", "Ведучий закрив кімнату.");

        return NoContent();
    }

    // GET api/sessions/ABC123/leaderboard
    [HttpGet("{roomCode}/leaderboard")]
    [ProducesResponseType<List<LeaderboardEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard(string roomCode)
    {
        var session = await db.GameSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode.ToUpper());

        if (session is null) return NotFound();

        var leaderboard = session.Participants
            .Where(p => !p.IsHost)
            .OrderByDescending(p => p.TotalScore)
            .Select((p, i) => new LeaderboardEntryDto(p.Nickname, p.TotalScore, i + 1))
            .ToList();

        return Ok(leaderboard);
    }

    // ──────────────────────────────────────────────────────────────────────────
    private static string GenerateRoomCode()
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // exclude I,O to avoid confusion
        const string digits = "0123456789";
        var rng = Random.Shared;

        string part1 = new string(Enumerable.Range(0, 3).Select(_ => letters[rng.Next(letters.Length)]).ToArray());
        string part2 = new string(Enumerable.Range(0, 3).Select(_ => digits[rng.Next(digits.Length)]).ToArray());
        return $"{part1}-{part2}";
    }
}

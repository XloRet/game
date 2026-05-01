using System.ComponentModel.DataAnnotations;

namespace QuizGameShow.Models;

/// <summary>
/// Represents an active game session associated with a quiz.
/// </summary>
public class GameSession
{
    public int Id { get; set; }

    /// <summary>Unique 6-digit room code shown to players (e.g., "529-103").</summary>
    [Required, MaxLength(10)]
    public string RoomCode { get; set; } = string.Empty;

    public int QuizId { get; set; }

    public bool IsActive { get; set; } = true;

    public GameState State { get; set; } = GameState.Lobby;

    /// <summary>Index of the question currently displayed (-1 = lobby).</summary>
    public int CurrentQuestionIndex { get; set; } = -1;

    /// <summary>UTC time when the current question was shown to players.</summary>
    public DateTime? QuestionStartedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Quiz? Quiz { get; set; }
    public List<Participant> Participants { get; set; } = [];
}

/// <summary>
/// Lifecycle states of a game session.
/// </summary>
public enum GameState
{
    Lobby,
    Question,
    ShowingResults,
    Finished
}

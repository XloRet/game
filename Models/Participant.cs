using System.ComponentModel.DataAnnotations;

namespace QuizGameShow.Models;

/// <summary>
/// Represents a player who joined a game session.
/// </summary>
public class Participant
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    [Required, MaxLength(50)]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>SignalR connection ID for targeted messaging.</summary>
    [MaxLength(100)]
    public string ConnectionId { get; set; } = string.Empty;

    public int TotalScore { get; set; } = 0;

    public bool IsHost { get; set; } = false;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public GameSession? Session { get; set; }
    public List<PlayerAnswer> PlayerAnswers { get; set; } = [];
}

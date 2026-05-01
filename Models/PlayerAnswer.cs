namespace QuizGameShow.Models;

/// <summary>
/// Records a player's answer to a specific question, including timing information.
/// </summary>
public class PlayerAnswer
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }

    public int QuestionId { get; set; }

    public int AnswerId { get; set; }

    /// <summary>Whether the selected answer was correct.</summary>
    public bool IsCorrect { get; set; }

    /// <summary>Points awarded for this answer based on speed.</summary>
    public int PointsAwarded { get; set; }

    /// <summary>How many seconds elapsed before the player answered.</summary>
    public double SecondsElapsed { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Participant? Participant { get; set; }
    public Question? Question { get; set; }
    public Answer? Answer { get; set; }
}

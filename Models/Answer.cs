using System.ComponentModel.DataAnnotations;

namespace QuizGameShow.Models;

/// <summary>
/// Represents an answer option for a question.
/// </summary>
public class Answer
{
    public int Id { get; set; }

    public int QuestionId { get; set; }

    [Required, MaxLength(300)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Whether this answer is the correct one.</summary>
    public bool IsCorrect { get; set; }

    // Navigation property
    public Question? Question { get; set; }
}

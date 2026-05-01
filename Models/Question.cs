using System.ComponentModel.DataAnnotations;

namespace QuizGameShow.Models;

/// <summary>
/// Represents a single question in a quiz.
/// </summary>
public class Question
{
    public int Id { get; set; }

    public int QuizId { get; set; }

    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Time limit in seconds to answer the question.</summary>
    public int TimeLimit { get; set; } = 20;

    /// <summary>Maximum points awarded for a correct answer.</summary>
    public int MaxPoints { get; set; } = 1000;

    public int OrderIndex { get; set; }

    // Navigation properties
    public Quiz? Quiz { get; set; }
    public List<Answer> Answers { get; set; } = [];
}

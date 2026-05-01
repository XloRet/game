using System.ComponentModel.DataAnnotations;

namespace QuizGameShow.Models;

/// <summary>
/// Represents a quiz created by a host.
/// </summary>
public class Quiz
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = "Admin";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public List<Question> Questions { get; set; } = [];
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizGameShow.Data;
using QuizGameShow.DTOs;
using QuizGameShow.Models;

namespace QuizGameShow.Controllers;

/// <summary>
/// CRUD API for managing quizzes and their questions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class QuizzesController(QuizDbContext db) : ControllerBase
{
    // GET api/quizzes
    [HttpGet]
    [ProducesResponseType<List<QuizSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<QuizSummaryDto>>> GetAll()
    {
        var quizzes = await db.Quizzes
            .Include(q => q.Questions)
            .Select(q => new QuizSummaryDto(
                q.Id,
                q.Title,
                q.Description,
                q.Questions.Count,
                q.CreatedAt))
            .ToListAsync();

        return Ok(quizzes);
    }

    // GET api/quizzes/5
    [HttpGet("{id:int}")]
    [ProducesResponseType<QuizDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizDetailDto>> GetById(int id)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions.OrderBy(q => q.OrderIndex))
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz is null) return NotFound($"Тест #{id} не знайдено.");

        var dto = new QuizDetailDto(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.Questions.Select(q => new QuestionDto(
                q.Id,
                q.Text,
                q.TimeLimit,
                q.MaxPoints,
                q.OrderIndex,
                q.Answers.Select(a => new AnswerDto(a.Id, a.Text, a.IsCorrect)).ToList()
            )).ToList()
        );

        return Ok(dto);
    }

    // POST api/quizzes
    [HttpPost]
    [ProducesResponseType<QuizSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuizSummaryDto>> Create([FromBody] CreateQuizDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!dto.Questions.Any())
            return BadRequest("Тест повинен мати хоча б одне питання.");

        foreach (var (q, i) in dto.Questions.Select((q, i) => (q, i)))
        {
            if (q.Answers.Count < 2 || q.Answers.Count > 4)
                return BadRequest($"Питання #{i + 1} повинно мати від 2 до 4 відповідей.");

            if (!q.Answers.Any(a => a.IsCorrect))
                return BadRequest($"Питання #{i + 1} не має правильної відповіді.");

            if (q.Answers.Count(a => a.IsCorrect) > 1)
                return BadRequest($"Питання #{i + 1} має більше однієї правильної відповіді.");
        }

        var quiz = new Quiz
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Questions = dto.Questions.Select((q, i) => new Question
            {
                Text = q.Text.Trim(),
                TimeLimit = Math.Clamp(q.TimeLimit, 5, 120),
                MaxPoints = Math.Clamp(q.MaxPoints, 100, 2000),
                OrderIndex = i,
                Answers = q.Answers.Select(a => new Answer
                {
                    Text = a.Text.Trim(),
                    IsCorrect = a.IsCorrect
                }).ToList()
            }).ToList()
        };

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var result = new QuizSummaryDto(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.Questions.Count,
            quiz.CreatedAt);

        return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, result);
    }

    // PUT api/quizzes/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateQuizDto dto)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz is null) return NotFound($"Тест #{id} не знайдено.");

        quiz.Title = dto.Title.Trim();
        quiz.Description = dto.Description.Trim();

        // Replace all questions
        db.Questions.RemoveRange(quiz.Questions);
        quiz.Questions = dto.Questions.Select((q, i) => new Question
        {
            Text = q.Text.Trim(),
            TimeLimit = Math.Clamp(q.TimeLimit, 5, 120),
            MaxPoints = Math.Clamp(q.MaxPoints, 100, 2000),
            OrderIndex = i,
            Answers = q.Answers.Select(a => new Answer
            {
                Text = a.Text.Trim(),
                IsCorrect = a.IsCorrect
            }).ToList()
        }).ToList();

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE api/quizzes/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var quiz = await db.Quizzes.FindAsync(id);
        if (quiz is null) return NotFound($"Тест #{id} не знайдено.");

        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

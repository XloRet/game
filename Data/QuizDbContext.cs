using Microsoft.EntityFrameworkCore;
using QuizGameShow.Models;

namespace QuizGameShow.Data;

/// <summary>
/// Entity Framework database context for the Quiz Game Show application.
/// </summary>
public class QuizDbContext(DbContextOptions<QuizDbContext> options) : DbContext(options)
{
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<PlayerAnswer> PlayerAnswers => Set<PlayerAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Quiz -> Questions (cascade delete)
        modelBuilder.Entity<Question>()
            .HasOne(q => q.Quiz)
            .WithMany(qz => qz.Questions)
            .HasForeignKey(q => q.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        // Question -> Answers (cascade delete)
        modelBuilder.Entity<Answer>()
            .HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // GameSession -> Participants (cascade delete)
        modelBuilder.Entity<Participant>()
            .HasOne(p => p.Session)
            .WithMany(s => s.Participants)
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Participant -> PlayerAnswers (cascade delete)
        modelBuilder.Entity<PlayerAnswer>()
            .HasOne(pa => pa.Participant)
            .WithMany(p => p.PlayerAnswers)
            .HasForeignKey(pa => pa.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique room code index
        modelBuilder.Entity<GameSession>()
            .HasIndex(s => s.RoomCode)
            .IsUnique();

        // Seed sample data for demonstration
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Sample quiz
        modelBuilder.Entity<Quiz>().HasData(new Quiz
        {
            Id = 1,
            Title = "Загальні знання",
            Description = "Перевір свої знання з різних тем!",
            CreatedBy = "Admin",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Sample questions
        modelBuilder.Entity<Question>().HasData(
            new Question { Id = 1, QuizId = 1, Text = "Яка столиця України?", TimeLimit = 15, MaxPoints = 1000, OrderIndex = 0 },
            new Question { Id = 2, QuizId = 1, Text = "Скільки планет у Сонячній системі?", TimeLimit = 20, MaxPoints = 1000, OrderIndex = 1 },
            new Question { Id = 3, QuizId = 1, Text = "Яка хімічна формула води?", TimeLimit = 15, MaxPoints = 1000, OrderIndex = 2 },
            new Question { Id = 4, QuizId = 1, Text = "Хто написав 'Кобзар'?", TimeLimit = 15, MaxPoints = 1000, OrderIndex = 3 },
            new Question { Id = 5, QuizId = 1, Text = "Що є найбільшою країною світу за площею?", TimeLimit = 20, MaxPoints = 1000, OrderIndex = 4 }
        );

        // Sample answers
        modelBuilder.Entity<Answer>().HasData(
            // Q1: Capital of Ukraine
            new Answer { Id = 1, QuestionId = 1, Text = "Харків", IsCorrect = false },
            new Answer { Id = 2, QuestionId = 1, Text = "Київ", IsCorrect = true },
            new Answer { Id = 3, QuestionId = 1, Text = "Львів", IsCorrect = false },
            new Answer { Id = 4, QuestionId = 1, Text = "Одеса", IsCorrect = false },

            // Q2: Planets in Solar System
            new Answer { Id = 5, QuestionId = 2, Text = "7", IsCorrect = false },
            new Answer { Id = 6, QuestionId = 2, Text = "8", IsCorrect = true },
            new Answer { Id = 7, QuestionId = 2, Text = "9", IsCorrect = false },
            new Answer { Id = 8, QuestionId = 2, Text = "10", IsCorrect = false },

            // Q3: Formula of water
            new Answer { Id = 9, QuestionId = 3, Text = "CO2", IsCorrect = false },
            new Answer { Id = 10, QuestionId = 3, Text = "H2O2", IsCorrect = false },
            new Answer { Id = 11, QuestionId = 3, Text = "H2O", IsCorrect = true },
            new Answer { Id = 12, QuestionId = 3, Text = "NaCl", IsCorrect = false },

            // Q4: Who wrote Kobzar
            new Answer { Id = 13, QuestionId = 4, Text = "Іван Франко", IsCorrect = false },
            new Answer { Id = 14, QuestionId = 4, Text = "Леся Українка", IsCorrect = false },
            new Answer { Id = 15, QuestionId = 4, Text = "Тарас Шевченко", IsCorrect = true },
            new Answer { Id = 16, QuestionId = 4, Text = "Михайло Коцюбинський", IsCorrect = false },

            // Q5: Largest country
            new Answer { Id = 17, QuestionId = 5, Text = "Канада", IsCorrect = false },
            new Answer { Id = 18, QuestionId = 5, Text = "Китай", IsCorrect = false },
            new Answer { Id = 19, QuestionId = 5, Text = "Росія", IsCorrect = true },
            new Answer { Id = 20, QuestionId = 5, Text = "США", IsCorrect = false }
        );
    }
}

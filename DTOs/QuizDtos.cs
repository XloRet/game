namespace QuizGameShow.DTOs;

// ─── Quiz DTOs ────────────────────────────────────────────────────────────────

public record CreateQuizDto(
    string Title,
    string Description,
    List<CreateQuestionDto> Questions
);

public record QuizSummaryDto(
    int Id,
    string Title,
    string Description,
    int QuestionCount,
    DateTime CreatedAt
);

public record QuizDetailDto(
    int Id,
    string Title,
    string Description,
    List<QuestionDto> Questions
);

// ─── Question DTOs ────────────────────────────────────────────────────────────

public record CreateQuestionDto(
    string Text,
    int TimeLimit,
    int MaxPoints,
    List<CreateAnswerDto> Answers
);

public record QuestionDto(
    int Id,
    string Text,
    int TimeLimit,
    int MaxPoints,
    int OrderIndex,
    List<AnswerDto> Answers
);

/// <summary>
/// Question data sent to players — does NOT reveal which answer is correct.
/// </summary>
public record QuestionForPlayerDto(
    int Id,
    string Text,
    int TimeLimit,
    int MaxPoints,
    List<AnswerForPlayerDto> Answers
);

// ─── Answer DTOs ──────────────────────────────────────────────────────────────

public record CreateAnswerDto(
    string Text,
    bool IsCorrect
);

public record AnswerDto(
    int Id,
    string Text,
    bool IsCorrect
);

/// <summary>
/// Answer data sent to players — IsCorrect is hidden.
/// </summary>
public record AnswerForPlayerDto(
    int Id,
    string Text
);

// ─── Session DTOs ─────────────────────────────────────────────────────────────

public record CreateSessionDto(int QuizId);

public record SessionDto(
    int Id,
    string RoomCode,
    int QuizId,
    string QuizTitle,
    bool IsActive
);

// ─── Player DTOs ──────────────────────────────────────────────────────────────

public record JoinSessionDto(
    string RoomCode,
    string Nickname
);

public record LeaderboardEntryDto(
    string Nickname,
    int TotalScore,
    int Rank
);

public record AnswerResultDto(
    bool IsCorrect,
    int PointsAwarded,
    int TotalScore,
    string CorrectAnswerText
);

public record SubmitAnswerDto(
    string RoomCode,
    int AnswerId
);

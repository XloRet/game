using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuizGameShow.Data;
using QuizGameShow.DTOs;
using QuizGameShow.Models;

namespace QuizGameShow.Hubs;

/// <summary>
/// SignalR Hub — the real-time engine of the Quiz Game Show.
///
/// Group naming convention:
///   "{roomCode}"       — all players in the room
///   "{roomCode}_host"  — only the host connection
/// </summary>
public class QuizHub(QuizDbContext db, ILogger<QuizHub> logger) : Hub
{
    // ──────────────────────────────────────────────────────────────────────────
    // CONNECTION EVENTS
    // ──────────────────────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Mark participant as disconnected (keep their score in DB)
        var participant = await db.Participants
            .FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null)
        {
            var session = await db.GameSessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.Id == participant.SessionId);

            logger.LogInformation(
                "Participant '{Nickname}' disconnected from room {RoomCode}",
                participant.Nickname, session?.RoomCode);

            if (session != null)
            {
                // Notify remaining players
                await Clients.OthersInGroup(session.RoomCode)
                    .SendAsync("PlayerLeft", participant.Nickname);

                // Update host's player list
                var leaderboard = BuildLeaderboard(session.Participants);
                await Clients.Group(session.RoomCode + "_host")
                    .SendAsync("UpdatePlayerList", leaderboard);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LOBBY METHODS
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Player joins a game room using a room code and a chosen nickname.
    /// Called by: Player UI
    /// </summary>
    public async Task JoinRoom(string roomCode, string nickname)
    {
        roomCode = roomCode.Trim().ToUpper();
        nickname = nickname.Trim();

        if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > 50)
        {
            await Clients.Caller.SendAsync("Error", "Нікнейм повинен бути від 1 до 50 символів.");
            return;
        }

        var session = await db.GameSessions
            .Include(s => s.Participants)
            .Include(s => s.Quiz)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode && s.IsActive);

        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", $"Кімнату '{roomCode}' не знайдено або гру вже завершено.");
            return;
        }

        if (session.State != GameState.Lobby)
        {
            await Clients.Caller.SendAsync("Error", "Гра вже розпочалась. Приєднатись не можна.");
            return;
        }

        // Prevent duplicate nicknames in the same session
        bool nicknameTaken = session.Participants
            .Any(p => p.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase) && !p.IsHost);

        if (nicknameTaken)
        {
            await Clients.Caller.SendAsync("Error", $"Нікнейм '{nickname}' вже зайнятий. Оберіть інший.");
            return;
        }

        // Add participant to DB
        var participant = new Participant
        {
            SessionId = session.Id,
            Nickname = nickname,
            ConnectionId = Context.ConnectionId,
            IsHost = false
        };

        db.Participants.Add(participant);
        await db.SaveChangesAsync();

        // Add to SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        logger.LogInformation("Player '{Nickname}' joined room {RoomCode}", nickname, roomCode);

        // Tell the joining player they succeeded
        await Clients.Caller.SendAsync("JoinedRoom", new
        {
            roomCode,
            nickname,
            quizTitle = session.Quiz?.Title,
            participantId = participant.Id
        });

        // Tell everyone (including host) a new player arrived
        await Clients.Group(roomCode).SendAsync("PlayerJoined", nickname);

        // Send updated leaderboard to host
        session.Participants.Add(participant);
        var leaderboard = BuildLeaderboard(session.Participants.Where(p => !p.IsHost).ToList());
        await Clients.Group(roomCode + "_host").SendAsync("UpdatePlayerList", leaderboard);
    }

    /// <summary>
    /// Host connects to the session management group.
    /// Called by: Host Panel
    /// </summary>
    public async Task HostJoinRoom(string roomCode)
    {
        roomCode = roomCode.Trim().ToUpper();

        var session = await db.GameSessions
            .Include(s => s.Quiz)
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode && s.IsActive);

        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", $"Сесію '{roomCode}' не знайдено.");
            return;
        }

        // Add host to both the player group and the host-only group
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode + "_host");

        logger.LogInformation("Host connected to room {RoomCode}", roomCode);

        var players = session.Participants.Where(p => !p.IsHost).ToList();
        await Clients.Caller.SendAsync("HostJoined", new
        {
            roomCode,
            quizTitle = session.Quiz?.Title,
            playerCount = players.Count,
            players = BuildLeaderboard(players)
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GAME FLOW METHODS (Host only)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host starts the game. Sends the first question to all players.
    /// Called by: Host Panel
    /// </summary>
    public async Task StartGame(string roomCode)
    {
        var session = await db.GameSessions
            .Include(s => s.Quiz)
                .ThenInclude(q => q!.Questions.OrderBy(q => q.OrderIndex))
                    .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode && s.IsActive);

        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", "Сесію не знайдено.");
            return;
        }

        if (session.State != GameState.Lobby)
        {
            await Clients.Caller.SendAsync("Error", "Гра вже запущена.");
            return;
        }

        if (!session.Quiz!.Questions.Any())
        {
            await Clients.Caller.SendAsync("Error", "У цьому тесті немає питань.");
            return;
        }

        session.State = GameState.Question;
        session.CurrentQuestionIndex = 0;
        session.QuestionStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Game started in room {RoomCode}", roomCode);

        var question = session.Quiz.Questions.ElementAt(0);
        await SendQuestionToAll(roomCode, question, 0, session.Quiz.Questions.Count);
    }

    /// <summary>
    /// Host moves to the next question. If no more questions → finish game.
    /// Called by: Host Panel after reviewing results
    /// </summary>
    public async Task NextQuestion(string roomCode)
    {
        var session = await db.GameSessions
            .Include(s => s.Quiz)
                .ThenInclude(q => q!.Questions.OrderBy(q => q.OrderIndex))
                    .ThenInclude(q => q.Answers)
            .Include(s => s.Participants)
                .ThenInclude(p => p.PlayerAnswers)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode && s.IsActive);

        if (session == null) return;

        int nextIndex = session.CurrentQuestionIndex + 1;
        int totalQuestions = session.Quiz!.Questions.Count;

        if (nextIndex >= totalQuestions)
        {
            // Game over
            await FinishGame(session, roomCode);
            return;
        }

        session.State = GameState.Question;
        session.CurrentQuestionIndex = nextIndex;
        session.QuestionStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var question = session.Quiz.Questions.ElementAt(nextIndex);
        await SendQuestionToAll(roomCode, question, nextIndex, totalQuestions);
    }

    /// <summary>
    /// Host explicitly shows the current question's result/leaderboard.
    /// Called by: Host Panel
    /// </summary>
    public async Task ShowResults(string roomCode)
    {
        var session = await db.GameSessions
            .Include(s => s.Quiz)
                .ThenInclude(q => q!.Questions.OrderBy(q => q.OrderIndex))
                    .ThenInclude(q => q.Answers)
            .Include(s => s.Participants)
                .ThenInclude(p => p.PlayerAnswers)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode && s.IsActive);

        if (session == null) return;

        session.State = GameState.ShowingResults;
        await db.SaveChangesAsync();

        var currentQuestion = session.Quiz!.Questions.ElementAt(session.CurrentQuestionIndex);
        var correctAnswer = currentQuestion.Answers.FirstOrDefault(a => a.IsCorrect);

        // Build per-answer stats
        var answerStats = currentQuestion.Answers.Select(a => new
        {
            answerId = a.Id,
            text = a.Text,
            isCorrect = a.IsCorrect,
            count = session.Participants
                .SelectMany(p => p.PlayerAnswers)
                .Count(pa => pa.AnswerId == a.Id && pa.QuestionId == currentQuestion.Id)
        }).ToList();

        var leaderboard = BuildLeaderboard(session.Participants.Where(p => !p.IsHost).ToList());

        // Send to all players
        await Clients.Group(roomCode).SendAsync("ShowResults", new
        {
            correctAnswerId = correctAnswer?.Id,
            correctAnswerText = correctAnswer?.Text,
            answerStats,
            leaderboard,
            isLastQuestion = session.CurrentQuestionIndex >= session.Quiz.Questions.Count - 1
        });

        logger.LogInformation(
            "Results shown for question {Index} in room {RoomCode}",
            session.CurrentQuestionIndex, roomCode);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PLAYER ANSWER SUBMISSION
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Player submits their answer for the current question.
    /// Called by: Player UI
    /// </summary>
    public async Task SendAnswer(string roomCode, int answerId)
    {
        var participant = await db.Participants
            .Include(p => p.Session)
                .ThenInclude(s => s!.Quiz)
                    .ThenInclude(q => q!.Questions.OrderBy(q => q.OrderIndex))
                        .ThenInclude(q => q.Answers)
            .Include(p => p.PlayerAnswers)
            .FirstOrDefaultAsync(p =>
                p.ConnectionId == Context.ConnectionId &&
                p.Session!.RoomCode == roomCode);

        if (participant == null)
        {
            await Clients.Caller.SendAsync("Error", "Гравця не знайдено в цій кімнаті.");
            return;
        }

        var session = participant.Session!;

        if (session.State != GameState.Question)
        {
            await Clients.Caller.SendAsync("Error", "Зараз не час для відповіді.");
            return;
        }

        var currentQuestion = session.Quiz!.Questions.ElementAt(session.CurrentQuestionIndex);

        // Prevent answering the same question twice
        bool alreadyAnswered = participant.PlayerAnswers
            .Any(pa => pa.QuestionId == currentQuestion.Id);

        if (alreadyAnswered)
        {
            await Clients.Caller.SendAsync("Error", "Ви вже відповіли на це питання.");
            return;
        }

        var selectedAnswer = currentQuestion.Answers.FirstOrDefault(a => a.Id == answerId);
        if (selectedAnswer == null)
        {
            await Clients.Caller.SendAsync("Error", "Недійсна відповідь.");
            return;
        }

        // Calculate score based on speed
        double secondsElapsed = (DateTime.UtcNow - (session.QuestionStartedAt ?? DateTime.UtcNow)).TotalSeconds;
        int points = CalculatePoints(selectedAnswer.IsCorrect, secondsElapsed, currentQuestion.MaxPoints, currentQuestion.TimeLimit);

        var playerAnswer = new PlayerAnswer
        {
            ParticipantId = participant.Id,
            QuestionId = currentQuestion.Id,
            AnswerId = answerId,
            IsCorrect = selectedAnswer.IsCorrect,
            PointsAwarded = points,
            SecondsElapsed = secondsElapsed
        };

        db.PlayerAnswers.Add(playerAnswer);
        participant.TotalScore += points;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Player '{Nickname}' answered Q{QuestionId} in {Seconds:F1}s — {Points} pts",
            participant.Nickname, currentQuestion.Id, secondsElapsed, points);

        // Send personal result to player
        var correctAnswer = currentQuestion.Answers.First(a => a.IsCorrect);
        await Clients.Caller.SendAsync("AnswerResult", new AnswerResultDto(
            selectedAnswer.IsCorrect,
            points,
            participant.TotalScore,
            correctAnswer.Text
        ));

        // Notify host that someone answered (without revealing who chose what)
        var totalAnswered = await db.PlayerAnswers
            .CountAsync(pa => pa.QuestionId == currentQuestion.Id);

        var totalPlayers = await db.Participants
            .CountAsync(p => p.SessionId == session.Id && !p.IsHost);

        await Clients.Group(roomCode + "_host").SendAsync("PlayerAnswered", new
        {
            totalAnswered,
            totalPlayers
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task SendQuestionToAll(string roomCode, Question question, int questionIndex, int totalQuestions)
    {
        var questionForPlayer = new QuestionForPlayerDto(
            question.Id,
            question.Text,
            question.TimeLimit,
            question.MaxPoints,
            question.Answers.Select(a => new AnswerForPlayerDto(a.Id, a.Text)).ToList()
        );

        var hostQuestion = new QuestionDto(
            question.Id,
            question.Text,
            question.TimeLimit,
            question.MaxPoints,
            questionIndex,
            question.Answers.Select(a => new AnswerDto(a.Id, a.Text, a.IsCorrect)).ToList()
        );

        // Notify all clients about the new question
        await Clients.Group(roomCode).SendAsync("ShowQuestion", new
        {
            questionForPlayer,
            questionIndex,
            totalQuestions
        });

        // Also send the full version (with correct answer) to the host
        await Clients.Group(roomCode + "_host").SendAsync("ShowQuestionHost", new
        {
            hostQuestion,
            questionIndex,
            totalQuestions
        });

        logger.LogInformation(
            "Question {Index}/{Total} shown in room {RoomCode}",
            questionIndex + 1, totalQuestions, roomCode);
    }

    private async Task FinishGame(GameSession session, string roomCode)
    {
        session.State = GameState.Finished;
        session.IsActive = false;
        await db.SaveChangesAsync();

        var finalLeaderboard = BuildLeaderboard(
            session.Participants.Where(p => !p.IsHost).ToList());

        await Clients.Group(roomCode).SendAsync("GameFinished", new
        {
            leaderboard = finalLeaderboard,
            winner = finalLeaderboard.FirstOrDefault()
        });

        logger.LogInformation("Game finished in room {RoomCode}. Winner: {Winner}",
            roomCode, finalLeaderboard.FirstOrDefault()?.Nickname);
    }

    /// <summary>
    /// Time-based scoring: faster correct answers earn more points.
    /// Formula: points = maxPoints * (1 - 0.5 * elapsed / timeLimit)
    /// Minimum: 100 points for a correct answer regardless of speed.
    /// </summary>
    private static int CalculatePoints(bool isCorrect, double secondsElapsed, int maxPoints, int timeLimit)
    {
        if (!isCorrect) return 0;

        double ratio = Math.Clamp(secondsElapsed / timeLimit, 0, 1);
        int points = (int)(maxPoints * (1.0 - 0.5 * ratio));

        return Math.Max(points, 100); // Minimum 100 points for correct answer
    }

    private static List<LeaderboardEntryDto> BuildLeaderboard(IEnumerable<Participant> participants)
    {
        return participants
            .OrderByDescending(p => p.TotalScore)
            .Select((p, i) => new LeaderboardEntryDto(p.Nickname, p.TotalScore, i + 1))
            .ToList();
    }
}

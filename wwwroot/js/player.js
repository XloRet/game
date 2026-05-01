/**
 * Quiz Game Show — Player JavaScript
 * Handles SignalR connection and all player-side game logic.
 */

'use strict';

// ──────────────────────────────────────────────────────────────────────────────
// STATE
// ──────────────────────────────────────────────────────────────────────────────
let connection = null;
let myNickname = '';
let myRoomCode = '';
let currentScore = 0;
let timerInterval = null;
let timerSeconds = 0;

const CIRCUMFERENCE = 2 * Math.PI * 26; // SVG circle r=26

// ──────────────────────────────────────────────────────────────────────────────
// SCREEN MANAGEMENT
// ──────────────────────────────────────────────────────────────────────────────
function showScreen(id) {
  document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
  document.getElementById(id).classList.add('active');
}

// ──────────────────────────────────────────────────────────────────────────────
// CONNECTION
// ──────────────────────────────────────────────────────────────────────────────
function buildConnection() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/quiz')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // ── Server → Client events ──────────────────────────────────────────────────

  connection.on('JoinedRoom', (data) => {
    myNickname = data.nickname;
    myRoomCode = data.roomCode;
    document.getElementById('lobby-quiz-title').textContent = data.quizTitle || 'Завантаження...';
    document.getElementById('lobby-room-code').textContent = data.roomCode;
    document.getElementById('lobby-nickname').textContent = data.nickname;
    showScreen('screen-lobby');
  });

  connection.on('PlayerJoined', (nickname) => {
    addPlayerChip(nickname);
  });

  connection.on('PlayerLeft', (nickname) => {
    removePlayerChip(nickname);
  });

  connection.on('ShowQuestion', (data) => {
    clearTimer();
    document.getElementById('answered-overlay').classList.add('hidden');
    renderQuestion(data);
    showScreen('screen-question');
  });

  connection.on('AnswerResult', (result) => {
    clearTimer();
    currentScore = result.totalScore;
    document.getElementById('score-display').textContent = currentScore;

    // Show overlay while waiting for host
    document.getElementById('answered-overlay').classList.remove('hidden');
    document.getElementById('answered-icon').textContent = result.isCorrect ? '✅' : '❌';
    document.getElementById('answered-msg').textContent = result.isCorrect
      ? `Правильно! +${result.pointsAwarded} балів`
      : `Неправильно. Правильна відповідь: ${result.correctAnswerText}`;
  });

  connection.on('ShowResults', (data) => {
    clearTimer();
    // Show result screen then leaderboard
    renderResultScreen(data);
    setTimeout(() => {
      renderLeaderboard(data.leaderboard);
      showScreen('screen-leaderboard');
    }, 3500);
  });

  connection.on('GameFinished', (data) => {
    clearTimer();
    renderGameOver(data);
    showScreen('screen-gameover');
  });

  connection.on('SessionClosed', (msg) => {
    clearTimer();
    showError(msg || 'Ведучий закрив кімнату.');
    showScreen('screen-join');
    resetState();
  });

  connection.on('Error', (msg) => {
    showError(msg);
  });

  connection.onreconnecting(() => {
    console.warn('[SignalR] Reconnecting...');
  });
  connection.onreconnected(() => {
    console.info('[SignalR] Reconnected.');
  });
  connection.onclose(() => {
    console.warn('[SignalR] Connection closed.');
  });
}

// ──────────────────────────────────────────────────────────────────────────────
// JOIN ROOM
// ──────────────────────────────────────────────────────────────────────────────
async function joinRoom() {
  const code = document.getElementById('input-room-code').value.trim().toUpperCase();
  const name = document.getElementById('input-nickname').value.trim();
  const btn  = document.getElementById('btn-join');

  hideError();

  if (!code) { showError('Введіть код кімнати.'); return; }
  if (!name)  { showError('Введіть нікнейм.'); return; }

  btn.disabled = true;
  btn.textContent = 'Підключення...';

  try {
    if (!connection || connection.state === signalR.HubConnectionState.Disconnected) {
      buildConnection();
      await connection.start();
      console.info('[SignalR] Connected.');
    }
    await connection.invoke('JoinRoom', code, name);
  } catch (err) {
    console.error('[SignalR] JoinRoom error:', err);
    showError('Помилка підключення. Спробуйте ще раз.');
  } finally {
    btn.disabled = false;
    btn.textContent = 'Увійти в гру 🚀';
  }
}

// ──────────────────────────────────────────────────────────────────────────────
// QUESTION RENDERING
// ──────────────────────────────────────────────────────────────────────────────
function renderQuestion(data) {
  const { questionForPlayer, questionIndex, totalQuestions } = data;

  document.getElementById('q-current').textContent = questionIndex + 1;
  document.getElementById('q-total').textContent = totalQuestions;
  document.getElementById('question-text').textContent = questionForPlayer.text;
  document.getElementById('score-display').textContent = currentScore;

  const grid = document.getElementById('answers-grid');
  grid.innerHTML = '';

  questionForPlayer.answers.forEach((ans, i) => {
    const btn = document.createElement('button');
    btn.className = 'answer-btn';
    btn.dataset.index = i;
    btn.dataset.answerId = ans.id;
    btn.textContent = ans.text;
    btn.id = `answer-btn-${ans.id}`;
    btn.addEventListener('click', () => submitAnswer(ans.id, btn));
    grid.appendChild(btn);
  });

  startTimer(questionForPlayer.timeLimit);
}

// ──────────────────────────────────────────────────────────────────────────────
// ANSWER SUBMISSION
// ──────────────────────────────────────────────────────────────────────────────
async function submitAnswer(answerId, btn) {
  // Disable all buttons immediately
  document.querySelectorAll('.answer-btn').forEach(b => {
    b.disabled = true;
    b.classList.remove('selected');
  });
  btn.classList.add('selected');

  clearTimer();

  try {
    await connection.invoke('SendAnswer', myRoomCode, answerId);
  } catch (err) {
    console.error('[SignalR] SendAnswer error:', err);
  }
}

// ──────────────────────────────────────────────────────────────────────────────
// TIMER
// ──────────────────────────────────────────────────────────────────────────────
function startTimer(seconds) {
  timerSeconds = seconds;
  const timerText   = document.getElementById('timer-text');
  const timerCircle = document.getElementById('timer-circle');
  const timerColor  = () => timerSeconds > 5 ? '#f59e0b' : '#ef4444';

  timerText.textContent = timerSeconds;
  timerCircle.style.strokeDashoffset = 0;
  timerCircle.style.stroke = timerColor();

  timerInterval = setInterval(() => {
    timerSeconds--;
    const progress = 1 - timerSeconds / seconds;
    timerCircle.style.strokeDashoffset = CIRCUMFERENCE * progress;
    timerCircle.style.stroke = timerColor();
    timerText.textContent = timerSeconds;

    if (timerSeconds <= 0) {
      clearTimer();
      // Auto-show "time's up" overlay
      document.querySelectorAll('.answer-btn').forEach(b => b.disabled = true);
      document.getElementById('answered-overlay').classList.remove('hidden');
      document.getElementById('answered-icon').textContent = '⏰';
      document.getElementById('answered-msg').textContent = 'Час вийшов!';
    }
  }, 1000);
}

function clearTimer() {
  if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }
}

// ──────────────────────────────────────────────────────────────────────────────
// RESULT SCREEN
// ──────────────────────────────────────────────────────────────────────────────
function renderResultScreen(data) {
  // We get this after ShowResults — already switched to answered overlay,
  // so re-render the full result screen from AnswerResult data stored in DOM
  const isCorrect = document.getElementById('answered-icon').textContent === '✅';
  const pointsText = document.getElementById('answered-msg').textContent;

  document.getElementById('result-icon').textContent  = isCorrect ? '✅' : '❌';
  document.getElementById('result-title').textContent = isCorrect ? 'Правильно!' : 'Неправильно!';
  document.getElementById('result-correct-answer').textContent =
    `Правильна відповідь: ${data.correctAnswerText}`;
  document.getElementById('result-total').textContent = currentScore;

  // Extract points from message if available
  const match = pointsText.match(/\+(\d+)/);
  document.getElementById('result-points').textContent = match ? `+${match[1]}` : '+0';
  showScreen('screen-result');
}

// ──────────────────────────────────────────────────────────────────────────────
// LEADERBOARD RENDERING
// ──────────────────────────────────────────────────────────────────────────────
function renderLeaderboard(entries) {
  const list = document.getElementById('leaderboard-list');
  list.innerHTML = '';

  const medals = ['🥇', '🥈', '🥉'];
  entries.slice(0, 10).forEach((entry, i) => {
    const item = document.createElement('div');
    item.className = 'leaderboard-item';
    item.style.animationDelay = `${i * 60}ms`;
    item.innerHTML = `
      <span class="lb-rank">${medals[i] ?? entry.rank}</span>
      <span class="lb-name">${escHtml(entry.nickname)}</span>
      <span class="lb-score">${entry.totalScore} балів</span>
    `;
    list.appendChild(item);
  });
}

// ──────────────────────────────────────────────────────────────────────────────
// GAME OVER
// ──────────────────────────────────────────────────────────────────────────────
function renderGameOver(data) {
  const title = data.winner
    ? `🏆 Переможець: ${escHtml(data.winner.nickname)}!`
    : 'Ніхто не переміг';
  document.getElementById('gameover-title').textContent = 'Гру завершено!';
  document.getElementById('winner-text').textContent = title;
  renderFinalLeaderboard(data.leaderboard);
}

function renderFinalLeaderboard(entries) {
  const list = document.getElementById('final-leaderboard');
  list.innerHTML = '';
  const medals = ['🥇', '🥈', '🥉'];
  (entries || []).slice(0, 10).forEach((e, i) => {
    const item = document.createElement('div');
    item.className = 'leaderboard-item';
    item.innerHTML = `
      <span class="lb-rank">${medals[i] ?? e.rank}</span>
      <span class="lb-name">${escHtml(e.nickname)}</span>
      <span class="lb-score">${e.totalScore} балів</span>
    `;
    list.appendChild(item);
  });
}

// ──────────────────────────────────────────────────────────────────────────────
// PLAYER CHIPS (Lobby)
// ──────────────────────────────────────────────────────────────────────────────
function addPlayerChip(nickname) {
  const container = document.getElementById('player-list-container');
  const chip = document.createElement('div');
  chip.className = 'player-chip';
  chip.id = `chip-${slugify(nickname)}`;
  chip.textContent = nickname;
  container.appendChild(chip);
}

function removePlayerChip(nickname) {
  const chip = document.getElementById(`chip-${slugify(nickname)}`);
  chip?.remove();
}

// ──────────────────────────────────────────────────────────────────────────────
// RESET
// ──────────────────────────────────────────────────────────────────────────────
function resetToJoin() {
  clearTimer();
  resetState();
  showScreen('screen-join');
}

function resetState() {
  myNickname = '';
  myRoomCode = '';
  currentScore = 0;
  document.getElementById('player-list-container').innerHTML = '';
  document.getElementById('input-room-code').value = '';
  document.getElementById('input-nickname').value = '';
}

// ──────────────────────────────────────────────────────────────────────────────
// UTILITIES
// ──────────────────────────────────────────────────────────────────────────────
function showError(msg) {
  const el = document.getElementById('join-error');
  el.textContent = msg;
  el.classList.remove('hidden');
}
function hideError() {
  document.getElementById('join-error').classList.add('hidden');
}

function escHtml(str) {
  return String(str)
    .replace(/&/g,'&amp;')
    .replace(/</g,'&lt;')
    .replace(/>/g,'&gt;')
    .replace(/"/g,'&quot;');
}

function slugify(str) {
  return str.replace(/[^a-zA-Z0-9]/g, '_');
}

// ──────────────────────────────────────────────────────────────────────────────
// KEYBOARD SHORTCUTS
// ──────────────────────────────────────────────────────────────────────────────
document.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') {
    const join = document.getElementById('screen-join');
    if (join.classList.contains('active')) joinRoom();
  }
});

// Auto-format room code input: insert dash after 3 chars
document.getElementById('input-room-code').addEventListener('input', function() {
  let v = this.value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 6);
  if (v.length > 3) v = v.slice(0, 3) + '-' + v.slice(3);
  this.value = v;
});

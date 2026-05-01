/**
 * Quiz Game Show — Host Panel JavaScript
 * Handles quiz CRUD via REST API and game control via SignalR.
 */

'use strict';

// ──────────────────────────────────────────────────────────────────────────────
// STATE
// ──────────────────────────────────────────────────────────────────────────────
const API = '';  // Same-origin; empty prefix = relative URL

let hostConnection = null;
let activeRoomCode = '';
let currentQuestionTotal = 0;
let hostTimerInterval = null;
let hostTimerSeconds  = 0;
let resultsShown = false;
let currentHostQuestion = null; // stores full question data (with isCorrect) until results phase

// ──────────────────────────────────────────────────────────────────────────────
// NAVIGATION
// ──────────────────────────────────────────────────────────────────────────────
function showPage(name) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
  document.getElementById(`page-${name}`).classList.add('active');
  document.getElementById(`nav-${name}`)?.classList.add('active');

  if (name === 'quizzes') loadQuizzes();
  if (name === 'game')    loadQuizzesIntoSelect();
  if (name === 'create')  ensureOneQuestion();
}

// ──────────────────────────────────────────────────────────────────────────────
// QUIZ LIST
// ──────────────────────────────────────────────────────────────────────────────
async function loadQuizzes() {
  const grid = document.getElementById('quizzes-grid');
  grid.innerHTML = '<div class="loading-spinner">Завантаження...</div>';
  try {
    const res  = await fetch(`${API}/api/quizzes`);
    const list = await res.json();
    grid.innerHTML = '';

    if (!list.length) {
      grid.innerHTML = '<p style="color:var(--clr-muted);padding:2rem">Тестів ще немає. Створіть перший! ➕</p>';
      return;
    }

    list.forEach(q => {
      const card = document.createElement('div');
      card.className = 'quiz-card';
      card.innerHTML = `
        <div class="quiz-card-title">${escHtml(q.title)}</div>
        <div class="quiz-card-desc">${escHtml(q.description || '—')}</div>
        <div class="quiz-card-meta">
          <span>📝 ${q.questionCount} питань</span>
          <span>📅 ${new Date(q.createdAt).toLocaleDateString('uk-UA')}</span>
        </div>
        <div class="quiz-card-actions">
          <button class="btn btn-primary" onclick="launchGame(${q.id})">🚀 Запустити</button>
          <button class="btn btn-ghost" onclick="deleteQuiz(${q.id}, this)">🗑️</button>
        </div>
      `;
      grid.appendChild(card);
    });
  } catch (err) {
    grid.innerHTML = `<p class="error-msg">Помилка завантаження: ${err.message}</p>`;
  }
}

async function deleteQuiz(id, btn) {
  if (!confirm('Видалити цей тест?')) return;
  btn.disabled = true;
  try {
    await fetch(`${API}/api/quizzes/${id}`, { method: 'DELETE' });
    loadQuizzes();
  } catch (err) {
    alert('Помилка видалення: ' + err.message);
    btn.disabled = false;
  }
}

// ──────────────────────────────────────────────────────────────────────────────
// QUIZ CREATION
// ──────────────────────────────────────────────────────────────────────────────
let questionCount = 0;
const ANS_COLORS = ['#e63946','#2196f3','#f9c74f','#06d6a0'];

function ensureOneQuestion() {
  if (questionCount === 0) addQuestion();
}

function addQuestion() {
  questionCount++;
  const qIdx = questionCount;
  const container = document.getElementById('questions-container');

  const block = document.createElement('div');
  block.className = 'question-block';
  block.id = `q-block-${qIdx}`;

  block.innerHTML = `
    <div class="question-block-header">
      <span class="question-block-title">Питання ${qIdx}</span>
      <button type="button" class="btn-remove-question" onclick="removeQuestion(${qIdx})">✕</button>
    </div>
    <div class="form-group">
      <label>Текст питання *</label>
      <input type="text" name="q-text-${qIdx}" placeholder="Введіть питання..." required maxlength="500" />
    </div>
    <div class="answers-editor-header">
      <span class="answers-editor-label">Варіанти відповідей</span>
      <span class="answers-editor-hint">◉ — позначте правильну</span>
    </div>
    <div class="answers-editor" id="ans-editor-${qIdx}">
      ${[0,1,2,3].map(i => `
        <div class="answer-editor-item" id="ans-item-${qIdx}-${i}">
          <span class="answer-color-dot" style="background:${ANS_COLORS[i]}"></span>
          <input type="text" placeholder="Відповідь ${i+1}" name="ans-text-${qIdx}-${i}" maxlength="300" />
          <input type="radio" name="correct-${qIdx}" value="${i}" class="correct-radio"
            onchange="markCorrect(${qIdx}, ${i})" title="Позначити як правильну відповідь" />
        </div>
      `).join('')}
    </div>
    <div class="question-meta">
      <div class="form-group">
        <label>Час (сек)</label>
        <select name="q-time-${qIdx}">
          <option value="10">10 сек</option>
          <option value="15">15 сек</option>
          <option value="20" selected>20 сек</option>
          <option value="30">30 сек</option>
          <option value="60">60 сек</option>
        </select>
      </div>
      <div class="form-group">
        <label>Макс. балів</label>
        <select name="q-points-${qIdx}">
          <option value="500">500</option>
          <option value="1000" selected>1000</option>
          <option value="2000">2000</option>
        </select>
      </div>
    </div>
  `;

  container.appendChild(block);
}

function removeQuestion(idx) {
  const block = document.getElementById(`q-block-${idx}`);
  block?.remove();
}

function markCorrect(qIdx, ansIdx) {
  for (let i = 0; i < 4; i++) {
    const item = document.getElementById(`ans-item-${qIdx}-${i}`);
    if (item) item.classList.toggle('is-correct', i === ansIdx);
  }
}

async function submitQuiz(event) {
  event.preventDefault();
  const errEl = document.getElementById('create-error');
  const sucEl = document.getElementById('create-success');
  const btn   = document.getElementById('submit-quiz-btn');
  errEl.classList.add('hidden');
  sucEl.classList.add('hidden');

  const title = document.getElementById('quiz-title').value.trim();
  const desc  = document.getElementById('quiz-desc').value.trim();

  const blocks = document.querySelectorAll('.question-block');
  if (!blocks.length) {
    showFormError('Додайте хоча б одне питання.'); return;
  }

  const questions = [];
  for (const block of blocks) {
    const qIdx   = block.id.replace('q-block-', '');
    const text   = block.querySelector(`[name="q-text-${qIdx}"]`).value.trim();
    const time   = parseInt(block.querySelector(`[name="q-time-${qIdx}"]`).value);
    const points = parseInt(block.querySelector(`[name="q-points-${qIdx}"]`).value);

    if (!text) { showFormError(`Питання "${text || '#' + qIdx}" порожнє.`); return; }

    const answers = [];
    for (let i = 0; i < 4; i++) {
      const ansText = block.querySelector(`[name="ans-text-${qIdx}-${i}"]`)?.value.trim();
      if (ansText) {
        const radio = block.querySelector(`input[name="correct-${qIdx}"][value="${i}"]`);
        answers.push({ text: ansText, isCorrect: radio?.checked || false });
      }
    }

    if (answers.length < 2) { showFormError(`Питання "${text}": потрібно щонайменше 2 відповіді.`); return; }
    if (!answers.some(a => a.isCorrect)) { showFormError(`Питання "${text}": вкажіть правильну відповідь.`); return; }

    questions.push({ text, timeLimit: time, maxPoints: points, answers });
  }

  btn.disabled = true;
  btn.textContent = '💾 Збереження...';

  try {
    const res = await fetch(`${API}/api/quizzes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, description: desc, questions })
    });

    if (!res.ok) {
      const msg = await res.text();
      throw new Error(msg || `HTTP ${res.status}`);
    }

    sucEl.textContent = '✅ Тест успішно збережено!';
    sucEl.classList.remove('hidden');
    document.getElementById('create-quiz-form').reset();
    document.getElementById('questions-container').innerHTML = '';
    questionCount = 0;
    addQuestion();
    setTimeout(() => showPage('quizzes'), 1500);
  } catch (err) {
    showFormError('Помилка: ' + err.message);
  } finally {
    btn.disabled = false;
    btn.textContent = '💾 Зберегти тест';
  }
}

function showFormError(msg) {
  const el = document.getElementById('create-error');
  el.textContent = msg;
  el.classList.remove('hidden');
}

// ──────────────────────────────────────────────────────────────────────────────
// GAME MANAGEMENT
// ──────────────────────────────────────────────────────────────────────────────
async function loadQuizzesIntoSelect() {
  const sel = document.getElementById('select-quiz');
  sel.innerHTML = '<option value="">Завантаження...</option>';
  try {
    const res  = await fetch(`${API}/api/quizzes`);
    const list = await res.json();
    sel.innerHTML = list.length
      ? list.map(q => `<option value="${q.id}">${escHtml(q.title)} (${q.questionCount} питань)</option>`).join('')
      : '<option value="">Тестів немає — спочатку створіть тест</option>';
  } catch (err) {
    sel.innerHTML = '<option value="">Помилка завантаження</option>';
  }
}

async function createRoom() {
  const quizId = parseInt(document.getElementById('select-quiz').value);
  const errEl  = document.getElementById('room-error');
  errEl.classList.add('hidden');

  if (!quizId) { errEl.textContent = 'Оберіть тест.'; errEl.classList.remove('hidden'); return; }

  try {
    const res = await fetch(`${API}/api/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ quizId })
    });

    if (!res.ok) throw new Error(await res.text());
    const session = await res.json();

    activeRoomCode = session.roomCode;
    document.getElementById('host-room-code').textContent = session.roomCode;
    document.getElementById('ctrl-room-code').textContent = session.roomCode;
    document.getElementById('game-setup').classList.add('hidden');
    document.getElementById('game-lobby').classList.remove('hidden');

    await connectHostHub(session.roomCode);
  } catch (err) {
    errEl.textContent = 'Помилка: ' + err.message;
    errEl.classList.remove('hidden');
  }
}

async function connectHostHub(roomCode) {
  if (hostConnection) {
    try { await hostConnection.stop(); } catch (_) {}
  }

  hostConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/quiz')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // ── Server → Host events ────────────────────────────────────────────────────

  hostConnection.on('HostJoined', (data) => {
    document.getElementById('player-count').textContent = data.playerCount;
    renderHostPlayerList(data.players || []);
  });

  hostConnection.on('UpdatePlayerList', (players) => {
    const count = players.length;
    document.getElementById('player-count').textContent = count;
    renderHostPlayerList(players);
  });

  hostConnection.on('PlayerJoined', (nickname) => {
    addHostPlayerChip(nickname);
    const span = document.getElementById('player-count');
    span.textContent = parseInt(span.textContent || '0') + 1;
  });

  hostConnection.on('PlayerLeft', (nickname) => {
    document.getElementById(`hchip-${slugify(nickname)}`)?.remove();
  });

  hostConnection.on('ShowQuestionHost', (data) => {
    clearHostTimer();
    resultsShown = false;
    currentHostQuestion = data.hostQuestion; // save for later reveal
    document.getElementById('btn-next-q').disabled = true;
    renderHostQuestion(data);
    document.getElementById('game-lobby').classList.add('hidden');
    document.getElementById('game-control').classList.remove('hidden');
    document.getElementById('answer-stats').classList.add('hidden');
    startHostTimer(data.hostQuestion.timeLimit);
  });

  hostConnection.on('PlayerAnswered', (data) => {
    document.getElementById('ctrl-answered').textContent = data.totalAnswered;
    document.getElementById('ctrl-total-players').textContent = data.totalPlayers;
  });

  hostConnection.on('ShowResults', (data) => {
    clearHostTimer();
    resultsShown = true;
    highlightCorrectAnswer(); // now reveal the correct answer to host
    renderAnswerStats(data);
    document.getElementById('answer-stats').classList.remove('hidden');
    document.getElementById('btn-next-q').disabled = false;
  });

  hostConnection.on('GameFinished', (data) => {
    clearHostTimer();
    renderHostGameOver(data.leaderboard);
    document.getElementById('game-control').classList.add('hidden');
    document.getElementById('game-finished').classList.remove('hidden');
  });

  hostConnection.on('Error', (msg) => {
    alert('Помилка: ' + msg);
  });

  await hostConnection.start();
  await hostConnection.invoke('HostJoinRoom', roomCode);
}

// ──────────────────────────────────────────────────────────────────────────────
// HOST GAME ACTIONS
// ──────────────────────────────────────────────────────────────────────────────
async function hostStartGame() {
  if (!hostConnection || !activeRoomCode) return;
  try {
    await hostConnection.invoke('StartGame', activeRoomCode);
  } catch (err) {
    alert('Помилка: ' + err.message);
  }
}

async function hostShowResults() {
  if (!hostConnection || !activeRoomCode) return;
  try {
    await hostConnection.invoke('ShowResults', activeRoomCode);
  } catch (err) {
    alert('Помилка: ' + err.message);
  }
}

async function hostNextQuestion() {
  if (!hostConnection || !activeRoomCode) return;
  if (!resultsShown) {
    if (!confirm('Результати ще не показано. Перейти до наступного питання?')) return;
  }
  try {
    document.getElementById('btn-next-q').disabled = true;
    await hostConnection.invoke('NextQuestion', activeRoomCode);
  } catch (err) {
    alert('Помилка: ' + err.message);
  }
}

async function closeRoom() {
  if (!confirm('Закрити кімнату та завершити гру?')) return;
  try {
    await fetch(`${API}/api/sessions/${activeRoomCode}`, { method: 'DELETE' });
    resetGame();
  } catch (err) {
    alert('Помилка: ' + err.message);
  }
}

// ──────────────────────────────────────────────────────────────────────────────
// HOST RENDERING
// ──────────────────────────────────────────────────────────────────────────────
function renderHostPlayerList(players) {
  const container = document.getElementById('host-player-list');
  container.innerHTML = '';
  players.forEach(p => addHostPlayerChip(p.nickname));
}

function addHostPlayerChip(nickname) {
  const container = document.getElementById('host-player-list');
  if (document.getElementById(`hchip-${slugify(nickname)}`)) return;
  const chip = document.createElement('div');
  chip.className = 'host-player-chip';
  chip.id = `hchip-${slugify(nickname)}`;
  chip.textContent = nickname;
  container.appendChild(chip);
}

function renderHostQuestion(data) {
  const { hostQuestion, questionIndex, totalQuestions } = data;
  currentQuestionTotal = totalQuestions;

  document.getElementById('ctrl-q-num').textContent = questionIndex + 1;
  document.getElementById('ctrl-q-total').textContent = totalQuestions;
  document.getElementById('ctrl-q-text').textContent  = hostQuestion.text;
  document.getElementById('ctrl-answered').textContent = '0';

  // Count players
  fetch(`${API}/api/sessions/${activeRoomCode}/leaderboard`)
    .then(r => r.json())
    .then(lb => { document.getElementById('ctrl-total-players').textContent = lb.length; })
    .catch(() => {});

  const grid = document.getElementById('ctrl-answers');
  grid.innerHTML = '';
  // Render ALL answers identically — correct answer is revealed only after ShowResults
  hostQuestion.answers.forEach((a, i) => {
    const pill = document.createElement('div');
    pill.className = 'ctrl-answer-pill';
    pill.dataset.index = i;
    pill.dataset.answerId = a.id;
    pill.textContent = a.text;
    grid.appendChild(pill);
  });
}

/**
 * Called after ShowResults — highlights the correct answer pill
 * and adds the ✓ badge. Uses the saved currentHostQuestion data.
 */
function highlightCorrectAnswer() {
  if (!currentHostQuestion) return;
  const grid = document.getElementById('ctrl-answers');
  currentHostQuestion.answers.forEach((a, i) => {
    const pill = grid.querySelector(`.ctrl-answer-pill[data-answer-id="${a.id}"]`);
    if (!pill) return;
    if (a.isCorrect) {
      pill.classList.add('correct');
      const badge = document.createElement('span');
      badge.className = 'correct-badge';
      badge.textContent = '✓';
      pill.appendChild(badge);
    }
  });
}

function renderAnswerStats(data) {
  const statsContainer = document.getElementById('stats-bars');
  statsContainer.innerHTML = '';

  const maxCount = Math.max(...data.answerStats.map(s => s.count), 1);
  const colors   = ['#e63946','#2196f3','#f9c74f','#06d6a0'];

  data.answerStats.forEach((stat, i) => {
    const pct = Math.round((stat.count / maxCount) * 100);
    const bar = document.createElement('div');
    bar.className = 'stat-bar-item';
    bar.innerHTML = `
      <div class="stat-bar-label">${escHtml(stat.text)}${stat.isCorrect ? ' ✓' : ''}</div>
      <div class="stat-bar-track">
        <div class="stat-bar-fill" style="width:${pct}%;background:${colors[i % 4]}"></div>
      </div>
      <div class="stat-bar-count">${stat.count}</div>
    `;
    statsContainer.appendChild(bar);
  });

  // Mini leaderboard
  const lbEl = document.getElementById('host-leaderboard');
  lbEl.innerHTML = '';
  const medals = ['🥇','🥈','🥉'];
  (data.leaderboard || []).slice(0, 5).forEach((p, i) => {
    const item = document.createElement('div');
    item.className = 'mini-lb-item';
    item.innerHTML = `
      <span class="mini-lb-rank">${medals[i] ?? p.rank}</span>
      <span class="mini-lb-name">${escHtml(p.nickname)}</span>
      <span class="mini-lb-score">${p.totalScore}</span>
    `;
    lbEl.appendChild(item);
  });
}

function renderHostGameOver(leaderboard) {
  const el = document.getElementById('host-final-leaderboard');
  el.innerHTML = '';
  const medals = ['🥇','🥈','🥉'];
  (leaderboard || []).forEach((p, i) => {
    const item = document.createElement('div');
    item.className = 'leaderboard-item';
    item.innerHTML = `
      <span class="lb-rank">${medals[i] ?? p.rank}</span>
      <span class="lb-name">${escHtml(p.nickname)}</span>
      <span class="lb-score">${p.totalScore} балів</span>
    `;
    el.appendChild(item);
  });
}

// ──────────────────────────────────────────────────────────────────────────────
// HOST TIMER (display only, scoring is server-side)
// ──────────────────────────────────────────────────────────────────────────────
function startHostTimer(seconds) {
  hostTimerSeconds = seconds;
  document.getElementById('ctrl-timer-val').textContent = seconds;

  hostTimerInterval = setInterval(() => {
    hostTimerSeconds--;
    document.getElementById('ctrl-timer-val').textContent = Math.max(0, hostTimerSeconds);
    if (hostTimerSeconds <= 0) clearHostTimer();
  }, 1000);
}

function clearHostTimer() {
  if (hostTimerInterval) { clearInterval(hostTimerInterval); hostTimerInterval = null; }
}

// ──────────────────────────────────────────────────────────────────────────────
// LAUNCH GAME FROM QUIZ LIST
// ──────────────────────────────────────────────────────────────────────────────
async function launchGame(quizId) {
  showPage('game');
  // Pre-select the quiz in the dropdown
  await loadQuizzesIntoSelect();
  document.getElementById('select-quiz').value = quizId;
}

// ──────────────────────────────────────────────────────────────────────────────
// RESET GAME
// ──────────────────────────────────────────────────────────────────────────────
function resetGame() {
  clearHostTimer();
  activeRoomCode = '';
  currentQuestionTotal = 0;
  resultsShown = false;

  document.getElementById('game-setup').classList.remove('hidden');
  document.getElementById('game-lobby').classList.add('hidden');
  document.getElementById('game-control').classList.add('hidden');
  document.getElementById('game-finished').classList.add('hidden');
  document.getElementById('host-player-list').innerHTML = '';
  document.getElementById('player-count').textContent = '0';
  document.getElementById('stats-bars').innerHTML = '';
  document.getElementById('host-leaderboard').innerHTML = '';
}

// ──────────────────────────────────────────────────────────────────────────────
// UTILITIES
// ──────────────────────────────────────────────────────────────────────────────
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
// INIT
// ──────────────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {
  loadQuizzes();
  ensureOneQuestion();

  // If opened via ?code=XXX (from player's "Create Room" flow),
  // auto-navigate to game management and connect to that room.
  const params   = new URLSearchParams(window.location.search);
  const codeParam = params.get('code');
  if (codeParam) {
    showPage('game');
    await loadQuizzesIntoSelect();
    activeRoomCode = codeParam.toUpperCase();

    document.getElementById('host-room-code').textContent  = activeRoomCode;
    document.getElementById('ctrl-room-code').textContent  = activeRoomCode;
    document.getElementById('game-setup').classList.add('hidden');
    document.getElementById('game-lobby').classList.remove('hidden');

    await connectHostHub(activeRoomCode);
  }
});

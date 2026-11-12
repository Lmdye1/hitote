'use strict';

const H = window.Hitote;
const $ = id => document.getElementById(id);
const editor = $('editor');
const surface = $('keyboard-surface');
const STORAGE_KEY = 'hitote-v1';
let pending = '';
let selectedRow = null;
let mode = 'pointer';
let custom = [];
let candidates = [];
let selection = { start: 0, end: 0 };
let noticeTimer;
let gesture = null;
let saveTimer;

function notice(message) {
  $('notice').textContent = message;
  $('notice').classList.add('visible');
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(() => $('notice').classList.remove('visible'), 2500);
}

function save() {
  clearTimeout(saveTimer);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ draft: editor.value, pending, custom }));
    $('save-status').textContent = 'このブラウザに保存済み';
  } catch {
    $('save-status').textContent = '保存できません・コピーで保管';
  }
}

function load() {
  try {
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null');
    if (!stored || typeof stored !== 'object') return;
    editor.value = typeof stored.draft === 'string' ? stored.draft : '';
    pending = typeof stored.pending === 'string' ? stored.pending : '';
    custom = Array.isArray(stored.custom) ? stored.custom.filter(entry => entry && typeof entry.reading === 'string' && /^[ぁ-ゖー]+$/.test(entry.reading) && typeof entry.text === 'string' && entry.text.length > 0).slice(0, 200) : [];
    selection = { start: editor.value.length, end: editor.value.length };
    editor.setSelectionRange(selection.start, selection.end);
    $('save-status').textContent = '保存した下書きを復元しました';
  } catch {
    $('save-status').textContent = '保存データを読み込めません';
  }
}

function rememberSelection() {
  selection = { start: editor.selectionStart, end: editor.selectionEnd };
}

function updateCount() {
  $('char-count').textContent = `${Array.from(editor.value).length} 文字`;
}

function applyEdit(result) {
  editor.value = result.text;
  selection = { start: result.caret, end: result.caret };
  editor.setSelectionRange(result.caret, result.caret);
  updateCount();
  save();
}

function insertText(text) {
  applyEdit(H.replaceSelection(editor.value, selection.start, selection.end, text));
}

function commit(text = pending) {
  if (!text) return;
  pending = '';
  selectedRow = null;
  insertText(text);
  renderComposition();
  renderRow();
}

function addKana(char) {
  pending += char;
  selectedRow = null;
  renderComposition();
  renderRow();
  save();
}

function renderComposition() {
  const composition = $('composition');
  composition.replaceChildren();
  composition.classList.toggle('has-text', Boolean(pending));
  if (pending) composition.textContent = pending;
  else {
    const hint = document.createElement('span');
    hint.className = 'muted';
    hint.textContent = 'かなを選ぶと、ここに表示されます';
    composition.append(hint);
  }
  $('commit').disabled = !pending;
  candidates = H.suggestions(pending, custom);
  $('suggestions').replaceChildren();
  candidates.forEach((entry, index) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'suggestion';
    const key = document.createElement('kbd');
    key.textContent = index + 1;
    const text = document.createElement('span');
    text.textContent = entry.text;
    button.append(key, text);
    button.title = `よみ：${entry.reading}`;
    button.addEventListener('click', () => {
      commit(entry.text);
      focusPanel();
    });
    $('suggestions').append(button);
  });
  if (!candidates.length) {
    const hint = document.createElement('p');
    hint.className = 'fine-print';
    hint.textContent = '一致する登録フレーズがありません。「かなで確定」でそのまま入力できます。';
    $('suggestions').append(hint);
  }
}

function chooseRow(row) {
  // A second tap on the same row enters its center character.
  if (selectedRow === row) addKana(row.chars[0]);
  else {
    selectedRow = row;
    renderRow();
  }
}

function renderRow() {
  document.querySelectorAll('.kana-key').forEach(button => {
    const selected = button.dataset.key === selectedRow?.key;
    button.classList.toggle('selected', selected);
    button.setAttribute('aria-pressed', String(selected));
  });
  $('vowel-label').textContent = selectedRow ? `${selectedRow.label}行 → 文字を選んでください` : '行を選んでください';
  $('cancel-row').disabled = !selectedRow;
  $('vowels').replaceChildren();
  (selectedRow?.chars || ['あ', 'い', 'う', 'え', 'お']).forEach((char, index) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'vowel';
    button.disabled = !selectedRow;
    const label = document.createElement('span');
    label.textContent = char;
    const key = document.createElement('kbd');
    key.textContent = H.vowelKeys[index].toUpperCase();
    button.append(label, key);
    button.addEventListener('click', () => { addKana(char); focusPanel(); });
    $('vowels').append(button);
  });
}

function buildKeypad() {
  H.rows.forEach((row, index) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `kana-key${index === 9 ? ' wide' : ''}`;
    button.dataset.key = row.key;
    button.setAttribute('aria-label', `${row.label}行：${row.chars.join('、')}`);
    const key = document.createElement('kbd');
    key.textContent = row.key.toUpperCase();
    const title = document.createElement('strong');
    title.textContent = row.label;
    const subtitle = document.createElement('small');
    subtitle.textContent = row.chars.slice(1).join(' ');
    button.append(key, title, subtitle);
    let suppressClick = false;
    button.addEventListener('pointerdown', event => {
      if (!event.isPrimary || event.button !== 0) return;
      event.preventDefault();
      suppressClick = false;
      gesture = { id: event.pointerId, x: event.clientX, y: event.clientY, row, button };
      button.setPointerCapture(event.pointerId);
      focusPanel();
    });
    button.addEventListener('pointermove', event => {
      if (!gesture || gesture.id !== event.pointerId) return;
      const index = H.flickIndex(event.clientX - gesture.x, event.clientY - gesture.y);
      $('gesture-preview').classList.toggle('visible', index !== -1);
      $('gesture-preview').textContent = index === -1 ? '' : row.chars[index];
    });
    button.addEventListener('pointerup', event => {
      if (!gesture || gesture.id !== event.pointerId) return;
      const index = H.flickIndex(event.clientX - gesture.x, event.clientY - gesture.y);
      gesture = null;
      suppressClick = true;
      $('gesture-preview').classList.remove('visible');
      if (index === -1) chooseRow(row);
      else addKana(row.chars[index]);
    });
    const cancelGesture = () => { gesture = null; $('gesture-preview').classList.remove('visible'); };
    button.addEventListener('pointercancel', cancelGesture);
    button.addEventListener('lostpointercapture', cancelGesture);
    button.addEventListener('click', event => {
      if (suppressClick && event.detail !== 0) { suppressClick = false; return; }
      chooseRow(row);
      focusPanel();
    });
    $('kana-grid').append(button);
  });
}

function focusPanel() { surface.focus({ preventScroll: true }); }

function setMode(next) {
  mode = next;
  selectedRow = null;
  renderRow();
  document.body.classList.toggle('keyboard-mode', mode === 'keyboard');
  for (const name of ['pointer', 'keyboard']) {
    $(`${name}-mode`).classList.toggle('active', mode === name);
    $(`${name}-mode`).setAttribute('aria-pressed', String(mode === name));
  }
  $('mode-help').textContent = mode === 'keyboard'
    ? '① 行のキー → ② A S D F G で母音。例：W → S で「き」。'
    : 'タップで行を選択。フリックなら、そのままかなを入力。';
  const tip = $('input-tip').querySelector('p');
  tip.textContent = mode === 'keyboard'
    ? 'Space：先頭候補を確定 / 1〜5：候補を選ぶ / R・Backspace：1文字削除 / T：濁点 / B：小文字。パネルにフォーカスがあるときだけ有効です。英数入力に切り替えて使ってください。'
    : 'フリックの方向は 中央：あ / 左：い / 上：う / 右：え / 下：お。中央の文字は行を2回タップするか、下の候補から選べます。';
  focusPanel();
}

function action(name) {
  selectedRow = null;
  if (name === 'voice' || name === 'small') {
    if (pending) pending = H[name](pending);
    else notice('入力中のかなの末尾を変更します');
  } else if (name === 'delete') {
    if (pending) pending = H.deleteBackward(pending, pending.length, pending.length).text;
    else applyEdit(H.deleteBackward(editor.value, selection.start, selection.end));
  } else {
    const chars = { comma: '、', period: '。', newline: '\n' };
    if (chars[name]) commit(pending + chars[name]);
  }
  renderComposition();
  renderRow();
  save();
  focusPanel();
}

surface.addEventListener('keydown', event => {
  if (event.ctrlKey || event.altKey || event.metaKey) return;
  if (event.isComposing || event.keyCode === 229) {
    if (mode === 'keyboard') notice('キーボードモードでは、IMEを英数入力に切り替えてください');
    return;
  }
  const key = event.key.toLowerCase();
  if (key === 'backspace') {
    event.preventDefault();
    action('delete');
    return;
  }
  if (mode !== 'keyboard') return;
  const codeKey = /^Key[A-Z]$/.test(event.code) ? event.code.slice(3).toLowerCase() : key;
  // Keep Tab and focused-button activation available for keyboard navigation.
  if (event.target !== surface && (key === ' ' || key === 'enter')) return;
  let handled = true;
  if (key === 'escape') { selectedRow = null; renderRow(); }
  else if (codeKey === 'r') action('delete');
  else if (selectedRow && H.vowelKeys.includes(codeKey)) addKana(selectedRow.chars[H.vowelKeys.indexOf(codeKey)]);
  else if (key === ' ') {
    if (pending) commit(candidates[0]?.text || pending);
  } else if (key === 'enter') commit();
  else if (/^[1-5]$/.test(key)) {
    const candidate = candidates[Number(key) - 1];
    if (candidate) commit(candidate.text);
  } else if (H.rows.some(row => row.key === codeKey)) {
    selectedRow = H.rows.find(row => row.key === codeKey);
    renderRow();
  } else if ({ t: 'voice', b: 'small', g: 'comma', h: 'period', f: 'newline' }[codeKey]) {
    action({ t: 'voice', b: 'small', g: 'comma', h: 'period', f: 'newline' }[codeKey]);
  } else handled = false;
  if (handled) event.preventDefault();
});

['select', 'click', 'keyup', 'blur'].forEach(name => editor.addEventListener(name, rememberSelection));
editor.addEventListener('input', () => {
  rememberSelection();
  updateCount();
  $('save-status').textContent = '保存中…';
  clearTimeout(saveTimer);
  saveTimer = setTimeout(save, 200);
});
window.addEventListener('pagehide', save);
$('pointer-mode').addEventListener('click', () => setMode('pointer'));
$('keyboard-mode').addEventListener('click', () => setMode('keyboard'));
$('cancel-row').addEventListener('click', () => { selectedRow = null; renderRow(); focusPanel(); });
$('commit').addEventListener('click', () => { commit(); focusPanel(); });
document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => action(button.dataset.action)));

$('copy').addEventListener('click', async () => {
  if (pending) commit();
  if (!editor.value) { notice('文章を入力してからコピーしてください'); return; }
  try {
    await navigator.clipboard.writeText(editor.value);
    notice('文章をコピーしました');
  } catch {
    const old = { ...selection };
    editor.focus();
    editor.select();
    let copied = false;
    try { copied = document.execCommand('copy'); } catch { /* Manual fallback below. */ }
    if (copied) {
      editor.setSelectionRange(old.start, old.end);
      selection = old;
      notice('文章をコピーしました');
    } else notice('文章を選択しました。Ctrl+Cでコピーしてください');
  }
});

function renderCustom() {
  $('custom-phrases').replaceChildren();
  custom.forEach((entry, index) => {
    const item = document.createElement('li');
    const text = document.createElement('span');
    text.textContent = `${entry.reading} → ${entry.text}`;
    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'text-button';
    remove.textContent = '削除';
    remove.setAttribute('aria-label', `${entry.reading}の登録を削除`);
    remove.addEventListener('click', () => {
      custom.splice(index, 1);
      save();
      renderCustom();
      renderComposition();
    });
    item.append(text, remove);
    $('custom-phrases').append(item);
  });
}

$('phrase-form').addEventListener('submit', event => {
  event.preventDefault();
  const reading = H.normalizeReading($('phrase-reading').value);
  const text = $('phrase-text').value.trim();
  if (!/^[ぁ-ゖー]+$/.test(reading) || !text) {
    $('form-message').textContent = 'よみは、ひらがな・カタカナ・長音で入力してください。文章も必要です。';
    return;
  }
  if (custom.length >= 200) { $('form-message').textContent = '登録上限は200件です。使わない登録を削除してください。'; return; }
  custom = custom.filter(entry => !(entry.reading === reading && entry.text === text));
  custom.unshift({ reading, text });
  save();
  renderCustom();
  renderComposition();
  $('phrase-form').reset();
  $('form-message').textContent = `「${reading}」を登録しました。`;
});

load();
buildKeypad();
renderRow();
renderComposition();
renderCustom();
updateCount();

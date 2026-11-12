'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const H = require('./engine.js');

test('すべての行を左手の行キーと母音キーで選べる', () => {
  assert.equal(new Set(H.rows.map(row => row.key)).size, 10);
  for (const row of H.rows) assert.equal(row.chars.length, H.vowelKeys.length);
  assert.equal(H.rows.find(row => row.key === 'w').chars[H.vowelKeys.indexOf('s')], 'き');
  assert.equal(H.rows.find(row => row.key === 'v').chars[H.vowelKeys.indexOf('d')], 'ん');
});

test('濁点・半濁点を循環し、対象外の文字は保つ', () => {
  assert.equal(H.voice('こんにちは'), 'こんにちば');
  assert.equal(H.voice('ば'), 'ぱ');
  assert.equal(H.voice('ぱ'), 'は');
  assert.equal(H.voice('が'), 'か');
  assert.equal(H.voice('う'), 'ゔ');
  assert.equal(H.voice('ん'), 'ん');
  assert.equal(H.voice(''), '');
});

test('拗音・促音を往復できる', () => {
  assert.equal(H.small('きや'), 'きゃ');
  assert.equal(H.small('きゃ'), 'きや');
  assert.equal(H.small('まつ'), 'まっ');
});

test('かな入力を文章途中へ挿入し、選択部分だけを置換する', () => {
  assert.deepEqual(H.replaceSelection('今日はです。', 3, 3, '晴れ'), { text: '今日は晴れです。', caret: 5 });
  assert.deepEqual(H.replaceSelection('今日は雨です。', 3, 4, '晴れ'), { text: '今日は晴れです。', caret: 5 });
});

test('削除は絵文字や結合文字を分断せず、前方の文字を残す', () => {
  assert.deepEqual(H.deleteBackward('あ👨‍👩‍👧‍👦い', 12, 12), { text: 'あい', caret: 1 });
  assert.deepEqual(H.deleteBackward('か\u3099', 2, 2), { text: '', caret: 0 });
  assert.deepEqual(H.deleteBackward('あいう', 1, 2), { text: 'あう', caret: 1 });
  assert.deepEqual(H.deleteBackward('あ', 0, 0), { text: 'あ', caret: 0 });
});

test('候補は前方一致、完全一致優先、ユーザー辞書を反映する', () => {
  const custom = [{ reading: 'おつかれ', text: 'お疲れさま！' }, { reading: 'おつ', text: 'お疲れさまです。' }];
  assert.equal(H.suggestions('おつ', custom)[0].text, 'お疲れさまです。');
  assert.equal(H.suggestions('おつかれ', custom)[0].text, 'お疲れさま！');
  assert.equal(H.suggestions('あり')[0].text, 'ありがとうございます。');
  assert.deepEqual(H.suggestions('みとうろくのことば'), []);
  assert.equal(H.suggestions('', custom).filter(entry => entry.text === 'お疲れさまです。').length, 1);
});

test('カタカナ・半角カナのよみを正規化する', () => {
  assert.equal(H.normalizeReading(' ｵﾂｶﾚ '), 'おつかれ');
  assert.equal(H.normalizeReading('メール'), 'めーる');
});

test('フリック方向とタップを識別する', () => {
  assert.equal(H.flickIndex(5, 5), -1);
  assert.equal(H.flickIndex(-40, 10), 1);
  assert.equal(H.flickIndex(5, -40), 2);
  assert.equal(H.flickIndex(40, 5), 3);
  assert.equal(H.flickIndex(5, 40), 4);
});

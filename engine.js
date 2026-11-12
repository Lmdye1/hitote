(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.Hitote = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const rows = [
    { key: 'q', label: 'あ', chars: ['あ', 'い', 'う', 'え', 'お'] },
    { key: 'w', label: 'か', chars: ['か', 'き', 'く', 'け', 'こ'] },
    { key: 'e', label: 'さ', chars: ['さ', 'し', 'す', 'せ', 'そ'] },
    { key: 'a', label: 'た', chars: ['た', 'ち', 'つ', 'て', 'と'] },
    { key: 's', label: 'な', chars: ['な', 'に', 'ぬ', 'ね', 'の'] },
    { key: 'd', label: 'は', chars: ['は', 'ひ', 'ふ', 'へ', 'ほ'] },
    { key: 'z', label: 'ま', chars: ['ま', 'み', 'む', 'め', 'も'] },
    { key: 'x', label: 'や', chars: ['や', '（', 'ゆ', '）', 'よ'] },
    { key: 'c', label: 'ら', chars: ['ら', 'り', 'る', 'れ', 'ろ'] },
    { key: 'v', label: 'わ', chars: ['わ', 'を', 'ん', 'ー', '〜'] }
  ];
  const vowelKeys = ['a', 's', 'd', 'f', 'g'];
  const builtin = [
    ['ありがとう', 'ありがとうございます。'], ['おつかれ', 'お疲れさまです。'],
    ['よろしく', 'よろしくお願いします。'], ['りょうかい', '了解しました。'],
    ['おねがい', 'お願いします。'], ['ありがとう', 'ありがとう！'],
    ['おはよう', 'おはようございます。'], ['こんにちは', 'こんにちは。'],
    ['こんばんは', 'こんばんは。'], ['おやすみ', 'おやすみなさい。'],
    ['すみません', 'すみません。'], ['ごめん', 'ごめんなさい。'],
    ['だいじょうぶ', '大丈夫です。'], ['わかりました', 'わかりました。'],
    ['しょうち', '承知しました。'], ['かくにん', '確認します。'],
    ['かくにん', 'ご確認をお願いします。'], ['しつもん', '質問があります。'],
    ['おせわ', 'お世話になっております。'], ['しつれい', '失礼いたします。'],
    ['おひさしぶり', 'お久しぶりです。'], ['また', 'また連絡します。'],
    ['れんらく', '連絡ありがとうございます。'], ['すこし', '少しお待ちください。'],
    ['おくれます', '少し遅れます。'], ['いま', '今から向かいます。'],
    ['つきました', '着きました。'], ['たすかります', '助かります。'],
    ['おめでとう', 'おめでとうございます！'], ['いいですね', 'いいですね！'],
    ['きょう', '今日'], ['あした', '明日'], ['きのう', '昨日'],
    ['らいしゅう', '来週'], ['こんしゅう', '今週'], ['じかん', '時間'],
    ['よてい', '予定'], ['しごと', '仕事'], ['かいぎ', '会議'],
    ['でんわ', '電話'], ['めーる', 'メール'], ['にほんご', '日本語'],
    ['にゅうりょく', '入力'], ['かたて', '片手'], ['ぶんしょう', '文章'],
    ['べんり', '便利'], ['はやい', '速い'], ['わたし', '私'],
    ['じぶん', '自分'], ['ともだち', '友達'], ['かぞく', '家族'],
    ['えき', '駅'], ['いえ', '家'], ['かいしゃ', '会社'],
    ['がっこう', '学校'], ['とうきょう', '東京'], ['おおさか', '大阪'],
    ['たのしい', '楽しい'], ['うれしい', '嬉しい'], ['おいしい', '美味しい']
  ].map(([reading, text]) => ({ reading, text }));

  function normalizeReading(text) {
    return text.normalize('NFKC').trim().replace(/[ァ-ヶ]/g, char => String.fromCharCode(char.charCodeAt(0) - 0x60));
  }

  function suggestions(reading, custom = [], limit = 5) {
    const query = normalizeReading(reading);
    const entries = [...custom, ...builtin];
    const matches = query ? entries.filter(entry => entry.reading.startsWith(query)) : entries;
    const ranked = query ? [...matches.filter(entry => entry.reading === query), ...matches.filter(entry => entry.reading !== query)] : matches;
    const seen = new Set();
    return ranked.filter(entry => {
      if (seen.has(entry.text)) return false;
      seen.add(entry.text);
      return true;
    }).slice(0, limit);
  }

  function cycleLast(text, groups) {
    const chars = Array.from(text);
    if (!chars.length) return text;
    const last = chars[chars.length - 1];
    const group = groups.find(group => group.includes(last));
    if (group) chars[chars.length - 1] = group[(group.indexOf(last) + 1) % group.length];
    return chars.join('');
  }

  function voice(text) {
    return cycleLast(text, ['かが', 'きぎ', 'くぐ', 'けげ', 'こご', 'さざ', 'しじ', 'すず', 'せぜ', 'そぞ', 'ただ', 'ちぢ', 'つづ', 'てで', 'とど', 'はばぱ', 'ひびぴ', 'ふぶぷ', 'へべぺ', 'ほぼぽ', 'うゔ']);
  }
  function small(text) { return cycleLast(text, ['あぁ', 'いぃ', 'うぅ', 'えぇ', 'おぉ', 'つっ', 'やゃ', 'ゆゅ', 'よょ', 'わゎ', 'かゕ', 'けゖ']); }

  function replaceSelection(text, start, end, insertion) {
    start = Math.max(0, Math.min(text.length, start));
    end = Math.max(start, Math.min(text.length, end));
    return { text: text.slice(0, start) + insertion + text.slice(end), caret: start + insertion.length };
  }

  function deleteBackward(text, start, end) {
    if (start !== end) return replaceSelection(text, start, end, '');
    if (start === 0) return { text, caret: 0 };
    const before = text.slice(0, start);
    let length;
    if (typeof Intl.Segmenter === 'function') {
      const segments = Array.from(new Intl.Segmenter('ja', { granularity: 'grapheme' }).segment(before));
      length = segments[segments.length - 1].segment.length;
    } else {
      length = Array.from(before).pop().length;
    }
    return replaceSelection(text, start - length, end, '');
  }

  function flickIndex(dx, dy, threshold = 22) {
    if (Math.hypot(dx, dy) < threshold) return -1;
    if (Math.abs(dx) > Math.abs(dy)) return dx < 0 ? 1 : 3;
    return dy < 0 ? 2 : 4;
  }

  return { rows, vowelKeys, builtin, normalizeReading, suggestions, voice, small, replaceSelection, deleteBackward, flickIndex };
});

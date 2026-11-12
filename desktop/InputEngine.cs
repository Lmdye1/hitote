using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Hitote
{
    internal sealed class Output
    {
        public string Text;
        public Keys Key;
        public static Output Roman(string text) { return new Output { Text = text }; }
        public static Output Press(Keys key) { return new Output { Key = key }; }
    }

    internal sealed class InputResult
    {
        public bool Handled;
        public readonly List<Output> Outputs = new List<Output>();
    }

    internal sealed class KanaRow
    {
        public Keys Key;
        public string Characters;
        public KanaRow(Keys key, string characters) { Key = key; Characters = characters; }
    }

    internal sealed class InputEngine
    {
        public static readonly KanaRow[] Rows = {
            new KanaRow(Keys.Q, "あいうえお"), new KanaRow(Keys.W, "かきくけこ"),
            new KanaRow(Keys.E, "さしすせそ"), new KanaRow(Keys.A, "たちつてと"),
            new KanaRow(Keys.S, "なにぬねの"), new KanaRow(Keys.D, "はひふへほ"),
            new KanaRow(Keys.Z, "まみむめも"), new KanaRow(Keys.X, "や（ゆ）よ"),
            new KanaRow(Keys.C, "らりるれろ"), new KanaRow(Keys.V, "わをんー〜")
        };
        public static readonly Keys[] Vowels = { Keys.A, Keys.S, Keys.D, Keys.F, Keys.G };
        private static readonly Dictionary<char, string> Roman = BuildRoman();
        private static readonly string[] Voiced = {
            "かが", "きぎ", "くぐ", "けげ", "こご", "さざ", "しじ", "すず", "せぜ", "そぞ",
            "ただ", "ちぢ", "つづ", "てで", "とど", "はばぱ", "ひびぴ", "ふぶぷ", "へべぺ", "ほぼぽ", "うゔ"
        };
        private static readonly string[] Small = { "あぁ", "いぃ", "うぅ", "えぇ", "おぉ", "つっ", "やゃ", "ゆゅ", "よょ", "わゎ" };
        public KanaRow SelectedRow { get; private set; }
        public char LastKana { get; private set; }

        private static Dictionary<char, string> BuildRoman()
        {
            var map = new Dictionary<char, string>();
            Add(map, "あいうえお", "a i u e o");
            Add(map, "かきくけこ", "ka ki ku ke ko");
            Add(map, "さしすせそ", "sa si su se so");
            Add(map, "たちつてと", "ta ti tu te to");
            Add(map, "なにぬねの", "na ni nu ne no");
            Add(map, "はひふへほ", "ha hi hu he ho");
            Add(map, "まみむめも", "ma mi mu me mo");
            Add(map, "や（ゆ）よ", "ya ( yu ) yo");
            Add(map, "らりるれろ", "ra ri ru re ro");
            // n' terminates ん unambiguously even before a vowel or や行.
            Add(map, "わをんー〜", "wa wo n' - ~");
            Add(map, "がぎぐげご", "ga gi gu ge go");
            Add(map, "ざじずぜぞ", "za zi zu ze zo");
            Add(map, "だぢづでど", "da di du de do");
            Add(map, "ばびぶべぼ", "ba bi bu be bo");
            Add(map, "ぱぴぷぺぽ", "pa pi pu pe po");
            Add(map, "ぁぃぅぇぉっゃゅょゎゔ", "xa xi xu xe xo xtu xya xyu xyo xwa vu");
            return map;
        }

        private static void Add(Dictionary<char, string> map, string characters, string values)
        {
            var parts = values.Split(' ');
            for (int i = 0; i < characters.Length; i++) map.Add(characters[i], parts[i]);
        }

        public void Reset() { SelectedRow = null; LastKana = '\0'; }

        public InputResult Process(Keys key)
        {
            var result = new InputResult { Handled = true };
            if (key == Keys.Back || key == Keys.R)
            {
                if (SelectedRow == null) result.Outputs.Add(Output.Press(Keys.Back));
                Reset();
                return result;
            }

            int vowel = Array.IndexOf(Vowels, key);
            if (SelectedRow != null && vowel >= 0)
            {
                LastKana = SelectedRow.Characters[vowel];
                SelectedRow = null;
                result.Outputs.Add(Output.Roman(Roman[LastKana]));
                return result;
            }

            if (key == Keys.T || key == Keys.B)
            {
                var groups = key == Keys.T ? Voiced : Small;
                foreach (string group in groups)
                {
                    int index = group.IndexOf(LastKana);
                    if (index < 0) continue;
                    LastKana = group[(index + 1) % group.Length];
                    result.Outputs.Add(Output.Press(Keys.Back));
                    result.Outputs.Add(Output.Roman(Roman[LastKana]));
                    break;
                }
                SelectedRow = null;
                return result;
            }

            foreach (KanaRow row in Rows)
            {
                if (row.Key != key) continue;
                SelectedRow = row;
                LastKana = '\0';
                return result;
            }

            bool rowWasSelected = SelectedRow != null;
            Reset();
            if (key == Keys.G) result.Outputs.Add(Output.Roman(","));
            else if (key == Keys.H) result.Outputs.Add(Output.Roman("."));
            else if (key == Keys.F) result.Outputs.Add(Output.Press(Keys.Enter));
            else if (key == Keys.Escape && rowWasSelected) { /* Cancel only the unfinished row. */ }
            else result.Handled = false; // Space, Enter, arrows and other keys belong to the IME/app.
            return result;
        }
    }
}

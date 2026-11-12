using System;
using System.Text;
using System.Windows.Forms;

namespace Hitote
{
    internal static class SelfTest
    {
        private static int count;
        private static void Equal(string expected, string actual)
        {
            count++;
            if (expected != actual) throw new Exception("Expected: " + expected + "; Actual: " + actual);
        }
        private static string Output(InputResult result)
        {
            var text = new StringBuilder();
            foreach (var item in result.Outputs) text.Append(item.Text ?? "[" + item.Key + "]");
            return text.ToString();
        }
        private static string Type(InputEngine engine, params Keys[] keys)
        {
            var text = new StringBuilder();
            foreach (Keys key in keys) text.Append(Output(engine.Process(key)));
            return text.ToString();
        }
        public static int Main()
        {
            try
            {
                var engine = new InputEngine();
                Equal("ki", Type(engine, Keys.W, Keys.S));
                Equal("[Back]gi", Type(engine, Keys.T));
                Equal("[Back]ki", Type(engine, Keys.T));
                Equal("ha[Back]ba[Back]pa[Back]ha", Type(engine, Keys.D, Keys.A, Keys.T, Keys.T, Keys.T));
                Equal("kiyo[Back]xyou", Type(engine, Keys.W, Keys.S, Keys.X, Keys.G, Keys.B, Keys.Q, Keys.D));
                Equal("n'a", Type(engine, Keys.V, Keys.D, Keys.Q, Keys.A));
                Equal("[Back]", Type(engine, Keys.R));
                Equal("[Back]", Type(engine, Keys.Back));
                Equal("", Type(engine, Keys.W, Keys.Back)); // Cancel a row before deleting text.
                Equal("", Type(engine, Keys.W, Keys.Escape));
                Equal("False", engine.Process(Keys.Escape).Handled.ToString());
                Equal("False", engine.Process(Keys.Space).Handled.ToString());
                Equal("", Type(engine, Keys.T)); // Never rewrite a character after conversion.
                Equal("[Return]", Type(engine, Keys.F));
                Equal(",.", Type(engine, Keys.G, Keys.H));
                Equal("hu", Type(engine, Keys.D, Keys.D)); // Repeated letter in two distinct taps.
                engine.Process(Keys.Left);
                Equal("", Type(engine, Keys.T)); // Caret movement invalidates the editable character.
                Type(engine, Keys.D, Keys.A);
                engine.Reset();
                Equal("", Type(engine, Keys.T)); // Same rule for focus/mouse/shortcut changes.
                foreach (var row in InputEngine.Rows)
                    foreach (var vowel in InputEngine.Vowels)
                    {
                        engine.Reset();
                        var result = Type(engine, row.Key, vowel);
                        if (result.Length == 0) throw new Exception("Missing kana mapping");
                        count++;
                    }
                Console.WriteLine("PASS: " + count + " checks (mapping, IME-boundary keys, voiced/small kana, backspace, reset)");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}

using System.Globalization;
using System.Text;

namespace SerpentsEyes.Core.GameData;

/// <summary>What a parsed segment of UE rich text represents.</summary>
public enum RichSegmentKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>A damage-type or styled span, e.g. &lt;BL&gt;Sanguine&lt;/&gt;. Tag carries the code ("BL").</summary>
    Styled,

    /// <summary>A &lt;math&gt;…&lt;/&gt; scaling formula. Text carries the raw expression.</summary>
    Math,

    /// <summary>A self-closing stat icon, e.g. &lt;Icon.Stats.Faith/&gt;. Text carries "Faith".</summary>
    StatIcon,

    /// <summary>A self-closing input glyph, e.g. &lt;input name="IA_Ability_Primary"/&gt;. Text carries the action name.</summary>
    Input,
}

/// <summary>One parsed piece of a rich-text string.</summary>
public sealed record RichSegment(RichSegmentKind Kind, string Text, string? Tag = null);

/// <summary>
/// Parser for the game's UE rich-text markup. Grammar notes: opening tags are
/// &lt;Name&gt; or &lt;Name attr=""&gt;, the closing tag is always the anonymous &lt;/&gt;,
/// and self-closing tags are &lt;Name/&gt; or &lt;Name attr="value"/&gt;.
/// </summary>
public static class UeRichText
{
    public static IReadOnlyList<RichSegment> Parse(string raw)
    {
        var segments = new List<RichSegment>();
        var plain = new StringBuilder();
        int i = 0;

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                segments.Add(new RichSegment(RichSegmentKind.Text, plain.ToString()));
                plain.Clear();
            }
        }

        while (i < raw.Length)
        {
            if (raw[i] != '<')
            {
                plain.Append(raw[i]);
                i++;
                continue;
            }

            int close = raw.IndexOf('>', i + 1);
            if (close < 0)
            {
                plain.Append(raw[i]);
                i++;
                continue;
            }

            string inside = raw[(i + 1)..close];
            if (inside.EndsWith('/')) // self-closing
            {
                FlushPlain();
                string body = inside[..^1].Trim();
                if (body.StartsWith("Icon.Stats.", StringComparison.OrdinalIgnoreCase))
                {
                    segments.Add(new RichSegment(RichSegmentKind.StatIcon, body["Icon.Stats.".Length..]));
                }
                else if (body.StartsWith("input", StringComparison.OrdinalIgnoreCase))
                {
                    int q1 = body.IndexOf('"');
                    int q2 = q1 >= 0 ? body.IndexOf('"', q1 + 1) : -1;
                    segments.Add(new RichSegment(RichSegmentKind.Input, q2 > q1 ? body[(q1 + 1)..q2] : body));
                }
                // Unknown self-closing tags are dropped silently.
                i = close + 1;
                continue;
            }

            // Opening tag: capture until the anonymous close </>.
            string tagName = inside.Split(' ', 2)[0];
            int end = raw.IndexOf("</>", close + 1, StringComparison.Ordinal);
            if (end < 0)
            {
                plain.Append(raw[i]);
                i++;
                continue;
            }

            FlushPlain();
            string content = raw[(close + 1)..end];
            if (tagName.Equals("math", StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(new RichSegment(RichSegmentKind.Math, content));
            }
            else
            {
                segments.Add(new RichSegment(RichSegmentKind.Styled, content, tagName));
            }
            i = end + 3;
        }

        FlushPlain();
        return segments;
    }
}

/// <summary>
/// Evaluates the game's scaling formulas: arithmetic over numbers, {l} (item level),
/// and {p_&lt;attr&gt;} (player attribute) placeholders, with + - * / and parentheses.
/// </summary>
public static class ScalingMath
{
    /// <summary>True when the formula depends only on item level (no player-attribute terms).</summary>
    public static bool IsLevelOnly(string expression) => !expression.Contains("{p_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Formats a formula for display with explicit grouping: "1+2*{l}" becomes
    /// "1 + (2 × Lv)". Multiplicative terms are parenthesized whenever they sit next
    /// to +/- so precedence never has to be inferred. Returns null if unparseable.
    /// </summary>
    public static string? TryFormat(string expression, Func<string, string> resolvePlaceholder)
    {
        try
        {
            var formatter = new Formatter(expression, resolvePlaceholder);
            var (text, _) = formatter.FormatExpression();
            return formatter.AtEnd ? text : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed class Formatter(string text, Func<string, string> resolve)
    {
        private int _pos;

        public bool AtEnd
        {
            get
            {
                SkipWhitespace();
                return _pos >= text.Length;
            }
        }

        public (string Text, bool IsMulti) FormatExpression()
        {
            var (result, isMulti) = FormatTerm();
            bool additive = false;
            while (true)
            {
                SkipWhitespace();
                char op = Peek();
                if (op is not ('+' or '-'))
                {
                    return additive ? (result, false) : (result, isMulti);
                }
                _pos++;
                if (!additive && isMulti)
                {
                    result = $"({result})"; // first term was a product; wrap it too
                }
                additive = true;
                var (rhs, rhsMulti) = FormatTerm();
                result += $" {(op == '+' ? '+' : '−')} {(rhsMulti ? $"({rhs})" : rhs)}";
            }
        }

        private (string Text, bool IsMulti) FormatTerm()
        {
            string result = FormatFactor();
            bool isMulti = false;
            while (true)
            {
                SkipWhitespace();
                char op = Peek();
                if (op is not ('*' or '/'))
                {
                    return (result, isMulti);
                }
                _pos++;
                isMulti = true;
                result += $" {(op == '*' ? '×' : '÷')} {FormatFactor()}";
            }
        }

        private string FormatFactor()
        {
            SkipWhitespace();
            char c = Peek();
            if (c == '(')
            {
                _pos++;
                var (inner, _) = FormatExpression();
                SkipWhitespace();
                if (Peek() != ')')
                {
                    throw new FormatException("expected )");
                }
                _pos++;
                return $"({inner})";
            }
            if (c == '-')
            {
                _pos++;
                return "−" + FormatFactor();
            }
            if (c == '{')
            {
                int end = text.IndexOf('}', _pos);
                if (end < 0)
                {
                    throw new FormatException("unterminated placeholder");
                }
                string name = text[(_pos + 1)..end];
                _pos = end + 1;
                return resolve(name);
            }
            int start = _pos;
            while (_pos < text.Length && (char.IsAsciiDigit(text[_pos]) || text[_pos] == '.'))
            {
                _pos++;
            }
            if (_pos == start)
            {
                throw new FormatException($"unexpected character at {_pos}");
            }
            return text[start.._pos];
        }

        private char Peek() => _pos < text.Length ? text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }

    /// <summary>Evaluates with the given item level; player attributes default to 1.</summary>
    public static bool TryEvaluate(string expression, int level, out double result)
    {
        try
        {
            var parser = new Parser(expression, level);
            result = parser.ParseExpression();
            return parser.AtEnd;
        }
        catch (FormatException)
        {
            result = 0;
            return false;
        }
    }

    private sealed class Parser(string text, int level)
    {
        private int _pos;

        public bool AtEnd
        {
            get
            {
                SkipWhitespace();
                return _pos >= text.Length;
            }
        }

        public double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '+')
                {
                    _pos++;
                    value += ParseTerm();
                }
                else if (Peek() == '-')
                {
                    _pos++;
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            double value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '*')
                {
                    _pos++;
                    value *= ParseFactor();
                }
                else if (Peek() == '/')
                {
                    _pos++;
                    double divisor = ParseFactor();
                    value = divisor == 0 ? throw new FormatException("division by zero") : value / divisor;
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            char c = Peek();
            if (c == '(')
            {
                _pos++;
                double value = ParseExpression();
                SkipWhitespace();
                if (Peek() != ')')
                {
                    throw new FormatException("expected )");
                }
                _pos++;
                return value;
            }
            if (c == '-')
            {
                _pos++;
                return -ParseFactor();
            }
            if (c == '{')
            {
                int end = text.IndexOf('}', _pos);
                if (end < 0)
                {
                    throw new FormatException("unterminated placeholder");
                }
                string name = text[(_pos + 1)..end];
                _pos = end + 1;
                return name.Equals("l", StringComparison.OrdinalIgnoreCase) ? level : 1; // {p_*} default to 1
            }
            int start = _pos;
            while (_pos < text.Length && (char.IsAsciiDigit(text[_pos]) || text[_pos] == '.'))
            {
                _pos++;
            }
            if (_pos == start)
            {
                throw new FormatException($"unexpected character at {_pos}");
            }
            return double.Parse(text[start.._pos], CultureInfo.InvariantCulture);
        }

        private char Peek() => _pos < text.Length ? text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }
}

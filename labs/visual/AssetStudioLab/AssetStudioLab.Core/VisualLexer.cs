using System.Globalization;
using System.Text;

namespace AssetStudioLab;

/// <summary>Tokenizes visual source, including units glued to numbers.</summary>
public sealed class VisualLexer
{
    private readonly string _text;
    private int _i;

    public VisualLexer(string text)
    {
        _text = text ?? string.Empty;
    }

    public VisualToken Next()
    {
        SkipTrivia();
        if (_i >= _text.Length)
            return new VisualToken(VisualTokenKind.Eof, string.Empty, _i);

        var start = _i;
        var c = _text[_i];
        if (c == '{') { _i++; return new VisualToken(VisualTokenKind.LBrace, "{", start); }
        if (c == '}') { _i++; return new VisualToken(VisualTokenKind.RBrace, "}", start); }
        if (c == '(') { _i++; return new VisualToken(VisualTokenKind.LParen, "(", start); }
        if (c == ')') { _i++; return new VisualToken(VisualTokenKind.RParen, ")", start); }
        if (c == ',') { _i++; return new VisualToken(VisualTokenKind.Comma, ",", start); }
        if (c == ':') { _i++; return new VisualToken(VisualTokenKind.Colon, ":", start); }
        if (c == ';') { _i++; return new VisualToken(VisualTokenKind.Semicolon, ";", start); }
        if (c == '*' && Peek(1) == '=') { _i += 2; return new VisualToken(VisualTokenKind.StarEqual, "*=", start); }
        if (c == '+' && Peek(1) == '=') { _i += 2; return new VisualToken(VisualTokenKind.PlusEqual, "+=", start); }
        if (c == '*') { _i++; return new VisualToken(VisualTokenKind.Star, "*", start); }
        if (c == '+') { _i++; return new VisualToken(VisualTokenKind.Plus, "+", start); }
        if (c == '-')
        {
            if (Peek(1) is >= '0' and <= '9' or '.')
                return ReadNumber();
            _i++;
            return new VisualToken(VisualTokenKind.Minus, "-", start);
        }

        if (c == '=') { _i++; return new VisualToken(VisualTokenKind.Equal, "=", start); }
        if (c == '#')
            return ReadHex();
        if (c is '"' or '\'')
            return ReadString();
        if (char.IsDigit(c) || c == '.')
            return ReadNumber();
        if (char.IsLetter(c) || c == '_')
            return ReadIdent();

        throw new VisualParseException($"Unexpected '{c}' at {start}.");
    }

    private VisualToken ReadIdent()
    {
        var start = _i;
        _i++;
        while (_i < _text.Length && (char.IsLetterOrDigit(_text[_i]) || _text[_i] == '_'))
            _i++;
        return new VisualToken(VisualTokenKind.Ident, _text[start.._i], start);
    }

    private VisualToken ReadNumber()
    {
        var start = _i;
        if (_text[_i] == '-')
            _i++;
        while (_i < _text.Length && (char.IsDigit(_text[_i]) || _text[_i] == '.'))
            _i++;
        var raw = _text[start.._i];
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var magnitude))
            throw new VisualParseException($"Invalid number '{raw}' at {start}.");

        SkipWhitespaceOnly();
        var unitStart = _i;
        if (_i < _text.Length && _text[_i] == '/' && Peek(1) == 's' && !char.IsLetterOrDigit(Peek(2)))
        {
            _i += 2;
            if (!VisualQuantityParser.TryParse(magnitude, "/s", out var perSecond))
                throw new VisualParseException($"Invalid unit /s at {start}.");
            return new VisualToken(VisualTokenKind.Number, _text[start.._i], start, perSecond);
        }

        if (_i < _text.Length && char.IsLetter(_text[_i]))
        {
            while (_i < _text.Length && char.IsLetter(_text[_i]))
                _i++;
            var unit = _text[unitStart.._i];
            if (VisualQuantityParser.TryParse(magnitude, unit, out var quantity))
                return new VisualToken(VisualTokenKind.Number, _text[start.._i], start, quantity);
            _i = unitStart;
        }

        return new VisualToken(
            VisualTokenKind.Number,
            raw,
            start,
            VisualValue.FromScalar(magnitude));
    }

    private VisualToken ReadHex()
    {
        var start = _i;
        _i++;
        while (_i < _text.Length && char.IsAsciiHexDigit(_text[_i]))
            _i++;
        var text = _text[start.._i];
        if (!VisualColors.TryParse(text, out var color))
            throw new VisualParseException($"Invalid color '{text}' at {start}.");
        return new VisualToken(VisualTokenKind.HexColor, text, start, VisualValue.FromColor(color));
    }

    private VisualToken ReadString()
    {
        var quote = _text[_i++];
        var start = _i - 1;
        var sb = new StringBuilder();
        while (_i < _text.Length && _text[_i] != quote)
        {
            if (_text[_i] == '\\' && _i + 1 < _text.Length)
            {
                _i++;
                sb.Append(_text[_i++]);
                continue;
            }

            sb.Append(_text[_i++]);
        }

        if (_i >= _text.Length)
            throw new VisualParseException($"Unterminated string at {start}.");
        _i++;
        return new VisualToken(VisualTokenKind.String, sb.ToString(), start);
    }

    private void SkipTrivia()
    {
        while (_i < _text.Length)
        {
            if (char.IsWhiteSpace(_text[_i]))
            {
                _i++;
                continue;
            }

            if (_text[_i] == '/' && Peek(1) == '/')
            {
                _i += 2;
                while (_i < _text.Length && _text[_i] is not ('\r' or '\n'))
                    _i++;
                continue;
            }

            if (_text[_i] == '/' && Peek(1) == '*')
            {
                _i += 2;
                while (_i + 1 < _text.Length && !(_text[_i] == '*' && _text[_i + 1] == '/'))
                    _i++;
                _i = Math.Min(_text.Length, _i + 2);
                continue;
            }

            break;
        }
    }

    private void SkipWhitespaceOnly()
    {
        while (_i < _text.Length && char.IsWhiteSpace(_text[_i]))
            _i++;
    }

    private char Peek(int offset)
    {
        var j = _i + offset;
        return j < _text.Length ? _text[j] : '\0';
    }
}

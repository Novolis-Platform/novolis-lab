namespace AssetStudioLab;

/// <summary>Parses the visual source language into the shared AST.</summary>
public sealed class VisualLanguageParser
{
    private readonly List<VisualToken> _tokens = [];
    private int _i;

    public static VisualParseResult Parse(string? source)
    {
        try
        {
            var parser = new VisualLanguageParser();
            var document = parser.ParseDocument(source ?? string.Empty);
            return VisualParseResult.Ok(document);
        }
        catch (VisualParseException ex)
        {
            return VisualParseResult.Fail(ex.Message);
        }
    }

    private VisualDocument ParseDocument(string source)
    {
        var lexer = new VisualLexer(source);
        while (true)
        {
            var token = lexer.Next();
            _tokens.Add(token);
            if (token.Kind == VisualTokenKind.Eof)
                break;
        }

        var definitions = new List<VisualDefinition>();
        while (!Check(VisualTokenKind.Eof))
            definitions.Add(ParseDefinition());
        return new VisualDocument(definitions);
    }

    private VisualDefinition ParseDefinition()
    {
        var kindToken = Consume(VisualTokenKind.Ident, "Expected a definition kind.");
        var kind = ParseKind(kindToken.Text);
        var name = Consume(VisualTokenKind.Ident, "Expected a definition name.").Text;
        var parameters = Check(VisualTokenKind.LParen)
            ? ParseParameters()
            : Array.Empty<VisualParameter>();
        var (properties, children) = ParseBlock();
        return new VisualDefinition
        {
            Kind = kind,
            Name = name,
            Parameters = parameters,
            Properties = properties,
            Children = children,
        };
    }

    private IReadOnlyList<VisualParameter> ParseParameters()
    {
        Consume(VisualTokenKind.LParen, "Expected '('.");
        var list = new List<VisualParameter>();
        if (!Check(VisualTokenKind.RParen))
        {
            while (true)
            {
                var name = Consume(VisualTokenKind.Ident, "Expected parameter name.").Text;
                VisualExpr defaultExpr = VisualExpr.Lit(VisualValue.FromScalar(0));
                if (Match(VisualTokenKind.Equal))
                    defaultExpr = ParseExpression();
                list.Add(new VisualParameter(name, InferKind(defaultExpr), defaultExpr));
                if (!Match(VisualTokenKind.Comma))
                    break;
            }
        }

        Consume(VisualTokenKind.RParen, "Expected ')'.");
        return list;
    }

    private (List<VisualProperty> Properties, List<VisualDefinition> Children) ParseBlock()
    {
        Consume(VisualTokenKind.LBrace, "Expected '{'.");
        var properties = new List<VisualProperty>();
        var children = new List<VisualDefinition>();
        while (!Check(VisualTokenKind.RBrace) && !Check(VisualTokenKind.Eof))
        {
            if (Check(VisualTokenKind.Ident) && PeekTextEquals("layer"))
            {
                Advance();
                var layer = ParseCallOrIdent();
                if (layer.Kind != VisualExprKind.Call)
                    throw new VisualParseException("Expected a layer call.");
                properties.Add(new VisualProperty(layer.Name!, layer, VisualAssignOp.Layer));
                Match(VisualTokenKind.Semicolon);
                continue;
            }

            var ident = Consume(VisualTokenKind.Ident, "Expected a property name.").Text;
            if (Check(VisualTokenKind.LBrace))
            {
                var (childProps, nested) = ParseBlock();
                children.Add(new VisualDefinition
                {
                    Kind = ParseNestedKind(ident),
                    Name = ident,
                    Properties = childProps,
                    Children = nested,
                });
                continue;
            }

            var assign = VisualAssignOp.Set;
            if (Match(VisualTokenKind.StarEqual))
                assign = VisualAssignOp.Multiply;
            else if (Match(VisualTokenKind.PlusEqual))
                assign = VisualAssignOp.Add;
            else
                Match(VisualTokenKind.Equal);

            VisualExpr value;
            if (Check(VisualTokenKind.LParen))
                value = FinishCall(ident);
            else if (IsFlagProperty(ident) && !IsExpressionStart())
                value = VisualExpr.Lit(VisualValue.FromBool(true));
            else
                value = ParseExpression();
            properties.Add(new VisualProperty(ident, value, assign));
            Match(VisualTokenKind.Semicolon);
        }

        Consume(VisualTokenKind.RBrace, "Expected '}'.");
        return (properties, children);
    }

    private VisualExpr ParseExpression() => ParseBinary();

    private VisualExpr ParseBinary()
    {
        var left = ParseUnary();
        while (true)
        {
            if (Match(VisualTokenKind.Star))
            {
                left = VisualExpr.Binary(VisualBinaryOp.Multiply, left, ParseUnary());
                continue;
            }

            if (Match(VisualTokenKind.Plus))
            {
                left = VisualExpr.Binary(VisualBinaryOp.Add, left, ParseUnary());
                continue;
            }

            if (Match(VisualTokenKind.Minus))
            {
                left = VisualExpr.Binary(VisualBinaryOp.Subtract, left, ParseUnary());
                continue;
            }

            return left;
        }
    }

    private VisualExpr ParseUnary()
    {
        if (Match(VisualTokenKind.Minus))
        {
            var inner = ParseUnary();
            return VisualExpr.Binary(
                VisualBinaryOp.Subtract,
                VisualExpr.Lit(VisualValue.FromScalar(0)),
                inner);
        }

        return ParsePrimary();
    }

    private VisualExpr ParsePrimary()
    {
        if (Match(VisualTokenKind.LParen))
        {
            var inner = ParseExpression();
            Consume(VisualTokenKind.RParen, "Expected ')'.");
            return inner;
        }

        if (Check(VisualTokenKind.Number))
        {
            var token = Advance();
            return VisualExpr.Lit(token.Number ?? VisualValue.FromScalar(0));
        }

        if (Check(VisualTokenKind.HexColor))
        {
            var token = Advance();
            return VisualExpr.Lit(token.Number ?? VisualValue.FromScalar(0));
        }

        if (Check(VisualTokenKind.String))
        {
            var token = Advance();
            if (VisualColors.TryParse(token.Text, out var color))
                return VisualExpr.Lit(VisualValue.FromColor(color));
            return VisualExpr.Ident(token.Text);
        }

        if (Check(VisualTokenKind.Ident))
            return ParseCallOrIdent();

        throw new VisualParseException($"Expected an expression near '{Peek().Text}'.");
    }

    private VisualExpr FinishCall(string name)
    {
        Consume(VisualTokenKind.LParen, "Expected '('.");
        var args = ParseArgList();
        Consume(VisualTokenKind.RParen, "Expected ')'.");
        return VisualExpr.Call(name, args);
    }

    private List<VisualArg> ParseArgList()
    {
        var args = new List<VisualArg>();
        if (Check(VisualTokenKind.RParen))
            return args;
        while (true)
        {
            args.Add(ParseArg());
            if (!Match(VisualTokenKind.Comma))
                break;
        }

        return args;
    }

    private VisualExpr ParseCallOrIdent()
    {
        var name = Consume(VisualTokenKind.Ident, "Expected a name.").Text;
        if (!Check(VisualTokenKind.LParen))
        {
            if (VisualColors.TryParse(name, out var namedColor))
                return VisualExpr.Lit(VisualValue.FromColor(namedColor));
            if (name.Equals("y", StringComparison.OrdinalIgnoreCase))
                return VisualExpr.Lit(VisualValue.FromVector3(0, 1, 0));
            if (name.Equals("x", StringComparison.OrdinalIgnoreCase))
                return VisualExpr.Lit(VisualValue.FromVector3(1, 0, 0));
            if (name.Equals("z", StringComparison.OrdinalIgnoreCase))
                return VisualExpr.Lit(VisualValue.FromVector3(0, 0, 1));
            if (name.Equals("true", StringComparison.OrdinalIgnoreCase))
                return VisualExpr.Lit(VisualValue.FromBool(true));
            if (name.Equals("false", StringComparison.OrdinalIgnoreCase))
                return VisualExpr.Lit(VisualValue.FromBool(false));
            return VisualExpr.Ident(name);
        }

        Advance();
        var args = ParseArgList();
        Consume(VisualTokenKind.RParen, "Expected ')'.");
        return VisualExpr.Call(name, args);
    }

    private VisualArg ParseArg()
    {
        if (Check(VisualTokenKind.Ident) && PeekKind(1) == VisualTokenKind.Colon)
        {
            var name = Advance().Text;
            Advance();
            return VisualArg.Named(name, ParseExpression());
        }

        return VisualArg.Positional(ParseExpression());
    }

    private static VisualDefinitionKind ParseKind(string text) =>
        text.ToLowerInvariant() switch
        {
            "material" => VisualDefinitionKind.Material,
            "light" => VisualDefinitionKind.Light,
            "volume" => VisualDefinitionKind.Volume,
            "effect" => VisualDefinitionKind.Effect,
            "geometry" => VisualDefinitionKind.Geometry,
            "surface" => VisualDefinitionKind.Surface,
            "trail" => VisualDefinitionKind.Trail,
            "emitter" => VisualDefinitionKind.Emitter,
            _ => throw new VisualParseException($"Unknown definition kind '{text}'."),
        };

    private static VisualDefinitionKind ParseNestedKind(string text) =>
        text.ToLowerInvariant() switch
        {
            "surface" => VisualDefinitionKind.Surface,
            "trail" => VisualDefinitionKind.Trail,
            "light" => VisualDefinitionKind.Light,
            "geometry" => VisualDefinitionKind.Geometry,
            "emitter" => VisualDefinitionKind.Emitter,
            _ => VisualDefinitionKind.Surface,
        };

    private static VisualKind InferKind(VisualExpr expr)
    {
        if (expr.Kind == VisualExprKind.Literal && expr.Literal is not null)
            return expr.Literal.Kind;
        return VisualKind.Scalar;
    }

    private bool IsExpressionStart()
    {
        var token = Peek();
        if (token.Kind is VisualTokenKind.Number or VisualTokenKind.HexColor or VisualTokenKind.String
            or VisualTokenKind.LParen or VisualTokenKind.Star or VisualTokenKind.Plus or VisualTokenKind.Minus)
            return true;
        if (token.Kind != VisualTokenKind.Ident)
            return false;
        if (PeekKind(1) == VisualTokenKind.LParen)
            return true;
        if (IsValueIdent(token.Text))
            return true;
        return !IsPropertyName(token.Text);
    }

    private static bool IsFlagProperty(string text) =>
        text.Equals("spot", StringComparison.OrdinalIgnoreCase)
        || text.Equals("directional", StringComparison.OrdinalIgnoreCase);

    private static bool IsPropertyName(string text) =>
        text.Equals("color", StringComparison.OrdinalIgnoreCase)
        || text.Equals("roughness", StringComparison.OrdinalIgnoreCase)
        || text.Equals("normal", StringComparison.OrdinalIgnoreCase)
        || text.Equals("emission", StringComparison.OrdinalIgnoreCase)
        || text.Equals("density", StringComparison.OrdinalIgnoreCase)
        || text.Equals("scattering", StringComparison.OrdinalIgnoreCase)
        || text.Equals("temperature", StringComparison.OrdinalIgnoreCase)
        || text.Equals("intensity", StringComparison.OrdinalIgnoreCase)
        || text.Equals("range", StringComparison.OrdinalIgnoreCase)
        || text.Equals("cone", StringComparison.OrdinalIgnoreCase)
        || text.Equals("penumbra", StringComparison.OrdinalIgnoreCase)
        || text.Equals("shadows", StringComparison.OrdinalIgnoreCase)
        || text.Equals("rate", StringComparison.OrdinalIgnoreCase)
        || text.Equals("lifetime", StringComparison.OrdinalIgnoreCase)
        || text.Equals("velocity", StringComparison.OrdinalIgnoreCase)
        || text.Equals("geometry", StringComparison.OrdinalIgnoreCase)
        || text.Equals("light", StringComparison.OrdinalIgnoreCase)
        || text.Equals("spot", StringComparison.OrdinalIgnoreCase)
        || text.Equals("material", StringComparison.OrdinalIgnoreCase)
        || text.Equals("wear", StringComparison.OrdinalIgnoreCase)
        || text.Equals("grime", StringComparison.OrdinalIgnoreCase);

    private static bool IsValueIdent(string text) =>
        text.Equals("x", StringComparison.OrdinalIgnoreCase)
        || text.Equals("y", StringComparison.OrdinalIgnoreCase)
        || text.Equals("z", StringComparison.OrdinalIgnoreCase)
        || text.Equals("true", StringComparison.OrdinalIgnoreCase)
        || text.Equals("false", StringComparison.OrdinalIgnoreCase)
        || text.Equals("inherit", StringComparison.OrdinalIgnoreCase)
        || text.Equals("hard", StringComparison.OrdinalIgnoreCase)
        || text.Equals("soft", StringComparison.OrdinalIgnoreCase)
        || text.Equals("spot", StringComparison.OrdinalIgnoreCase)
        || text.Equals("directional", StringComparison.OrdinalIgnoreCase)
        || text.Equals("point", StringComparison.OrdinalIgnoreCase)
        || VisualColors.TryParse(text, out _);

    private bool PeekTextEquals(string text) =>
        Peek().Kind == VisualTokenKind.Ident
        && string.Equals(Peek().Text, text, StringComparison.OrdinalIgnoreCase);

    private VisualToken Peek() => _tokens[Math.Min(_i, _tokens.Count - 1)];

    private VisualTokenKind PeekKind(int offset)
    {
        var j = _i + offset;
        return j < _tokens.Count ? _tokens[j].Kind : VisualTokenKind.Eof;
    }

    private bool Check(VisualTokenKind kind) => Peek().Kind == kind;

    private bool Match(VisualTokenKind kind)
    {
        if (!Check(kind))
            return false;
        Advance();
        return true;
    }

    private VisualToken Advance()
    {
        var token = Peek();
        if (_i < _tokens.Count - 1)
            _i++;
        return token;
    }

    private VisualToken Consume(VisualTokenKind kind, string message)
    {
        if (!Check(kind))
            throw new VisualParseException($"{message} Found '{Peek().Text}'.");
        return Advance();
    }
}

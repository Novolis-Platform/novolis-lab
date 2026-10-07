namespace AssetStudioLab;

/// <summary>Lexer token kind.</summary>
public enum VisualTokenKind
{
    Ident,
    Number,
    HexColor,
    String,
    LBrace,
    RBrace,
    LParen,
    RParen,
    Comma,
    Colon,
    Semicolon,
    StarEqual,
    PlusEqual,
    Equal,
    Star,
    Plus,
    Minus,
    Eof,
}

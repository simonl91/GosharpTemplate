using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace GosharpTemplate
{

    internal class Lexer
    {
        private readonly string text;
        private int start;
        private int current;
        private List<int> newlineIndicies;
        private List<string> errorData;
        private List<Token> tokens;

        public Lexer(ref string textInput)
        {
            text = textInput;
            newlineIndicies = new List<int>();
            errorData = new List<string>();
            tokens = new List<Token>(text.Length);
        }

        internal List<Token> Lex()
        {
            start = 0;
            current = 0;
            errorData.Clear();
            newlineIndicies.Clear();
            tokens.Clear();

            // consume everything as html until end or '{{'
            for (int i = 0; i < text.Length; i++)
            {
                if (IsAtEnd())
                {
                    if (CurrentLength() > 0)
                        tokens.Add(MakeToken(TokenKind.Html, start, current));
                    tokens.Add(MakeToken(TokenKind.Eof));
                    return tokens;
                }
                if (IsAtOpenBraceDouble())
                {
                    // Tokenize everyting before '{{' as html
                    if (CurrentLength() > 0)
                        tokens.Add(MakeToken(TokenKind.Html, start, current));
                    start = current;
                    // Advance over opening bracket
                    Advance();
                    Advance();
                    tokens.Add(MakeToken(TokenKind.OpenBraceDouble));
                    // Tokenize from '{{' to '}}'
                    LexTemplateExpression();
                    start = current;
                    if (IsAtEnd())
                    {
                        tokens.Add(MakeToken(TokenKind.Eof));
                        return tokens;
                    }
                    continue;
                }
                var c = Advance();
                if (c == '\n') newlineIndicies.Add(current - 1);
            }
            throw new ApplicationException("should be unreachable");
        }

        internal void PrintToken(Token token)
        {
            var line = GetLine(token.Start);
            var endLine = GetLine(token.Start + token.Length);
            var col = GetColumn(token.Start);
            var endcol = GetColumn(token.Start + token.Length);
            var sb = new StringBuilder();
            if (line == endLine)
                sb.Append($"{line} {col}-{endcol}:");
            else
                sb.Append($"{line}:{col}-{endLine}:{endcol}:");
            sb.AppendLine(token.ToString());
            //sb.AppendLine(template2.Substring(token.Start, token.Length));
            sb.AppendLine(GetText(token));
            Console.WriteLine(sb.ToString());
        }

        // tokenize inside of {{ }}
        private void LexTemplateExpression()
        {
            for (int i = 0; i < text.Length; i++)
            {
                SkipWhitespace();
                start = current;
                if (IsAtEnd()) return;

                if (IsAtClosingBraceDouble())
                {
                    Advance();
                    Advance();
                    tokens.Add(MakeToken(TokenKind.ClosingBraceDouble));
                    return;
                }
                var c = Advance();

                if (char.IsLetter(c) || c == '_')
                {
                    tokens.Add(CreateIdentifier());
                    continue;
                }
                if (c == '-')
                {
                    tokens.Add(MakeToken(TokenKind.Dash));
                    continue;
                }
                if (c == '.')
                {
                    tokens.Add(MakeToken(TokenKind.Dot));
                    continue;
                }
                if (c == '"')
                {
                    var stringToken = CreateString();
                    tokens.Add(stringToken);
                    if (stringToken.Kind == TokenKind.String) continue;
                    else return;
                }
                if (c == '\n')
                {
                    tokens.Add(ErrorToken("Unterminated '{{ }}'"));
                    newlineIndicies.Add(current - 1);
                    return;
                }
                tokens.Add(ErrorToken($"'{c}' is not a valid inside '{{{{ ... }}}}'"));
            }
        }

        private Token CreateIdentifier()
        {
            while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Advance();
            var identType = IdentifierType();
            return MakeToken(identType);
        }

        private TokenKind IdentifierType()
        {
            var identifier = text.Substring(start, current - start);
            switch (identifier)
            {
                case "define":
                    return TokenKind.KeywordDefine;
                case "block":
                    return TokenKind.KeywordBlock;
                case "with":
                    return TokenKind.KeywordWith;
                case "template":
                    return TokenKind.KeywordTemplate;
                case "end":
                    return TokenKind.KeywordEnd;
                case "if":
                    return TokenKind.KeywordIf;
                case "else":
                    return TokenKind.KeywordElse;
                case "range":
                    return TokenKind.KeywordRange;
                default:
                    return TokenKind.Ident;
            }
        }

        internal string GetErrorData(int index)
        {
            return errorData[index];
        }

        internal int GetLine(int index)
        {
            if (newlineIndicies.Count == 0) return 1;
            var i = 0;
            for (i = 0; i < newlineIndicies.Count; i++)
            {
                if (index < newlineIndicies[i]) return i + 1;
            }
            return i + 1;
        }

        internal int GetColumn(int index)
        {
            if (newlineIndicies.Count == 0)
                return index + 1;
            for (int i = 0; i < newlineIndicies.Count; i++)
            {
                if (newlineIndicies[i] > index)
                {
                    if (i == 0) return index + 1;
                    return index - newlineIndicies[i - 1];
                }
            }
            return index - newlineIndicies[newlineIndicies.Count - 1];
        }

        internal string GetText(Token token)
        {
            return text.Substring(token.Start, token.Length);
        }

        internal string GetTextFromStringToken(Token token)
        {
            Debug.Assert(token.Kind == TokenKind.String,
                "Expected string token");
            return text.Substring(token.Start + 1, token.Length - 2);
        }
        private Token CreateString()
        {
            while (Peek() != '"' && !IsAtEnd())
            {
                if (Peek() == '\n')
                {
                    newlineIndicies.Add(current);
                    Advance();
                    return ErrorToken("Unterminated string.");
                }
                Advance();
            }
            if (IsAtEnd()) return ErrorToken("Unterminated string.");
            // closing quote
            Advance();
            return MakeToken(TokenKind.String);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CurrentLength()
        {
            return current - start;
        }

        private Token MakeToken(TokenKind kind)
        {
            var token = new Token()
            {
                Kind = kind,
                Start = start,
                DataIdx = -1,
                Length = current - start
            };
            return token;
        }

        private Token MakeToken(TokenKind kind, int from, int to)
        {
            var token = new Token()
            {
                Kind = kind,
                Start = from,
                DataIdx = -1,
                Length = to - from
            };
            return token;
        }

        private Token ErrorToken(string errorMessage)
        {
            var length = current - start;
            var line = GetLine(start);
            var fromCol = GetColumn(start);
            var toCol = fromCol + (length - 1);
            var extraDataIndex = errorData.Count;
            errorData.Add($"{line} {fromCol}-{toCol} error: {errorMessage}");
            var token = new Token()
            {
                Kind = TokenKind.Error,
                Start = start,
                DataIdx = extraDataIndex,
                Length = length
            };
            return token;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtOpenBraceDouble()
        {
            if (current + 1 >= text.Length) return false;
            if (Nth(0) == '{' && Nth(1) == '{') return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtClosingBraceDouble()
        {
            if (current + 1 >= text.Length) return false;
            if (Nth(0) == '}' && Nth(1) == '}') return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtEnd()
        {
            return current >= text.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek()
        {
            return text[current];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Nth(int n)
        {
            return n < text.Length ?
                text[current + n]
                : text[text.Length - 1];
        }

        private void SkipWhitespace()
        {
            while (Peek() == ' ' || Peek() == '\r' || Peek() == '\t')
            {
                Advance();
            }
        }

        private char Advance()
        {
            current++;
            return text[current - 1];
        }

    }

    internal struct Token
    {
        internal int Start;
        internal int Length;
        internal int DataIdx;
        internal TokenKind Kind;

        public override readonly string ToString()
        {
            return $"Token: {Enum.GetName(typeof(TokenKind), Kind)}";
        }
    }

    internal enum TokenKind
    {
        OpenBraceDouble,
        ClosingBraceDouble,
        Dot,
        Ident,
        Html,
        String,
        Error,
        Eof,
        Dash,
        KeywordDefine,
        KeywordBlock,
        KeywordWith,
        KeywordIf,
        KeywordElse,
        KeywordTemplate,
        KeywordEnd,
        KeywordRange,
    }

}

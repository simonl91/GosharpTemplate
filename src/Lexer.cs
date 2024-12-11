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
            if (errorData.Count != 0) errorData.Clear();
            if (newlineIndicies.Count != 0) newlineIndicies.Clear();
            if (tokens.Count != 0) tokens.Clear();

            // consume everything as html until end or '{{'
            for (int i = 0; i < text.Length; i++)
            {
                if (isAtEnd())
                {
                    if (currentLength() > 0)
                        tokens.Add(makeToken(TokenKind.Html, start, current - 1));
                    tokens.Add(makeToken(TokenKind.Eof));
                    return tokens;
                }
                if (isAtOpenBraceDouble())
                {
                    if (currentLength() > 0)
                        tokens.Add(makeToken(TokenKind.Html, start, current));
                    start = current;
                    advance();
                    advance();
                    tokens.Add(makeToken(TokenKind.OpenBraceDouble));
                    LexTemplateExpression();
                    start = current;
                    if (isAtEnd())
                    {
                        tokens.Add(makeToken(TokenKind.Eof));
                        return tokens;
                    }
                    continue;
                }
                var c = advance();
                if (c == '\n') newlineIndicies.Add(current - 1);
            }
            Debug.Assert(false, "unreachable!");
            return tokens;
        }

        internal void PrintToken(Token token)
        {
            var line = GetLine(token.Start);
            var col = GetColumn(token.Start);
            var col2 = col + token.Length - 1;
            var sb = new StringBuilder();
            sb.Append($"{line} {col}-{col2}:");
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
                skipWhitespace();
                start = current;
                if (isAtEnd()) return;

                if (isAtClosingBraceDouble())
                {
                    advance();
                    advance();
                    tokens.Add(makeToken(TokenKind.ClosingBraceDouble));
                    return;
                }
                var c = advance();

                if (char.IsLetter(c) || c == '_')
                {
                    tokens.Add(createIdentifier());
                    continue;
                }
                if (c == '.')
                {
                    tokens.Add(makeToken(TokenKind.Dot));
                    continue;
                }
                if (c == '"')
                {
                    tokens.Add(createString());
                    continue;
                }
                if (c == '\n')
                {
                    newlineIndicies.Add(current - 1);
                    continue;
                }
                tokens.Add(errorToken("not a valid token"));
            }
        }

        private Token createIdentifier()
        {
            while (char.IsLetterOrDigit(peek()) || peek() == '_') advance();
            var identType = identifierType();
            return makeToken(identType);
        }

        private TokenKind identifierType()
        {
            var identifier = text.Substring(start, current - start);
            switch (identifier)
            {
                case "define":
                    return TokenKind.KeywordDefine;
                case "block":
                    return TokenKind.KeywordBlock;
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
            for (int i = 0; i < newlineIndicies.Count; i++)
            {
                if (newlineIndicies[i] > index) return i + 1;
            }
            return 1;
        }

        internal int GetColumn(int index)
        {
            for (int i = 0; i < newlineIndicies.Count; i++)
            {
                if (newlineIndicies[i] > index)
                {
                    if (i == 0) return index + 1;
                    return index - newlineIndicies[i - 1];
                }
            }
            return index + 1;
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

        private Token createString()
        {
            while (peek() != '"' && !isAtEnd())
            {
                if (peek() == '\n') newlineIndicies.Add(current);
                advance();
            }
            if (isAtEnd()) return errorToken("Unterminated string.");
            // closing quote
            advance();
            return makeToken(TokenKind.String);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int currentLength()
        {
            return current - start;
        }

        private Token makeToken(TokenKind kind)
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

        private Token makeToken(TokenKind kind, int from, int to)
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


        private Token errorToken(string errorMessage)
        {
            var length = current - start;
            var line = GetLine(start);
            var fromCol = GetColumn(start);
            var toCol = fromCol + (length - 1);
            var extraDataIndex = errorData.Count;
            errorData.Add($"{line} col {fromCol}-{toCol} error: {errorMessage}");
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
        private bool isAtOpenBraceDouble()
        {
            if (current + 1 >= text.Length) return false;
            if (nth(0) == '{' && nth(1) == '{') return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool isAtClosingBraceDouble()
        {
            if (current + 1 >= text.Length) return false;
            if (nth(0) == '}' && nth(1) == '}') return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool isAtEnd()
        {
            return current >= text.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char peek()
        {
            return text[current];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char nth(int n)
        {
            return n < text.Length ?
                text[current + n]
                : text[text.Length - 1];
        }

        private bool match(char expected)
        {
            if (isAtEnd()) return false;
            if (text[current] != expected) return false;
            current++;
            return true;
        }

        private bool match(Func<char, bool> expected)
        {
            if (isAtEnd()) return false;
            if (!expected.Invoke(text[current])) return false;
            current++;
            return true;
        }

        private void skipWhitespace()
        {
            while (peek() == ' ' || peek() == '\r' || peek() == '\t')
            {
                advance();
            }
        }

        private char advance()
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

        public override string ToString()
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
        KeywordDefine,
        KeywordBlock,
        KeywordIf,
        KeywordElse,
        KeywordTemplate,
        KeywordEnd,
        KeywordRange,
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GnuCashUtils.Categorization;

/// <summary>
/// Parses merchant matching DSL rules into compiled Expression predicates.
///
/// Supported syntax:
///   contains("text")       - case-insensitive substring match on Description
///   startswith("text")     - case-insensitive prefix match on Description
///   endswith("text")       - case-insensitive suffix match on Description
///   oneof("a", "b", ...)   - case-insensitive contains-any match on Description
///   regex("pattern")       - regex match on Description (case-insensitive)
///   amount > 10            - amount comparison (>, &lt;, >=, &lt;=, ==, !=)
///   expr and expr          - logical AND
///   expr or expr           - logical OR
///   (expr)                 - grouping
/// </summary>
public class MerchantRuleParser
{
    private static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod("Contains", [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod("StartsWith", [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod("EndsWith", [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo RegexIsMatchMethod =
        typeof(Regex).GetMethod("IsMatch", [typeof(string), typeof(string), typeof(RegexOptions)])!;

    public Expression<Func<CategorizationRowViewModel, bool>> Parse(string rule)
    {
        var tokens = Tokenize(rule);
        var reader = new TokenReader(tokens);
        var param = Expression.Parameter(typeof(CategorizationRowViewModel), "row");
        var body = ParseOr(reader, param);
        if (!reader.IsEof)
            throw new FormatException($"Unexpected token '{reader.Current.Value}' in rule: {rule}");
        return Expression.Lambda<Func<CategorizationRowViewModel, bool>>(body, param);
    }

    private static Expression ParseOr(TokenReader reader, ParameterExpression param)
    {
        var left = ParseAnd(reader, param);
        while (reader.TryConsume(TokenType.Or))
        {
            var right = ParseAnd(reader, param);
            left = Expression.OrElse(left, right);
        }
        return left;
    }

    private static Expression ParseAnd(TokenReader reader, ParameterExpression param)
    {
        var left = ParsePrimary(reader, param);
        while (reader.TryConsume(TokenType.And))
        {
            var right = ParsePrimary(reader, param);
            left = Expression.AndAlso(left, right);
        }
        return left;
    }

    private static Expression ParsePrimary(TokenReader reader, ParameterExpression param)
    {
        var desc = Expression.Property(param, nameof(CategorizationRowViewModel.Description));
        var amount = Expression.Property(param, nameof(CategorizationRowViewModel.Amount));

        if (reader.TryConsume(TokenType.LParen))
        {
            var inner = ParseOr(reader, param);
            reader.Consume(TokenType.RParen);
            return inner;
        }

        if (reader.TryConsume(TokenType.Contains))
        {
            reader.Consume(TokenType.LParen);
            var str = reader.ConsumeString();
            reader.Consume(TokenType.RParen);
            return Expression.Call(desc, StringContainsMethod,
                Expression.Constant(str),
                Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        if (reader.TryConsume(TokenType.StartsWith))
        {
            reader.Consume(TokenType.LParen);
            var str = reader.ConsumeString();
            reader.Consume(TokenType.RParen);
            return Expression.Call(desc, StringStartsWithMethod,
                Expression.Constant(str),
                Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        if (reader.TryConsume(TokenType.EndsWith))
        {
            reader.Consume(TokenType.LParen);
            var str = reader.ConsumeString();
            reader.Consume(TokenType.RParen);
            return Expression.Call(desc, StringEndsWithMethod,
                Expression.Constant(str),
                Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        if (reader.TryConsume(TokenType.OneOf))
        {
            reader.Consume(TokenType.LParen);
            var strings = reader.ConsumeStringList();
            reader.Consume(TokenType.RParen);
            var parts = strings
                .Select(s => (Expression)Expression.Call(desc, StringContainsMethod,
                    Expression.Constant(s),
                    Expression.Constant(StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return parts.Aggregate(Expression.OrElse);
        }

        if (reader.TryConsume(TokenType.Regex))
        {
            reader.Consume(TokenType.LParen);
            var pattern = reader.ConsumeString();
            reader.Consume(TokenType.RParen);
            return Expression.Call(RegexIsMatchMethod,
                desc,
                Expression.Constant(pattern),
                Expression.Constant(RegexOptions.IgnoreCase));
        }

        if (reader.TryConsume(TokenType.Amount))
        {
            var op = reader.Current.Type;
            if (op is not (TokenType.Gt or TokenType.Lt or TokenType.Gte or TokenType.Lte or TokenType.Eq or TokenType.Neq))
                throw new FormatException($"Expected comparison operator after 'amount' but got '{reader.Current.Value}'");
            reader.Advance();
            var number = reader.ConsumeNumber();
            var constant = Expression.Constant(number, typeof(decimal));
            return op switch
            {
                TokenType.Gt  => Expression.GreaterThan(amount, constant),
                TokenType.Lt  => Expression.LessThan(amount, constant),
                TokenType.Gte => Expression.GreaterThanOrEqual(amount, constant),
                TokenType.Lte => Expression.LessThanOrEqual(amount, constant),
                TokenType.Eq  => Expression.Equal(amount, constant),
                TokenType.Neq => Expression.NotEqual(amount, constant),
                _             => throw new InvalidOperationException()
            };
        }

        throw new FormatException($"Unexpected token '{reader.Current.Value}'");
    }

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i])) { i++; continue; }

            if (input[i] == '"')
            {
                var sb = new StringBuilder();
                i++; // skip opening quote
                while (i < input.Length && input[i] != '"')
                {
                    // Only unescape \" → " ; everything else (including \d, \w, \\) is kept as-is
                    if (input[i] == '\\' && i + 1 < input.Length && input[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                    }
                    else
                    {
                        sb.Append(input[i]);
                        i++;
                    }
                }
                if (i >= input.Length)
                    throw new FormatException("Unterminated string literal");
                i++; // skip closing quote
                tokens.Add(new Token(TokenType.StringLit, sb.ToString()));
                continue;
            }

            if (char.IsDigit(input[i]))
            {
                var start = i;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                tokens.Add(new Token(TokenType.NumberLit, input[start..i]));
                continue;
            }

            if (char.IsLetter(input[i]))
            {
                var start = i;
                while (i < input.Length && char.IsLetterOrDigit(input[i])) i++;
                var word = input[start..i];
                var type = word.ToLowerInvariant() switch
                {
                    "and"        => TokenType.And,
                    "or"         => TokenType.Or,
                    "contains"   => TokenType.Contains,
                    "startswith" => TokenType.StartsWith,
                    "endswith"   => TokenType.EndsWith,
                    "oneof"      => TokenType.OneOf,
                    "regex"      => TokenType.Regex,
                    "amount"     => TokenType.Amount,
                    _            => throw new FormatException($"Unknown keyword '{word}'")
                };
                tokens.Add(new Token(type, word));
                continue;
            }

            switch (input[i])
            {
                case '(': tokens.Add(new Token(TokenType.LParen, "(")); i++; break;
                case ')': tokens.Add(new Token(TokenType.RParen, ")")); i++; break;
                case ',': tokens.Add(new Token(TokenType.Comma, ",")); i++; break;
                case '>':
                    if (i + 1 < input.Length && input[i + 1] == '=') { tokens.Add(new Token(TokenType.Gte, ">=")); i += 2; }
                    else { tokens.Add(new Token(TokenType.Gt, ">")); i++; }
                    break;
                case '<':
                    if (i + 1 < input.Length && input[i + 1] == '=') { tokens.Add(new Token(TokenType.Lte, "<=")); i += 2; }
                    else { tokens.Add(new Token(TokenType.Lt, "<")); i++; }
                    break;
                case '=':
                    if (i + 1 < input.Length && input[i + 1] == '=') { tokens.Add(new Token(TokenType.Eq, "==")); i += 2; }
                    else throw new FormatException($"Unexpected character '=' at position {i}");
                    break;
                case '!':
                    if (i + 1 < input.Length && input[i + 1] == '=') { tokens.Add(new Token(TokenType.Neq, "!=")); i += 2; }
                    else throw new FormatException($"Unexpected character '!' at position {i}");
                    break;
                default:
                    throw new FormatException($"Unexpected character '{input[i]}' at position {i}");
            }
        }
        tokens.Add(new Token(TokenType.Eof, ""));
        return tokens;
    }

    private enum TokenType
    {
        And, Or,
        Contains, StartsWith, EndsWith, OneOf, Regex,
        Amount,
        Gt, Lt, Gte, Lte, Eq, Neq,
        LParen, RParen, Comma,
        StringLit, NumberLit,
        Eof
    }

    private readonly record struct Token(TokenType Type, string Value);

    private class TokenReader
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public TokenReader(List<Token> tokens) => _tokens = tokens;

        public Token Current => _tokens[_pos];
        public bool IsEof => _tokens[_pos].Type == TokenType.Eof;

        public void Advance()
        {
            if (!IsEof) _pos++;
        }

        public bool TryConsume(TokenType type)
        {
            if (_tokens[_pos].Type != type) return false;
            _pos++;
            return true;
        }

        public void Consume(TokenType type)
        {
            if (!TryConsume(type))
                throw new FormatException($"Expected {type} but got '{_tokens[_pos].Value}'");
        }

        public string ConsumeString()
        {
            if (_tokens[_pos].Type != TokenType.StringLit)
                throw new FormatException($"Expected string literal but got '{_tokens[_pos].Value}'");
            return _tokens[_pos++].Value;
        }

        public decimal ConsumeNumber()
        {
            if (_tokens[_pos].Type != TokenType.NumberLit)
                throw new FormatException($"Expected number but got '{_tokens[_pos].Value}'");
            return decimal.Parse(_tokens[_pos++].Value);
        }

        public List<string> ConsumeStringList()
        {
            var result = new List<string> { ConsumeString() };
            while (TryConsume(TokenType.Comma))
                result.Add(ConsumeString());
            return result;
        }
    }
}

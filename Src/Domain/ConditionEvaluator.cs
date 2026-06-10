using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProximoTurnoApi.Domain;

public static class ConditionEvaluator
{
    public static bool Evaluate(string? condition, decimal totalOrder, List<int> categories)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        try
        {
            var tokens = Tokenize(condition);
            int index = 0;
            bool result = ParseOr(tokens, ref index, totalOrder, categories);
            if (index < tokens.Count)
            {
                throw new Exception($"Unexpected extra tokens starting with '{tokens[index]}' at position {index}");
            }
            return result;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> Tokenize(string expr)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '(' || c == ')')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            if (c == '>')
            {
                if (i + 1 < expr.Length && expr[i + 1] == '=')
                {
                    tokens.Add(">=");
                    i += 2;
                }
                else
                {
                    tokens.Add(">");
                    i++;
                }
                continue;
            }
            if (c == '<' && i + 1 < expr.Length && char.IsLetter(expr[i + 1]))
            {
                int start = i;
                while (i < expr.Length && expr[i] != '>')
                {
                    i++;
                }
                if (i >= expr.Length || expr[i] != '>')
                {
                    throw new Exception($"Unclosed tag starting at position {start}");
                }
                i++; // consume '>'
                string tag = expr.Substring(start, i - start);
                if (tag == "<>")
                {
                    throw new Exception($"Empty tag at position {start}");
                }
                tokens.Add(tag);
                continue;
            }
            if (c == '<')
            {
                if (i + 1 < expr.Length && expr[i + 1] == '=')
                {
                    tokens.Add("<=");
                    i += 2;
                }
                else
                {
                    tokens.Add("<");
                    i++;
                }
                continue;
            }
            if (c == '=')
            {
                tokens.Add("=");
                i++;
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '.')
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '.'))
                {
                    i++;
                }
                tokens.Add(expr.Substring(start, i - start));
                continue;
            }

            throw new Exception($"Unrecognized character '{c}' at position {i}");
        }
        return tokens;
    }

    private static bool ParseOr(List<string> tokens, ref int index, decimal totalOrder, List<int> categories)
    {
        bool result = ParseAnd(tokens, ref index, totalOrder, categories);

        while (index < tokens.Count && tokens[index].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            bool right = ParseAnd(tokens, ref index, totalOrder, categories);
            result = result || right;
        }

        return result;
    }

    private static bool ParseAnd(List<string> tokens, ref int index, decimal totalOrder, List<int> categories)
    {
        bool result = ParsePrimary(tokens, ref index, totalOrder, categories);

        while (index < tokens.Count && tokens[index].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            bool right = ParsePrimary(tokens, ref index, totalOrder, categories);
            result = result && right;
        }

        return result;
    }

    private static bool ParsePrimary(List<string> tokens, ref int index, decimal totalOrder, List<int> categories)
    {
        if (index >= tokens.Count)
        {
            throw new Exception("Unexpected end of expression");
        }

        if (tokens[index] == "(")
        {
            index++;
            bool result = ParseOr(tokens, ref index, totalOrder, categories);
            if (index >= tokens.Count || tokens[index] != ")")
            {
                throw new Exception("Missing closing parenthesis");
            }
            index++;
            return result;
        }

        string varName = tokens[index++];
        if (index >= tokens.Count)
        {
            throw new Exception("Expected comparison operator");
        }
        string op = tokens[index++];
        if (index >= tokens.Count)
        {
            throw new Exception("Expected constant value");
        }
        string valStr = tokens[index++];

        if (varName.Equals("<TOTAL_ORDER>", StringComparison.OrdinalIgnoreCase))
        {
            decimal val = decimal.Parse(valStr, CultureInfo.InvariantCulture);
            return op switch
            {
                ">" => totalOrder > val,
                ">=" => totalOrder >= val,
                "<" => totalOrder < val,
                "<=" => totalOrder <= val,
                "=" => totalOrder == val,
                _ => throw new Exception($"Invalid operator {op} for total order")
            };
        }
        else if (varName.Equals("<GAME_CATEGORY>", StringComparison.OrdinalIgnoreCase))
        {
            int val = int.Parse(valStr, CultureInfo.InvariantCulture);
            return op switch
            {
                "=" => categories.Contains(val),
                _ => throw new Exception($"Invalid operator {op} for game category")
            };
        }

        throw new Exception($"Unknown variable: {varName}");
    }
}

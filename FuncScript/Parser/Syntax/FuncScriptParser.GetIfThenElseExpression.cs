using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetIfThenElseExpression(ParseContext context, IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            if (index >= exp.Length)
                return ParseBlockResult.NoAdvance(index);

            var keywordNodes = new List<ParseNode>();
            var afterIf = GetKeyWord(context, keywordNodes, index, "if");
            if (afterIf==index)
                return ParseBlockResult.NoAdvance(index);


            if (afterIf < exp.Length && !isCharWhiteSpace(exp[afterIf]))
                return ParseBlockResult.NoAdvance(index);

            var conditionStart = SkipWhitespaceAndComments(exp, afterIf);
            if (conditionStart >= exp.Length)
                return ParseBlockResult.NoAdvance(index);

            if (!TrySplitIfThenElseSegments(exp, conditionStart, out var conditionSegment, out var trueSegment,
                    out var falseStart, out var thenIndex, out var elseIndex))
            {
                return ParseBlockResult.NoAdvance(index);
            }

            // parse condition segment
            var conditionTextLength = conditionSegment.end - conditionSegment.start;
            if (conditionTextLength <= 0)
                return ParseBlockResult.NoAdvance(index);

            var conditionErrors = new List<SyntaxErrorData>();
            var conditionContext = context.CreateChild(exp.Substring(conditionSegment.start, conditionTextLength),
                conditionErrors);
            var conditionResult = Parse(conditionContext);
            var conditionExpr = conditionResult.ExpressionBlock;
            var conditionNode = conditionResult.ParseNode;
            if (conditionExpr == null || conditionErrors.Count > 0)
            {
                AddErrorsWithOffset(errors, conditionErrors, conditionSegment.start);
                return ParseBlockResult.NoAdvance(index);
            }
            OffsetParseNode(conditionNode, conditionSegment.start);
            conditionExpr.Pos = conditionSegment.start;
            conditionExpr.Length = conditionTextLength;

            // parse true segment
            var trueTextLength = trueSegment.end - trueSegment.start;
            if (trueTextLength <= 0)
                return ParseBlockResult.NoAdvance(index);

            var trueErrors = new List<SyntaxErrorData>();
            var trueContext = context.CreateChild(exp.Substring(trueSegment.start, trueTextLength), trueErrors);
            var trueResult = Parse(trueContext);
            var trueExpr = trueResult.ExpressionBlock;
            var trueNode = trueResult.ParseNode;
            if (trueExpr == null || trueErrors.Count > 0)
            {
                AddErrorsWithOffset(errors, trueErrors, trueSegment.start);
                return ParseBlockResult.NoAdvance(index);
            }
            OffsetParseNode(trueNode, trueSegment.start);
            trueExpr.Pos = trueSegment.start;
            trueExpr.Length = trueTextLength;

            // parse false segment using main expression parser to determine remaining length
            var falseErrors = new List<SyntaxErrorData>();
            var falseContext = context.CreateChild(exp, falseErrors);
            var falseResult = GetExpression(falseContext, new List<ParseNode>(), falseStart);
            if (!falseResult.HasProgress(falseStart) || falseResult.ExpressionBlock == null)
            {
                AddErrorsWithOffset(errors, falseErrors, 0);
                return ParseBlockResult.NoAdvance(index);
            }
            if (falseErrors.Count > 0)
            {
                AddErrorsWithOffset(errors, falseErrors, 0);
                return ParseBlockResult.NoAdvance(index);
            }

            var falseExpr = falseResult.ExpressionBlock;
            var falseNode = falseResult.ParseNode;
            var falseConsumed = falseResult.NextIndex;

            falseExpr.Pos = falseStart;
            falseExpr.Length = falseConsumed - falseStart;

            var functionBlock = new ReferenceBlock(exp.Substring(index, afterIf - index))
            {
                Pos = index,
                Length = afterIf - index
            };

            var functionCall = new FunctionCallExpression
            {
                Function = functionBlock,
                Parameters = new[] { conditionExpr, trueExpr, falseExpr },
                Pos = index,
                Length = falseConsumed - index
            };

            var identifierNode = new ParseNode(ParseNodeType.Identifier, index, afterIf - index);

            var paramsStart = conditionExpr.Pos;
            var paramsEnd = falseExpr.Pos + falseExpr.Length;
            var parametersChildren = new List<ParseNode>();
            if (conditionNode != null)
                parametersChildren.Add(conditionNode);

            parametersChildren.Add(new ParseNode(ParseNodeType.KeyWord, thenIndex, 4));

            if (trueNode != null)
                parametersChildren.Add(trueNode);

            parametersChildren.Add(new ParseNode(ParseNodeType.KeyWord, elseIndex, 4));

            if (falseNode != null)
                parametersChildren.Add(falseNode);

            var parametersNode = new ParseNode(ParseNodeType.FunctionParameterList, paramsStart,
                paramsEnd - paramsStart, parametersChildren);

            var functionCallNode = new ParseNode(ParseNodeType.FunctionCall, index, falseConsumed - index,
                new[] { identifierNode, parametersNode });

            siblings?.Add(functionCallNode);

            return new ParseBlockResult(falseConsumed, functionCall, functionCallNode);
        }

        private static bool TrySplitIfThenElseSegments(string exp, int conditionStart,
            out (int start, int end) conditionSegment, out (int start, int end) trueSegment, out int falseStart,
            out int thenIndex, out int elseIndex)
        {
            conditionSegment = default;
            trueSegment = default;
            falseStart = -1;
            thenIndex = -1;
            elseIndex = -1;

            thenIndex = FindKeywordOutsideExpressions(exp, conditionStart, "then");
            if (thenIndex < 0)
                return false;

            var conditionEnd = TrimEndExclusive(exp, conditionStart, thenIndex);
            if (conditionEnd <= conditionStart)
                return false;

            var trueStart = SkipWhitespaceAndComments(exp, thenIndex + 4);
            if (trueStart >= exp.Length)
                return false;

            elseIndex = FindElseOutsideExpressions(exp, trueStart);
            if (elseIndex < 0)
                return false;

            var trueEnd = TrimEndExclusive(exp, trueStart, elseIndex);
            if (trueEnd <= trueStart)
                return false;

            falseStart = SkipWhitespaceAndComments(exp, elseIndex + 4);
            if (falseStart > exp.Length)
                return false;

            conditionSegment = (conditionStart, conditionEnd);
            trueSegment = (trueStart, trueEnd);
            return true;
        }

        private static int FindKeywordOutsideExpressions(string exp, int startIndex, string keyword)
        {
            int depthParen = 0;
            int depthBrace = 0;
            int depthBracket = 0;
            bool inString = false;
            char stringDelimiter = '\0';

            for (var i = startIndex; i < exp.Length; i++)
            {
                var ch = exp[i];

                if (inString)
                {
                    if (ch == '\\' && stringDelimiter == '"' && i + 1 < exp.Length)
                    {
                        i++;
                        continue;
                    }

                    if (ch == stringDelimiter)
                        inString = false;
                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    inString = true;
                    stringDelimiter = ch;
                    continue;
                }

                switch (ch)
                {
                    case '(':
                        depthParen++;
                        continue;
                    case ')':
                        if (depthParen > 0)
                            depthParen--;
                        continue;
                    case '[':
                        depthBracket++;
                        continue;
                    case ']':
                        if (depthBracket > 0)
                            depthBracket--;
                        continue;
                    case '{':
                        depthBrace++;
                        continue;
                    case '}':
                        if (depthBrace > 0)
                            depthBrace--;
                        continue;
                }

                if (ch == '/' && i + 1 < exp.Length && exp[i + 1] == '/')
                {
                    var newline = exp.IndexOf('\n', i + 2);
                    if (newline == -1)
                        return -1;
                    i = newline;
                    continue;
                }

                if (depthParen == 0 && depthBrace == 0 && depthBracket == 0)
                {
                    var matchLength = MatchKeywordLength(exp, i, keyword);
                    if (matchLength > 0)
                        return i;
                    if (matchLength < 0)
                    {
                        i = -matchLength - 1;
                        continue;
                    }
                }
            }

            return -1;
        }

        private static int FindElseOutsideExpressions(string exp, int startIndex)
        {
            int depthParen = 0;
            int depthBrace = 0;
            int depthBracket = 0;
            bool inString = false;
            char stringDelimiter = '\0';
            int nestedIfDepth = 0;

            for (var i = startIndex; i < exp.Length; i++)
            {
                var ch = exp[i];

                if (inString)
                {
                    if (ch == '\\' && stringDelimiter == '"' && i + 1 < exp.Length)
                    {
                        i++;
                        continue;
                    }

                    if (ch == stringDelimiter)
                        inString = false;
                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    inString = true;
                    stringDelimiter = ch;
                    continue;
                }

                switch (ch)
                {
                    case '(':
                        depthParen++;
                        continue;
                    case ')':
                        if (depthParen > 0)
                            depthParen--;
                        continue;
                    case '[':
                        depthBracket++;
                        continue;
                    case ']':
                        if (depthBracket > 0)
                            depthBracket--;
                        continue;
                    case '{':
                        depthBrace++;
                        continue;
                    case '}':
                        if (depthBrace > 0)
                            depthBrace--;
                        continue;
                }

                if (ch == '/' && i + 1 < exp.Length && exp[i + 1] == '/')
                {
                    var newline = exp.IndexOf('\n', i + 2);
                    if (newline == -1)
                        return -1;
                    i = newline;
                    continue;
                }

                if (depthParen == 0 && depthBrace == 0 && depthBracket == 0)
                {
                    var ifLength = MatchKeywordLength(exp, i, "if");
                    if (ifLength > 0)
                    {
                        nestedIfDepth++;
                        i += ifLength;
                        continue;
                    }
                    if (ifLength < 0)
                    {
                        i = -ifLength - 1;
                        continue;
                    }

                    var elseLength = MatchKeywordLength(exp, i, "else");
                    if (elseLength > 0)
                    {
                        if (nestedIfDepth == 0)
                            return i;
                        nestedIfDepth--;
                        i += elseLength;
                        continue;
                    }
                    if (elseLength < 0)
                    {
                        i = -elseLength - 1;
                        continue;
                    }
                }
            }

            return -1;
        }

        private static int TrimEndExclusive(string exp, int start, int end)
        {
            var result = end;
            while (result > start && isCharWhiteSpace(exp[result - 1]))
                result--;
            return result;
        }

        private static int SkipWhitespaceAndComments(string exp, int index)
        {
            var i = index;
            while (i < exp.Length)
            {
                while (i < exp.Length && isCharWhiteSpace(exp[i]))
                    i++;

                if (i < exp.Length - 1 && exp[i] == '/' && exp[i + 1] == '/')
                {
                    i += 2;
                    while (i < exp.Length && exp[i] != '\n')
                        i++;
                    continue;
                }

                break;
            }

            return i;
        }

        private static void OffsetParseNode(ParseNode node, int offset)
        {
            if (node == null)
                return;

            node.Pos += offset;
            if (node.Childs == null)
                return;
            foreach (var child in node.Childs)
            {
                OffsetParseNode(child, offset);
            }
        }

        private static void AddErrorsWithOffset(List<SyntaxErrorData> target, List<SyntaxErrorData> source, int offset)
        {
            foreach (var error in source)
            {
                target.Add(new SyntaxErrorData(error.Loc + offset, error.Length, error.Message));
            }
        }

        private static int MatchKeywordLength(string exp, int index, string keyword)
        {
            var matchIndex = GetLiteralMatch(exp, index, keyword);
            if (matchIndex > index)
            {
                var beforeChar = index > 0 ? exp[index - 1] : '\0';
                var afterChar = matchIndex < exp.Length ? exp[matchIndex] : '\0';
                if ((beforeChar == '\0' || isCharWhiteSpace(beforeChar) || !IsIdentfierOtherChar(beforeChar)) &&
                    (afterChar == '\0' || isCharWhiteSpace(afterChar) || !IsIdentfierOtherChar(afterChar)))
                {
                    return matchIndex - index;
                }
                return -(matchIndex);
            }

            return 0;
        }
    }
}

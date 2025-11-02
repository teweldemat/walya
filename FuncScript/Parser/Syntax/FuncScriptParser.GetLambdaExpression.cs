using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<ExpressionFunction> GetLambdaExpression(ParseContext context,
            IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var currentIndex = GetIdentifierList(exp, index,siblings, out var parameters, out var parametersNode);
            if (currentIndex == index)
                return new ValueParseResult<ExpressionFunction>(index, null, null);

            var arrowIndex = currentIndex;
            if (arrowIndex >= exp.Length - 1)
                return new ValueParseResult<ExpressionFunction>(index, null, null);

            var afterArrow = GetToken(exp, arrowIndex,siblings,ParseNodeType.LambdaArrow, "=>");
            if (afterArrow == arrowIndex)
            {
                errors.Add(new SyntaxErrorData(arrowIndex, 0, "'=>' expected"));
                return new ValueParseResult<ExpressionFunction>(index, null, null);
            }

            currentIndex = afterArrow;

            var childNodes = new List<ParseNode>();
            if (parametersNode != null)
                childNodes.Add(parametersNode);

            var bodyResult = GetExpression(context, childNodes, currentIndex);
            if (!bodyResult.HasProgress(currentIndex) || bodyResult.ExpressionBlock == null)
            {
                errors.Add(new SyntaxErrorData(currentIndex, 0, "defination of lambda expression expected"));
                return new ValueParseResult<ExpressionFunction>(index, null, null);
            }

            currentIndex = bodyResult.NextIndex;

            var function = new ExpressionFunction(parameters.ToArray(), bodyResult.ExpressionBlock);

            var parseNode = new ParseNode(ParseNodeType.LambdaExpression, index, currentIndex - index, childNodes);

            siblings?.Add(parseNode);

            return new ValueParseResult<ExpressionFunction>(currentIndex, function, parseNode);
        }
    }
}

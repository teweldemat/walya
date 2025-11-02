using System;
using System.Collections.Generic;
using FuncScript.Block;
using FuncScript.Model;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetFunctionCallParametersList(ParseContext context, IList<ParseNode> siblings,
            ExpressionBlock function, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var roundResult = GetFunctionCallParametersList(context, siblings, "(", ")", function, index);
            if (roundResult.HasProgress(index))
                return roundResult;

            return GetFunctionCallParametersList(context, siblings, "[", "]", function, index);
        }

        static ParseBlockResult GetFunctionCallParametersList(ParseContext context, IList<ParseNode> siblings,
            string openBrace, string closeBrace, ExpressionBlock function, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var afterOpen = GetToken(exp, index,siblings,ParseNodeType.OpenBrace, openBrace);
            if (afterOpen == index)
                return ParseBlockResult.NoAdvance(index);

            var currentIndex = afterOpen;

            var parameters = new List<ExpressionBlock>();
            var parameterNodes = new List<ParseNode>();

            var parameterIndex = currentIndex;
            var parameterResult = GetExpression(context, parameterNodes, parameterIndex);
            if (parameterResult.HasProgress(parameterIndex) && parameterResult.ExpressionBlock != null)
            {
                parameters.Add(parameterResult.ExpressionBlock);
                currentIndex = parameterResult.NextIndex;

                while (true)
                {
                    var afterComma = GetToken(exp, currentIndex,siblings,ParseNodeType.ListSeparator, ",");
                    if (afterComma == currentIndex)
                        break;

                    var nextParameterIndex = afterComma;
                    var nextParameter = GetExpression(context, parameterNodes, nextParameterIndex);
                    if (!nextParameter.HasProgress(nextParameterIndex) || nextParameter.ExpressionBlock == null)
                    {
                        errors.Add(new SyntaxErrorData(nextParameterIndex, 0, "Parameter for call expected"));
                        return ParseBlockResult.NoAdvance(index);
                    }

                    parameters.Add(nextParameter.ExpressionBlock);
                    currentIndex = nextParameter.NextIndex;
                }
            }

            var afterClose = GetToken(exp, currentIndex,siblings,ParseNodeType.CloseBrance, closeBrace);
            if (afterClose == currentIndex)
            {
                errors.Add(new SyntaxErrorData(currentIndex, 0, $"'{closeBrace}' expected"));
                return ParseBlockResult.NoAdvance(index);
            }

            currentIndex = afterClose;

            var callExpression = new FunctionCallExpression
            {
                Function = function,
                Parameters = parameters.ToArray(),
                Pos = function.Pos,
                Length = currentIndex - function.Pos
            };

            var parseNode = new ParseNode(ParseNodeType.FunctionParameterList, index, currentIndex - index,
                parameterNodes);

            siblings?.Add(parseNode);

            return new ParseBlockResult(currentIndex, callExpression, parseNode);
        }
    }
}

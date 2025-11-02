using System;
using System.Collections.Generic;
using System.Linq;
using FuncScript.Block;
using FuncScript.Model;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetInfixFunctionCall(ParseContext context, IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var operands = new List<ExpressionBlock>();
            var childNodes = new List<ParseNode>();

            var firstOperandResult = GetCallAndMemberAccess(context, childNodes, index);
            if (!firstOperandResult.HasProgress(index) || firstOperandResult.ExpressionBlock == null)
                return ParseBlockResult.NoAdvance(index);

            operands.Add(firstOperandResult.ExpressionBlock);
            var currentIndex = firstOperandResult.NextIndex;

            var iden=GetIdentifier(context,siblings, currentIndex);
            var afterIdentifier = iden.NextIndex;
            if (afterIdentifier == currentIndex)
                return firstOperandResult;

            var function = context.Provider.Get(iden.IdenLower);
            if (function is not IFsFunction infixFunction)
            {
                errors.Add(new SyntaxErrorData(currentIndex, afterIdentifier - currentIndex, "A function expected"));
                return ParseBlockResult.NoAdvance(index);
            }

            if (infixFunction.CallType != CallType.Dual)
                return firstOperandResult;

            currentIndex = afterIdentifier;

            var secondOperandResult = GetCallAndMemberAccess(context, childNodes, currentIndex);
            if (!secondOperandResult.HasProgress(currentIndex) || secondOperandResult.ExpressionBlock == null)
            {
                errors.Add(new SyntaxErrorData(currentIndex, 0, $"Right side operand expected for {iden.Iden}"));
                return ParseBlockResult.NoAdvance(index);
            }

            operands.Add(secondOperandResult.ExpressionBlock);
            currentIndex = secondOperandResult.NextIndex;

            while (true)
            {
                var afterChain = GetToken(exp, currentIndex,siblings,ParseNodeType.ThirdOperandDelimeter, "~");
                if (afterChain == currentIndex)
                    break;

                currentIndex = afterChain;
                var nextOperand = GetCallAndMemberAccess(context, childNodes, currentIndex);
                if (!nextOperand.HasProgress(currentIndex) || nextOperand.ExpressionBlock == null)
                    break;

                operands.Add(nextOperand.ExpressionBlock);
                currentIndex = nextOperand.NextIndex;
            }

            if (operands.Count < 2)
                return ParseBlockResult.NoAdvance(index);

            var firstChild = childNodes.FirstOrDefault();
            var lastChild = childNodes.LastOrDefault();

            var startPos = firstChild?.Pos ?? index;
            var endPos = lastChild != null ? lastChild.Pos + lastChild.Length : startPos;
            if (endPos < startPos)
                endPos = startPos;

            var functionLiteral = new LiteralBlock(function)
            {
                Pos = startPos,
                Length = iden.NextIndex-startPos
            };

            var expression = new FunctionCallExpression
            {
                Function = functionLiteral,
                Parameters = operands.ToArray(),
                Pos = startPos,
                Length = endPos - startPos
            };

            var parseNode = new ParseNode(ParseNodeType.GeneralInfixExpression, startPos, endPos - startPos,
                childNodes);

            siblings?.Add(parseNode);

            return new ParseBlockResult(currentIndex, expression, parseNode);
        }
    }
}

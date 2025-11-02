using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetInfixExpressionSingleLevel(ParseContext context, IList<ParseNode> siblings,
            int level, string[] candidates, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            var exp = context.Expression;
            var errors = context.ErrorsList;

            ParseBlockResult operandResult;
            var currentIndex = index;
            if (level == 0)
                operandResult = GetInfixFunctionCall(context, new List<ParseNode>(), currentIndex);
            else
                operandResult = GetInfixExpressionSingleLevel(context, new List<ParseNode>(), level - 1,
                    s_operatorSymols[level - 1], currentIndex);

            if (!operandResult.HasProgress(currentIndex) || operandResult.ExpressionBlock == null)
                return ParseBlockResult.NoAdvance(index);

            var currentExpression = operandResult.ExpressionBlock;
            var currentNode = operandResult.ParseNode;
            currentIndex = operandResult.NextIndex;

            while (true)
            {
                var operatorResult = GetOperator(context, candidates, currentIndex);
                if (!operatorResult.HasProgress(currentIndex))
                    break;

                var symbol = operatorResult.Value.symbol;
                var currentOperatorNode = operatorResult.ParseNode;
                currentIndex = operatorResult.NextIndex;
                var indexBeforeOperator = currentIndex;

                var operands = new List<ExpressionBlock> { currentExpression };
                var operandNodes = new List<ParseNode>();
                if (currentNode != null)
                    operandNodes.Add(currentNode);

                while (true)
                {
                    if (currentOperatorNode != null)
                        operandNodes.Add(currentOperatorNode);

                    ParseBlockResult nextOperand;
                    if (level == 0)
                        nextOperand = GetInfixFunctionCall(context, operandNodes, currentIndex);
                    else
                        nextOperand = GetInfixExpressionSingleLevel(context, operandNodes, level - 1,
                            s_operatorSymols[level - 1], currentIndex);

                    if (!nextOperand.HasProgress(currentIndex) || nextOperand.ExpressionBlock == null)
                        return ParseBlockResult.NoAdvance(indexBeforeOperator);

                    operands.Add(nextOperand.ExpressionBlock);
                    currentIndex = nextOperand.NextIndex;

                    var repeated = GetToken(exp, currentIndex,siblings,ParseNodeType.Operator, symbol);
                    if (repeated == currentIndex)
                        break;
                    currentIndex = repeated;
                }

                if (operands.Count < 2)
                    return ParseBlockResult.NoAdvance(indexBeforeOperator);

                var startPos = operands[0].Pos;
                var endPos = operands[^1].Pos + operands[^1].Length;

                ExpressionBlock combined;
                if (symbol == "|")
                {
                    if (operands.Count > 2)
                    {
                        errors.Add(new SyntaxErrorData(currentIndex, 0, "Only two parameters expected for | "));
                        return ParseBlockResult.NoAdvance(indexBeforeOperator);
                    }

                    combined = new ListExpression
                    {
                        ValueExpressions = operands.ToArray(),
                        Pos = startPos,
                        Length = endPos - startPos
                    };
                }
                else
                {
                    var function = context.Provider.Get(symbol);
                    var functionLiteral = new LiteralBlock(function);
                    ParseNode operatorLocation = null;
                    foreach (var child in operandNodes)
                    {
                        if (child.NodeType == ParseNodeType.Operator)
                        {
                            operatorLocation = child;
                            break;
                        }
                    }

                    if (operatorLocation != null)
                    {
                        functionLiteral.Pos = operatorLocation.Pos;
                        functionLiteral.Length = operatorLocation.Length;
                    }

                    combined = new FunctionCallExpression
                    {
                        Function = functionLiteral,
                        Parameters = operands.ToArray(),
                        Pos = startPos,
                        Length = endPos - startPos
                    };
                }

                var nodeStart = operandNodes.Count > 0 ? operandNodes[0].Pos : startPos;
                var nodeLength = endPos - nodeStart;
                currentNode = new ParseNode(ParseNodeType.InfixExpression, nodeStart, nodeLength, operandNodes);
                currentExpression = combined;
            }

            if (currentNode != null)
                siblings?.Add(currentNode);

            return new ParseBlockResult(currentIndex, currentExpression, currentNode);
        }
    }
}

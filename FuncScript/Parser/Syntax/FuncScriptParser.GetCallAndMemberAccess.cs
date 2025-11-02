using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetCallAndMemberAccess(ParseContext context, IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var exp = context.Expression;

            var currentIndex = index;
            var unitNodes = new List<ParseNode>();
            var unitResult = GetUnit(context, unitNodes, currentIndex);
            if (!unitResult.HasProgress(currentIndex) || unitResult.ExpressionBlock == null)
                return ParseBlockResult.NoAdvance(index);

            var expression = unitResult.ExpressionBlock;
            var currentNode = unitResult.ParseNode;
            currentIndex = unitResult.NextIndex;

            while (true)
            {
                var callChildren = new List<ParseNode>();
                if (currentNode != null)
                    callChildren.Add(currentNode);
                var callResult = GetFunctionCallParametersList(context, callChildren, expression, currentIndex);
                if (callResult.HasProgress(currentIndex) && callResult.ExpressionBlock != null)
                {
                    expression = callResult.ExpressionBlock;
                    currentNode = new ParseNode(ParseNodeType.FunctionCall, index, callResult.NextIndex - index,
                        callChildren);
                    currentIndex = callResult.NextIndex;
                    continue;
                }

                var memberChildren = new List<ParseNode>();
                if (currentNode != null)
                    memberChildren.Add(currentNode);
                var memberResult = GetMemberAccess(context, memberChildren, expression, currentIndex);
                if (memberResult.HasProgress(currentIndex) && memberResult.ExpressionBlock != null)
                {
                    expression = memberResult.ExpressionBlock;
                    currentNode = new ParseNode(ParseNodeType.MemberAccess, index, memberResult.NextIndex - index,
                        memberChildren);
                    currentIndex = memberResult.NextIndex;
                    continue;
                }

                var selectorChildren = new List<ParseNode>();
                if (currentNode != null)
                    selectorChildren.Add(currentNode);
                var selectorResult = GetKvcExpression(context, selectorChildren, false, currentIndex);
                if (selectorResult.HasProgress(currentIndex) && selectorResult.Value != null)
                {
                    var selector = new SelectorExpression
                    {
                        Source = expression,
                        Selector = selectorResult.Value,
                        Pos = currentIndex,
                        Length = selectorResult.NextIndex - currentIndex
                    };

                    expression = selector;
                    currentNode = new ParseNode(ParseNodeType.Selection, index, selectorResult.NextIndex - index,
                        selectorChildren);
                    currentIndex = selectorResult.NextIndex;
                    continue;
                }

                break;
            }

            if (currentNode != null)
                siblings?.Add(currentNode);

            return new ParseBlockResult(currentIndex, expression, currentNode);
        }
    }
}

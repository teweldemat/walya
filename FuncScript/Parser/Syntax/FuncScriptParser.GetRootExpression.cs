using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResultWithNode GetRootExpression(ParseContext context, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var nodes = new List<ParseNode>();
            var kvcErrors = new List<SyntaxErrorData>();
            var kvcContext = context.CreateChild(context.Expression, kvcErrors);
            var kvcResult = GetKvcExpression(kvcContext, nodes, true, index);
            if (kvcResult.HasProgress(index) && kvcResult.Value != null)
            {
                context.ErrorsList.AddRange(kvcErrors);
                var kvcExpression = kvcResult.Value;
                if (kvcExpression.Length == 0)
                {
                    kvcExpression.Pos = index;
                    kvcExpression.Length = kvcResult.NextIndex - index;
                }
                return new ParseBlockResultWithNode(kvcResult.NextIndex, kvcExpression,new ParseNode(ParseNodeType.RootExpression,index,kvcResult.NextIndex - index,nodes));
            }

            var expressionResult = GetExpression(context, nodes, index);
            if (expressionResult.HasProgress(index) && expressionResult.ExpressionBlock != null)
            {
                var expression = expressionResult.ExpressionBlock;
                if (expression.Length == 0)
                {
                    expression.Pos = index;
                    expression.Length = expressionResult.NextIndex - index;
                }
                return new ParseBlockResultWithNode(expressionResult.NextIndex, expressionResult.ExpressionBlock,new ParseNode(ParseNodeType.RootExpression,index,expressionResult.NextIndex - index,nodes));;
            }

            return new ParseBlockResultWithNode(index,null,null);
        }
    }
}

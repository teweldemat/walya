using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ParseBlockResult GetInfixExpression(ParseContext context, IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var childNodes = new List<ParseNode>();
            var ret= GetInfixExpressionSingleLevel(context, childNodes, s_operatorSymols.Length - 1, s_operatorSymols[^1], index);
            if (ret.HasProgress(index))
            {
                siblings.Add(new ParseNode(ParseNodeType.InfixExpression,index,ret.NextIndex-index,childNodes));
            }

            return ParseResult.NoAdvance(index);
        }
    }
}

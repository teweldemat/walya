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

            return GetInfixExpressionSingleLevel(context, siblings, s_operatorSymols.Length - 1, s_operatorSymols[^1], index);
        }
    }
}

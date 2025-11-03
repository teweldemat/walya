using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int SkipSpace(ParseContext context,IList<ParseNode> siblings,  int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var exp = context.Expression;

            var i = index;
            while (i < exp.Length && isCharWhiteSpace(exp[i]))
            {
                i++;
            }

            if (i > index)
            {
                siblings.Add(new ParseNode(ParseNodeType.WhiteSpace, index, i - index));
            }

            var commentResult = GetCommentBlock(context,siblings, i);
            if (commentResult.HasProgress(i))
            {
                i = commentResult.NextIndex;
            }

            return i;
        }

        
    }
}

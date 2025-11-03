using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetKeyWord(ParseContext context, IList<ParseNode> siblings, int index, string keyword)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (keyword == null)
                throw new ArgumentNullException(nameof(keyword));

            var exp = context.Expression;
            if (index >= exp.Length)
                return index;
            
            var nextIndex = GetToken(context, index, siblings, ParseNodeType.KeyWord, keyword);
            if (nextIndex == index)
                return index;

            if (nextIndex < exp.Length && IsIdentfierOtherChar(exp[nextIndex]))
            {
                return index;
            }

            return nextIndex;
        }
    }
}

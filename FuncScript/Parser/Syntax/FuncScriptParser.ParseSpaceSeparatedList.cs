using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public static IReadOnlyList<string> ParseSpaceSeparatedList(IFsDataProvider provider, string expression,
            List<SyntaxErrorData> errors)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var errorList = errors ?? new List<SyntaxErrorData>();
            var context = new ParseContext(provider, expression, errorList);
            var result = GetSpaceSeparatedStringListExpression(context, new List<ParseNode>(), 0);

            return result.Value;
        }
    }
}

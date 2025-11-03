using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public static ParseBlockResultWithNode Parse(ParseContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return GetRootExpression(context, 0);
        }

        public static ExpressionBlock Parse(IFsDataProvider context, String exp, List<SyntaxErrorData> serrors)
        {
            var errors = serrors ?? new List<SyntaxErrorData>();
            return Parse(context, exp,  errors);
        }


    }
}

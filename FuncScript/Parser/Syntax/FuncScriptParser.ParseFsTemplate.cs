using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public static ExpressionBlock ParseFsTemplate(IFsDataProvider provider, string expression,
            out ParseNode parseNode, List<SyntaxErrorData> errors)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var errorList = errors ?? new List<SyntaxErrorData>();
            var context = new ParseContext(provider, expression, errorList);
            var result = GetFSTemplate(context, new List<ParseNode>(), 0);
            parseNode = result.ParseNode;
            return result.ExpressionBlock;
        }

        public static ExpressionBlock ParseFsTemplate(IFsDataProvider provider, string expression,
            List<SyntaxErrorData> errors)
        {
            return ParseFsTemplate(provider, expression, out _, errors);
        }
    }
}

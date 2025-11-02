using System;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<(string symbol, IFsFunction function)> GetOperator(ParseContext context,
            string[] candidates, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            var exp = context.Expression;
            var currentIndex = SkipSpace(exp, index);

            foreach (var op in candidates)
            {
                var nextIndex = GetLiteralMatch(exp, currentIndex, op);
                if (nextIndex <= currentIndex)
                    continue;

                var function = context.Provider.Get(op) as IFsFunction;
                var parseNode = new ParseNode(ParseNodeType.Operator, currentIndex, nextIndex - currentIndex);
                return new ValueParseResult<(string symbol, IFsFunction function)>(nextIndex, (op, function),
                    parseNode);
            }

            return new ValueParseResult<(string symbol, IFsFunction function)>(index, default, null);
        }
    }
}

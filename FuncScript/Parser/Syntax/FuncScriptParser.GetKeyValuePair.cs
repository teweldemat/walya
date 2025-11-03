using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<KvcExpression.KeyValueExpression> GetKeyValuePair(ParseContext context,
            IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var childNodes = new List<ParseNode>();
            var exp = context.Expression;
            var errors = context.ErrorsList;

            var keyErrors = new List<SyntaxErrorData>();
            var currentIndex = GetSimpleString(context,childNodes, index, out var name, out var nameNode, keyErrors);
            if (currentIndex == index)
            {
                var iden=GetIdentifier(context,siblings, index);
                currentIndex = iden.NextIndex;
                if (currentIndex == index)
                    return new ValueParseResult<KvcExpression.KeyValueExpression>(index, null, null);
            }

            var afterColon = GetToken(context, currentIndex,siblings,ParseNodeType.Colon, ":");
            if (afterColon == currentIndex)
                return new ValueParseResult<KvcExpression.KeyValueExpression>(index, null, null);

            currentIndex = afterColon;


            var valueResult = GetExpression(context, siblings, currentIndex);
            if (!valueResult.HasProgress(currentIndex) || valueResult.ExpressionBlock == null)
            {
                errors.Add(new SyntaxErrorData(currentIndex, 0, "value expression expected"));
                return new ValueParseResult<KvcExpression.KeyValueExpression>(index, null, null);
            }

            currentIndex = valueResult.NextIndex;

            var keyValue = new KvcExpression.KeyValueExpression
            {
                Key = name,
                ValueExpression = valueResult.ExpressionBlock
            };

            var parseNode = new ParseNode(ParseNodeType.KeyValuePair, index, currentIndex - index, childNodes);
            siblings.Add(parseNode);
            return new ValueParseResult<KvcExpression.KeyValueExpression>(currentIndex, keyValue);
        }
    }
}

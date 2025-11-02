using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<KvcExpression.KeyValueExpression> GetKvcItem(ParseContext context,
            IList<ParseNode> siblings, bool nakedKvc, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var exp = context.Expression;

            var keyValueResult = GetKeyValuePair(context, siblings, index);
            if (keyValueResult.HasProgress(index))
                return new ValueParseResult<KvcExpression.KeyValueExpression>(keyValueResult.NextIndex,
                    keyValueResult.Value, keyValueResult.ParseNode);

            var returnResult = GetReturnDefinition(context, siblings, index);
            if (returnResult.HasProgress(index) && returnResult.ExpressionBlock != null)
            {
                var item = new KvcExpression.KeyValueExpression
                {
                    Key = null,
                    ValueExpression = returnResult.ExpressionBlock
                };
                return new ValueParseResult<KvcExpression.KeyValueExpression>(returnResult.NextIndex, item,
                    returnResult.ParseNode);
            }

            if (!nakedKvc)
            {
                var identifierIndex = GetIdentifier(exp, index, out var iden, out var idenLower, out var nodeIden);
                if (identifierIndex > index)
                {
                    var reference = new ReferenceBlock(iden, idenLower, false)
                    {
                        Pos = index,
                        Length = identifierIndex - index
                    };
                    var item = new KvcExpression.KeyValueExpression
                    {
                        Key = iden,
                        KeyLower = idenLower,
                        ValueExpression = reference
                    };
                    if (nodeIden != null)
                        siblings?.Add(nodeIden);
                    return new ValueParseResult<KvcExpression.KeyValueExpression>(identifierIndex, item, nodeIden);
                }

                var stringErrors = new List<SyntaxErrorData>();
                var stringIndex = GetSimpleString(exp, index, out var stringIden, out var nodeStringIden, stringErrors);
                if (stringIndex > index)
                {
                    var reference = new ReferenceBlock(stringIden, stringIden.ToLowerInvariant(), false)
                    {
                        Pos = index,
                        Length = stringIndex - index
                    };
                    var item = new KvcExpression.KeyValueExpression
                    {
                        Key = stringIden,
                        KeyLower = stringIden.ToLowerInvariant(),
                        ValueExpression = reference
                    };
                    if (nodeStringIden != null)
                        siblings?.Add(nodeStringIden);
                    return new ValueParseResult<KvcExpression.KeyValueExpression>(stringIndex, item, nodeStringIden);
                }
            }

            return new ValueParseResult<KvcExpression.KeyValueExpression>(index, null, null);
        }
    }
}

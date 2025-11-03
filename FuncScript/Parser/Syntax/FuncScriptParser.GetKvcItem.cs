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
                    keyValueResult.Value);

            var returnResult = GetReturnDefinition(context, siblings, index);
            if (returnResult.HasProgress(index) && returnResult.ExpressionBlock != null)
            {
                var item = new KvcExpression.KeyValueExpression
                {
                    Key = null,
                    ValueExpression = returnResult.ExpressionBlock
                };
                return new ValueParseResult<KvcExpression.KeyValueExpression>(returnResult.NextIndex, item);
            }

            if (!nakedKvc)
            {
                var iden = GetIdentifier(context, siblings, index);
                var identifierIndex =iden.NextIndex ;
                if (identifierIndex > index)
                {
                    var reference = new ReferenceBlock(iden.Iden, iden.IdenLower, false)
                    {
                        Pos = index,
                        Length = identifierIndex - index
                    };
                    var item = new KvcExpression.KeyValueExpression
                    {
                        Key = iden.Iden,
                        KeyLower = iden.IdenLower,
                        ValueExpression = reference
                    };
                    return new ValueParseResult<KvcExpression.KeyValueExpression>(identifierIndex, item);
                }

                var stringErrors = new List<SyntaxErrorData>();
                var stringIndex = GetSimpleString(context,siblings, index, out var stringIden, out var nodeStringIden, stringErrors);
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
                    return new ValueParseResult<KvcExpression.KeyValueExpression>(stringIndex, item);
                }
            }

            return new ValueParseResult<KvcExpression.KeyValueExpression>(index, null);
        }
    }
}

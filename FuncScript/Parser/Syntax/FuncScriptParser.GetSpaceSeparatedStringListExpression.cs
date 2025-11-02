using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static ValueParseResult<IReadOnlyList<string>> GetSpaceSeparatedStringListExpression(ParseContext context,
            IList<ParseNode> siblings, int index)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var errors = context.ErrorsList;
            var exp = context.Expression;

            var i = index;

            var listItems = new List<string>();
            var nodeListItems = new List<ParseNode>();

            string firstItem;
            ParseNode firstNode;
            var i2 = GetSimpleString(exp, i, out firstItem, out firstNode, errors);
            if (i2 == i)
                i2 = GetSpaceLessString(exp, i, out firstItem, out firstNode);

            if (i2 > i)
            {
                listItems.Add(firstItem);
                if (firstNode != null)
                    nodeListItems.Add(firstNode);
                i = i2;
                while (true)
                {
                    i2 = GetWhitespaceToken(exp,siblings, i);
                    if (i2 == i)
                        break;

                    i = i2;

                    i2 = GetSimpleString(exp, i, out var otherItem, out var otherNode, errors);
                    if (i2 == i)
                        i2 = GetSpaceLessString(exp, i, out otherItem, out otherNode);

                    if (i2 == i)
                        break;

                    listItems.Add(otherItem);
                    if (otherNode != null)
                        nodeListItems.Add(otherNode);
                    i = i2;
                }
            }

            if (listItems.Count == 0)
                return new ValueParseResult<IReadOnlyList<string>>(i, null, null);

            var parseNode = new ParseNode(ParseNodeType.List, index, i - index, nodeListItems);
            siblings?.Add(parseNode);
            return new ValueParseResult<IReadOnlyList<string>>(i, listItems.ToArray(), parseNode);
        }
    }
}

using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static public int GetLiteralMatch(string exp, int index, params string[] keyWord)
        {
            if (exp == null)
            {
                throw new ArgumentNullException(nameof(exp), "The input expression cannot be null.");
            }

            foreach (var k in keyWord)
            {
                bool matchFound = true;
                if (index + k.Length <= exp.Length)
                {
                    for (int i = 0; i < k.Length; i++)
                    {
                        if (char.ToLowerInvariant(exp[index + i]) != char.ToLowerInvariant(k[i]))
                        {
                            matchFound = false;
                            break;
                        }
                    }

                    if (matchFound)
                    {
                        return index + k.Length;
                    }
                }
            }
            return index;
        }

        static int GetToken(ParseContext context,int index, IList<ParseNode> siblings,ParseNodeType nodeType,
             params string[] tokens)
        {
            var node = new ParseNode(nodeType);

            if (tokens == null || tokens.Length == 0)
                throw new ArgumentException("At least one token must be provided.", nameof(tokens));

            var searchIndex = SkipSpace(context,siblings, index);
            var nextIndex = GetLiteralMatch(context.Expression, searchIndex, tokens);
            if (nextIndex == searchIndex)
            {
                return index;
            }

            node.Pos = searchIndex;
            node.Length = nextIndex - searchIndex;
            
            siblings.Add(node);
            return nextIndex;
        }

        static int GetWhitespaceToken(string exp,IList<ParseNode> siblings,  int index)
        {

            var nextIndex = index;
            while (index < exp.Length && isCharWhiteSpace(exp[index]))
            {
                nextIndex++;
            }

            if (nextIndex > index)
            {
                siblings.Add(new ParseNode(ParseNodeType.WhiteSpace,index,nextIndex-index));
            }
            return nextIndex;
        }
    }
}

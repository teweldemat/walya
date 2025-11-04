using System.Collections.Generic;
using FuncScript.Core;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public class IdenResult 
        {
            public IdenResult(int nextIndex, string iden, string idenLower, int startIndex, int length)
            {
                this.NextIndex = nextIndex;
                this.Iden = iden;
                this.IdenLower = idenLower;
                this.StartIndex = startIndex;
                this.Length = length;
            }
            public int NextIndex { get; }

            public string Iden { get; }
            public string IdenLower { get; }

            public int StartIndex { get; }

            public int Length { get; }

        }
        static IdenResult GetIdentifier(ParseContext context,IList<ParseNode> siblings, int index)
        {
            string iden = null;
            string idenLower = null;

            if (index >= context.Expression.Length)
                return new IdenResult(index,null,null,index,0);

            var buffer = CreateNodeBuffer(siblings);
            var currentIndex = SkipSpace(context, buffer, index);
            if (currentIndex >= context.Expression.Length)
                return new IdenResult(index,null,null,index,0);

            if (!IsIdentfierFirstChar(context.Expression[currentIndex]))
                return new IdenResult(index,null,null,index,0);

            var i = currentIndex + 1;
            while (i < context.Expression.Length && IsIdentfierOtherChar(context.Expression[i]))
            {
                i++;
            }

            iden = context.Expression.Substring(currentIndex, i - currentIndex);
            idenLower = iden.ToLower();

            if (s_KeyWords.Contains(idenLower))
            {
                return new IdenResult(index,null,null,index,0);
            }

            var parseNode = new ParseNode(ParseNodeType.Identifier, currentIndex, i - currentIndex);
            buffer.Add(parseNode);
            CommitNodeBuffer(siblings, buffer);
            return new IdenResult(i, iden, idenLower, currentIndex, i - currentIndex);
        }
    }
}

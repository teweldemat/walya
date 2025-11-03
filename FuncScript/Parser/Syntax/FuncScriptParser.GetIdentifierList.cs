namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        static int GetIdentifierList(ParseContext context, int index,IList<ParseNode> siblings, out List<String> idenList, out ParseNode parseNode)
        {
            parseNode = null;
            idenList = null;
            var afterOpen = GetToken(context, index,siblings,ParseNodeType.OpenBrace, "(");
            if (afterOpen == index)
                return index;

            var i = afterOpen;
            idenList = new List<string>();
            var parseNodes = new List<ParseNode>();

            var iden=GetIdentifier(context,siblings, i);
            int i2 = iden.NextIndex;
            if (i2 > i)
            {
                idenList.Add(iden.Iden);
                i = i2;

                while (i < context.Expression.Length)
                {
                    var afterComma = GetToken(context, i,siblings,ParseNodeType.ListSeparator, ",");
                    if (afterComma == i)
                        break;

                    iden = GetIdentifier(context,siblings, afterComma);
                    i2 = iden.NextIndex;
                    if (i2 == afterComma)
                        return index;
                    idenList.Add(iden.Iden);
                    i = i2;
                }
            }

            var afterClose = GetToken(context, i,siblings,ParseNodeType.CloseBrance, ")");
            if (afterClose == i)
                return index;
            parseNode = new ParseNode(ParseNodeType.IdentiferList, index, afterClose - index, parseNodes);
            return afterClose;
        }
    }
}

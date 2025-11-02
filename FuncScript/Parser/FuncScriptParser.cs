using System;
using FuncScript.Block;
using FuncScript.Functions.Math;
using System.Text;
using System.Text.RegularExpressions;
using FuncScript.Functions.Logic;
using FuncScript.Model;
using System.Linq;
namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public enum ParseNodeType
        {
            Comment,
            FunctionParameterList,
            FunctionCall,
            MemberAccess,
            Selection,
            InfixExpression,
            LiteralInteger,
            KeyWord,
            LiteralDouble,
            LiteralLong,
            Identifier,
            IdentiferList,
            Operator,
            LambdaExpression,
            ExpressionInBrace,
            LiteralString,
            StringTemplate,
            KeyValuePair,
            KeyValueCollection,
            List,
            Key,
            Case,
            DataConnection,
            NormalErrorSink,
            SigSequence,
            ErrorKeyWord,
            SignalConnection,
            GeneralInfixExpression,
            PrefixOperatorExpression
        }

        public class SyntaxErrorData
        {
            public int Loc;
            public int Length;
            public String Message;

            public SyntaxErrorData(int loc, int length, string message)
            {
                Loc = loc;
                Message = message;
                Length = length;
            }
        }

        public class ParseNode
        {
            public ParseNodeType NodeType;
            public int Pos;
            public int Length;
            public IList<ParseNode> Childs;

            public ParseNode(ParseNodeType type, int pos, int length)
                : this(type, pos, length, Array.Empty<ParseNode>())
            {
            }

            public ParseNode(ParseNodeType nodeType, int pos, int length, IList<ParseNode> childs)
            {
                NodeType = nodeType;
                Pos = pos;
                Length = length;
                Childs = childs;
            }
        }


        static string[][] s_operatorSymols =
        {
            new[] { "^" },
            new[] { "*", "/", "%" },
            new[] { "+", "-" },
            new[] { ">=", "<=", "!=", ">", "<", "in" },
            new[] { "=", "??", "?!", "?." },
            new[] { "or", "and" },
            new[] { "|" },
            new[] { ">>" },
        };

        private static string[][] s_prefixOp =
            { new string[] { "!", NotFunction.SYMBOL }, new string[] { "-", NegateFunction.SYMBOL } };

        const string KW_RETURN = "return";
        const string KW_CASE = "case";
        const string KW_SWITCH = "switch";
        private const string KW_ERROR = "fault";
        static HashSet<string> s_KeyWords;

        static FuncScriptParser()
        {
            s_KeyWords = new HashSet<string>();
            s_KeyWords.Add(KW_RETURN);
            s_KeyWords.Add(KW_ERROR);
            s_KeyWords.Add(KW_CASE);
            s_KeyWords.Add(KW_SWITCH);
            s_KeyWords.Add(KW_SWITCH);
        }

        static bool isCharWhiteSpace(char ch)
            => ch == ' ' ||
               ch == '\r' ||
               ch == '\t' ||
               ch == '\n';

        static int SkipSpace(String exp, int index)
        {
            int i = index;
            while (index < exp.Length)
            {
                if (isCharWhiteSpace(exp[index]))
                {
                    index++;
                }
                else
                {
                    i = GetCommentBlock(exp, index, out var nodeComment);
                    if (i == index)
                        break;
                    index = i;
                }
            }

            return index;
        }

        static int GetInt(String exp, bool allowNegative, int index, out string intVal, out ParseNode parseNode)
        {
            parseNode = null;
            int i = index;
            if (allowNegative)
                i = GetLiteralMatch(exp, i, "-");

            var i2 = i;
            while (i2 < exp.Length && char.IsDigit(exp[i2]))
                i2++;

            if (i == i2)
            {
                intVal = null;
                return index;
            }

            i = i2;

            intVal = exp.Substring(index, i - index);
            parseNode = new ParseNode(ParseNodeType.LiteralInteger, index, i - index);
            return i;
        }

        static int GetKeyWordLiteral(String exp, int index, out object literal, out ParseNode parseNode)
        {
            parseNode = null;
            var i = GetLiteralMatch(exp, index, "null");
            if (i > index)
            {
                literal = null;
            }
            else if ((i = GetLiteralMatch(exp, index, "true")) > index)
            {
                literal = true;
            }
            else if ((i = GetLiteralMatch(exp, index, "false")) > index)
            {
                literal = false;
            }
            else
            {
                literal = null;
                return index;
            }

            parseNode = new ParseNode(ParseNodeType.KeyWord, index, i - index);
            return i;
        }

        static int GetNumber(String exp, int index, out object number, out ParseNode parseNode,
            List<SyntaxErrorData> serros)
        {
            parseNode = null;
            var hasDecimal = false;
            var hasExp = false;
            var hasLong = false;
            number = null;
            int i = index;
            var i2 = GetInt(exp, true, i, out var intDigits, out var nodeDigits);
            if (i2 == i)
                return index;
            i = i2;

            i2 = GetLiteralMatch(exp, i, ".");
            if (i2 > i)
                hasDecimal = true;
            i = i2;
            if (hasDecimal)
            {
                i = GetInt(exp, false, i, out var decimalDigits, out var nodeDecimlaDigits);
            }

            i2 = GetLiteralMatch(exp, i, "E");
            if (i2 > i)
                hasExp = true;
            i = i2;
            String expDigits = null;
            ParseNode nodeExpDigits;
            if (hasExp)
                i = GetInt(exp, true, i, out expDigits, out nodeExpDigits);

            if (!hasDecimal) //if no decimal we check if there is the 'l' suffix
            {
                i2 = GetLiteralMatch(exp, i, "l");
                if (i2 > i)
                    hasLong = true;
                i = i2;
            }

            if (hasDecimal) //if it has decimal we treat it as 
            {
                if (!double.TryParse(exp.Substring(index, i - index), out var dval))
                {
                    serros.Add(new SyntaxErrorData(index, i - index,
                        $"{exp.Substring(index, i - index)} couldn't be parsed as floating point"));
                    return index; //we don't expect this to happen
                }

                number = dval;
                parseNode = new ParseNode(ParseNodeType.LiteralDouble, index, i - index);
                return i;
            }

            if (hasExp) //it e is included without decimal, zeros are appended to the digits
            {
                if (!int.TryParse(expDigits, out var e) || e < 0)
                {
                    serros.Add(new SyntaxErrorData(index, expDigits == null ? 0 : expDigits.Length,
                        $"Invalid exponentional {expDigits}"));
                    return index;
                }

                var maxLng = long.MaxValue.ToString();
                if (maxLng.Length + 1 < intDigits.Length + e) //check overflow by length
                {
                    serros.Add(new SyntaxErrorData(index, expDigits.Length,
                        $"Exponential {expDigits} is out of range"));
                    return index;
                }

                intDigits = intDigits + new string('0', e);
            }

            long longVal;

            if (hasLong) //if l suffix is found
            {
                if (!long.TryParse(intDigits, out longVal))
                {
                    serros.Add(new SyntaxErrorData(index, expDigits.Length,
                        $"{intDigits} couldn't be parsed to 64bit integer"));
                    return index;
                }

                number = longVal;
                parseNode = new ParseNode(ParseNodeType.LiteralLong, index, i - index);
                return i;
            }

            if (int.TryParse(intDigits, out var intVal)) //try parsing as int
            {
                number = intVal;
                parseNode = new ParseNode(ParseNodeType.LiteralInteger, index, i - index);
                return i;
            }

            if (long.TryParse(intDigits, out longVal)) //try parsing as long
            {
                number = longVal;
                parseNode = new ParseNode(ParseNodeType.LiteralLong, index, i - index);
                return i;
            }

            return index; //all failed
        }

        static bool IsIdentfierFirstChar(char ch)
        {
            return char.IsLetter(ch) || ch == '_';
        }

        static bool IsIdentfierOtherChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        static int GetSpaceLessString(String exp, int index, out String text, out ParseNode parseNode)
        {
            parseNode = null;
            text = null;
            if (index >= exp.Length)
                return index;
            var i = index;

            if (i >= exp.Length || isCharWhiteSpace(exp[i]))
                return index;
            i++;
            while (i < exp.Length && !isCharWhiteSpace(exp[i]))
                i++;

            text = exp.Substring(index, i - index);
            parseNode = new ParseNode(ParseNodeType.Identifier, index, i - index);
            return i;
        }

        static int GetIdentifier(String exp, int index, out String iden, out String idenLower, out ParseNode parseNode)
        {
            parseNode = null;
            iden = null;
            idenLower = null;
            if (index >= exp.Length)
                return index;
            var i = index;
            if (!IsIdentfierFirstChar(exp[i]))
                return index;
            i++;
            while (i < exp.Length && IsIdentfierOtherChar(exp[i]))
            {
                i++;
            }

            iden = exp.Substring(index, i - index);
            idenLower = iden.ToLower();
            if (s_KeyWords.Contains(idenLower))
                return index;
            parseNode = new ParseNode(ParseNodeType.Identifier, index, i - index);
            return i;
        }

        static int GetIdentifierList(String exp, int index, out List<String> idenList, out ParseNode parseNode)
        {
            parseNode = null;
            idenList = null;
            int i = SkipSpace(exp, index);
            //get open brace
            if (i >= exp.Length || exp[i++] != '(')
                return index;

            idenList = new List<string>();
            var parseNodes = new List<ParseNode>();
            //get first identifier
            i = SkipSpace(exp, i);
            int i2 = GetIdentifier(exp, i, out var iden, out var idenLower, out var nodeIden);
            if (i2 > i)
            {
                parseNodes.Add(nodeIden);
                idenList.Add(iden);
                i = i2;

                //get additional identifiers sperated by commas
                i = SkipSpace(exp, i);
                while (i < exp.Length)
                {
                    if (exp[i] != ',')
                        break;
                    i++;
                    i = SkipSpace(exp, i);
                    i2 = GetIdentifier(exp, i, out iden, out idenLower, out nodeIden);
                    if (i2 == i)
                        return index;
                    parseNodes.Add(nodeIden);
                    idenList.Add(iden);
                    i = i2;
                    i = SkipSpace(exp, i);
                }
            }

            //get close brace
            if (i >= exp.Length || exp[i++] != ')')
                return index;
            parseNode = new ParseNode(ParseNodeType.IdentiferList, index, i - index, parseNodes);
            return i;
        }

        static int GetOperator(IFsDataProvider parseContext, string[] candidates, string exp, int index,
            out string matechedOp, out IFsFunction oper,
            out ParseNode parseNode)
        {
            foreach (var op in candidates)
            {
                var i = GetLiteralMatch(exp, index, op);
                if (i <= index) continue;

                var func = parseContext.GetData(op);
//                if (func is not IFsFunction f) 
//                    continue;

                oper = func as IFsFunction;
                parseNode = new ParseNode(ParseNodeType.Operator, index, i - index);
                matechedOp = op;
                return i;
            }

            oper = null;
            parseNode = null;
            matechedOp = null;
            return index;
        }

        static int GetLambdaExpression(IFsDataProvider context, String exp, int index, out ExpressionFunction func,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            func = null;

            var i = GetIdentifierList(exp, index, out var parms, out var nodesParams);
            if (i == index)
                return index;

            i = SkipSpace(exp, i);
            if (i >= exp.Length - 1) // we need two characters
                return index;
            var i2 = GetLiteralMatch(exp, i, "=>");
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "'=>' expected"));
                return index;
            }

            i += 2;
            i = SkipSpace(exp, i);
            var parmsSet = new HashSet<string>();
            foreach (var p in parms)
            {
                parmsSet.Add(p);
            }

            i2 = GetExpression(context, exp, i, out var defination, out var nodeDefination, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "defination of lambda expression expected"));
                return index;
            }

            func = new ExpressionFunction(parms.ToArray(), defination);
            i = i2;
            parseNode = new ParseNode(ParseNodeType.LambdaExpression, index, i - index,
                new[] { nodesParams, nodeDefination });
            return i;
        }

        static int GetExpInParenthesis(IFsDataProvider infixFuncProvider, String exp, int index,
            out ExpressionBlock expression, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            expression = null;
            var i = index;
            i = SkipSpace(exp, i);
            var i2 = GetLiteralMatch(exp, i, "(");
            if (i == i2)
                return index;
            i = i2;

            i = SkipSpace(exp, i);
            i2 = GetExpression(infixFuncProvider, exp, i, out expression, out var nodeExpression, serrors);
            if (i2 == i)
                expression = null;
            else
                i = i2;
            i = SkipSpace(exp, i);
            i2 = GetLiteralMatch(exp, i, ")");
            if (i == i2)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "')' expected"));
                return index;
            }

            i = i2;
            if (expression == null)
                expression = new NullExpressionBlock();
            parseNode = new ParseNode(ParseNodeType.ExpressionInBrace, index, i - index, new[] { nodeExpression });
            return i;
        }

        // Deprecated: This method uses the IndexOf method which can be slow for large strings or when searching for multiple keywords.
        // It is recommended to use the GetLiteralMatch method instead.
        [Obsolete("Use GetLiteralMatch instead.")]
        static int GetLiteralMatch_IndexOf(String exp, int index, params string[] keyWord)
        {
            foreach (var k in keyWord)
            {
                if (exp.IndexOf(k, index, StringComparison.CurrentCultureIgnoreCase) == index)
                {
                    return index + k.Length;
                }
            }

            return index;
        }

        /// <summary>
        /// Checks if any provided keywords are present in the input string, starting from the specified index.
        /// </summary>
        /// <param name="exp">The input string to search for keywords.</param>
        /// <param name="index">The starting index to search for keywords.</param>
        /// <param name="keyWord">Keywords to search for within the input string.</param>
        /// <returns>The index after the end of the matched keyword if found, or the same `index` if no match is found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the input expression is null.</exception>
        /// <remarks>
        /// This method uses a nested for loop for character comparison, providing better performance.
        /// </remarks>
        static public int GetLiteralMatch(string exp, int index, params string[] keyWord)
        {
            return GetLiteralMatch(exp, index, keyWord, out var matched);
        }

        static public int GetLiteralMatch(string exp, int index, string[] keyWord, out string matched)
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
                        matched = k.ToLowerInvariant();
                        return index + k.Length;
                    }
                }
            }

            matched = null;
            return index;
        }


        static int GetReturnDefinition(IFsDataProvider context, String exp, int index, out ExpressionBlock retExp,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            retExp = null;
            var i = GetLiteralMatch(exp, index, KW_RETURN);
            if (i == index)
                return index;
            var nodeReturn = new ParseNode(ParseNodeType.KeyWord, index, i - index);
            i = SkipSpace(exp, i);
            var i2 = GetExpression(context, exp, i, out var expBlock, out var nodeExpBlock, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "return expression expected"));
                return index;
            }

            i = i2;
            retExp = expBlock;
            retExp.Pos = index;
            retExp.Length = i - index;
            parseNode = new ParseNode(ParseNodeType.ExpressionInBrace, index, i - index,
                new[] { nodeReturn, nodeExpBlock });

            return i;
        }

        static int GetSimpleString(string exp, int index, out String str, out ParseNode pareNode,
            List<SyntaxErrorData> serrors)
        {
            var i = GetSimpleString(exp, "\"", index, out str, out pareNode, serrors);
            if (i > index)
                return i;
            return GetSimpleString(exp, "'", index, out str, out pareNode, serrors);
        }

        static int GetSimpleString(string exp, string delimator, int index, out String str, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            str = null;
            var i = GetLiteralMatch(exp, index, delimator);
            if (i == index)
                return index;
            int i2;
            var sb = new StringBuilder();
            while (true)
            {
                i2 = GetLiteralMatch(exp, i, @"\n");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\n');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\t");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\t');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\\");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\\');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\u");
                if (i2 > i)
                {
                    if (i + 6 <= exp.Length) // Checking if there is enough room for 4 hex digits
                    {
                        var unicodeStr = exp.Substring(i + 2, 4);
                        if (int.TryParse(unicodeStr, System.Globalization.NumberStyles.HexNumber, null,
                                out int charValue))
                        {
                            sb.Append((char)charValue);
                            i += 6; // Move past the "\uXXXX"
                            continue;
                        }
                    }
                }

                i2 = GetLiteralMatch(exp, i, $@"\{delimator}");
                if (i2 > i)
                {
                    sb.Append(delimator);
                    i = i2;
                    continue;
                }

                if (i >= exp.Length || GetLiteralMatch(exp, i, delimator) > i)
                    break;
                sb.Append(exp[i]);
                i++;
            }

            i2 = GetLiteralMatch(exp, i, delimator);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"'{delimator}' expected"));
                return index;
            }

            i = i2;
            str = sb.ToString();
            parseNode = new ParseNode(ParseNodeType.LiteralString, index, i - index);
            return i;
        }

        static int GetStringTemplate(IFsDataProvider provider, string exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var i = GetStringTemplate(provider, "\"", exp, index, out prog, out parseNode, serrors);
            if (i > index)
                return i;
            return GetStringTemplate(provider, "'", exp, index, out prog, out parseNode, serrors);
        }

        static int GetStringTemplate(IFsDataProvider provider, String delimator, string exp, int index,
            out ExpressionBlock prog, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            prog = null;
            var parts = new List<ExpressionBlock>();
            var nodeParts = new List<ParseNode>();


            var i = GetLiteralMatch(exp, index, $"f{delimator}");
            if (i == index)
                return index;
            var lastIndex = i;
            var sb = new StringBuilder();
            int i2;
            while (true)
            {
                i2 = GetLiteralMatch(exp, i, @"\\");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\\');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\n");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\n');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\t");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append('\t');
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, $@"\{delimator}");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append(delimator);
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, @"\{");
                if (i2 > i)
                {
                    i = i2;
                    sb.Append("{");
                    continue;
                }

                i2 = GetLiteralMatch(exp, i, "{");
                if (i2 > i)
                {
                    if (sb.Length > 0)
                    {
                        parts.Add(new LiteralBlock(sb.ToString()));
                        nodeParts.Add(new ParseNode(ParseNodeType.LiteralString, lastIndex, i - lastIndex));
                        sb = new StringBuilder();
                    }

                    i = i2;

                    i = SkipSpace(exp, i);
                    i2 = GetExpression(provider, exp, i, out var expr, out var nodeExpr, serrors);
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 0, "expression expected"));
                        return index;
                    }

                    parts.Add(expr);
                    nodeParts.Add(nodeExpr);
                    i = i2;
                    i2 = GetLiteralMatch(exp, i, "}");
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 0, "'}' expected"));
                        return index;
                    }

                    i = i2;
                    lastIndex = i;
                    continue;
                }

                if (i >= exp.Length || GetLiteralMatch(exp, i, delimator) > i)
                    break;
                sb.Append(exp[i]);
                i++;
            }

            if (i > lastIndex)
            {
                if (sb.Length > 0)
                {
                    parts.Add(new LiteralBlock(sb.ToString()));
                    nodeParts.Add(new ParseNode(ParseNodeType.LiteralString, lastIndex, i - lastIndex));
                    sb = new StringBuilder();
                }

                nodeParts.Add(new ParseNode(ParseNodeType.LiteralString, lastIndex, i - lastIndex));
            }

            i2 = GetLiteralMatch(exp, i, delimator);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"'{delimator}' expected"));
                return index;
            }

            i = i2;

            if (parts.Count == 0)
            {
                prog = new LiteralBlock("");
                parseNode = new ParseNode(ParseNodeType.LiteralString, index, i - index);
            }

            if (parts.Count == 1)
            {
                prog = parts[0];
                parseNode = nodeParts[0];
            }
            else
            {
                prog = new FunctionCallExpression
                {
                    Function = new LiteralBlock(provider.GetData("+")),
                    Parameters = parts.ToArray()
                };
                parseNode = new ParseNode(ParseNodeType.StringTemplate, index, i - index, nodeParts);
            }

            return i;
        }

        static int GetKeyValuePair(IFsDataProvider context, string exp, int index,
            out KvcExpression.KeyValueExpression keyValue, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            keyValue = null;
            string name;
            var i = GetSimpleString(exp, index, out name, out var nodeNeme, new List<SyntaxErrorData>());
            if (i == index)
            {
                i = GetIdentifier(exp, index, out name, out var nameLower, out nodeNeme);
                if (i == index)
                    return index;
            }

            i = SkipSpace(exp, i);

            var i2 = GetLiteralMatch(exp, i, ":");
            if (i2 == i)
                return index;

            i = i2;

            i = SkipSpace(exp, i);
            i2 = GetExpression(context, exp, i, out var expBlock, out var nodeExpBlock, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "value expression expected"));
                return index;
            }

            i = i2;
            i = SkipSpace(exp, i);
            keyValue = new KvcExpression.KeyValueExpression
            {
                Key = name,
                ValueExpression = expBlock
            };
            nodeNeme.NodeType = ParseNodeType.Key;
            parseNode = new ParseNode(ParseNodeType.KeyValuePair, index, i - index, new[] { nodeNeme, nodeExpBlock });
            return i;
        }


        static int GetConnectionItem(IFsDataProvider context, string exp, int index,
            out KvcExpression.ConnectionExpression connectionExpression, out ParseNode parseNode,
            ParseNodeType nodeType)
        {
            connectionExpression = null;
            parseNode = null;

            var errors = new List<SyntaxErrorData>();
            var i = GetExpression(context, exp, index, out var sourceExp, out var nodeSourceExp, errors);
            if (i <= index)
            {
                return index; // Failed to parse any expression at all.
            }

            i = SkipSpace(exp, i);
            // Ensure we have a '->' after the source expression
            var i2 = GetLiteralMatch(exp, i, nodeType == ParseNodeType.DataConnection ? ":->" : "->");
            if (i2 == i)
            {
                return index; // No '->' found immediately after the source expression.
            }

            i = SkipSpace(exp, i2);


            i2 = GetExpression(context, exp, i, out var sinkExp, out var nodeSinkExp, errors);
            if (i2 <= i)
            {
                errors.Add(new SyntaxErrorData(i, 0, "Sink expression expected"));
                return index; // Failed to parse the sink expression.
            }

            i = SkipSpace(exp, i2);
            if (sinkExp is ListExpression lst)
            {
                if (lst.ValueExpressions.Length != 2)
                {
                    errors.Add(new SyntaxErrorData(i, 0, "Exactly two items, sink and fault are expected"));
                    return index; // Failed to parse the sink expression.
                }

                connectionExpression = new KvcExpression.ConnectionExpression
                {
                    Source = sourceExp,
                    Sink = lst.ValueExpressions[0],
                    Catch = lst.ValueExpressions[1],
                };
                parseNode = new ParseNode
                (
                    nodeType,
                    index,
                    i - index,
                    new ParseNode[] { nodeSourceExp, nodeSinkExp }
                );
            }
            else
            {
                connectionExpression = new KvcExpression.ConnectionExpression
                {
                    Source = sourceExp,
                    Sink = sinkExp,
                };
                parseNode = new ParseNode
                (
                    ParseNodeType.DataConnection,
                    index,
                    i - index,
                    new ParseNode[] { nodeSourceExp, nodeSinkExp }
                );
            }

            return i;
        }

        static int GetKvcItem(IFsDataProvider context, bool nakedKvc, String exp, int index,
            out KvcExpression.KeyValueExpression item,
            out ParseNode parseNode)
        {
            item = null;
            var e1 = new List<SyntaxErrorData>();
            var i = GetKeyValuePair(context, exp, index, out item, out parseNode, e1);
            if (i > index)
                return i;

            var e2 = new List<SyntaxErrorData>();
            i = GetReturnDefinition(context, exp, index, out var retExp, out var nodeRetExp, e2);
            if (i > index)
            {
                item = new KvcExpression.KeyValueExpression
                {
                    Key = null,
                    ValueExpression = retExp
                };
                parseNode = nodeRetExp;
                return i;
            }

            if (!nakedKvc)
            {
                i = GetIdentifier(exp, index, out var iden, out var idenLower, out var nodeIden);

                if (i > index)
                {
                    item = new KvcExpression.KeyValueExpression
                    {
                        Key = iden,
                        KeyLower = idenLower,
                        ValueExpression = new ReferenceBlock(iden, idenLower, true)
                        {
                            Pos = index,
                            Length = i - index
                        }
                    };
                    parseNode = nodeIden;
                    return i;
                }

                var e3 = new List<SyntaxErrorData>();
                i = GetSimpleString(exp, index, out iden, out nodeIden, e3);
                if (i > index)
                {
                    item = new KvcExpression.KeyValueExpression
                    {
                        Key = iden,
                        KeyLower = idenLower,
                        ValueExpression = new ReferenceBlock(iden, iden.ToLower(), true)
                        {
                            Pos = index,
                            Length = i - index
                        }
                    };
                    parseNode = nodeIden;
                    return i;
                }
            }

            return index;
        }

        static int GetKvcExpression(IFsDataProvider context, bool nakdeMode, String exp, int index,
            out KvcExpression kvcExpr,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            kvcExpr = null;
            var i = SkipSpace(exp, index);
            int i2;
            if (!nakdeMode)
            {
                i2 = GetLiteralMatch(exp, i, "{");
                if (i2 == i)
                    return index;
                i = SkipSpace(exp, i2);
            }

            var kvs = new List<KvcExpression.KeyValueExpression>();
            var dataConnections = new List<KvcExpression.ConnectionExpression>();
            var signalConnections = new List<KvcExpression.ConnectionExpression>();
            var nodeItems = new List<ParseNode>();
            ExpressionBlock retExp = null;
            do
            {
                if (kvs.Count > 0 || retExp != null || dataConnections.Count > 0 || signalConnections.Count > 0)
                {
                    i2 = GetLiteralMatch(exp, i, ",", ";");
                    if (i2 == i)
                        break;
                    i = SkipSpace(exp, i2);
                }

                i2 = GetConnectionItem(context, exp, i, out var dataConItem, out var datanCodeConItem,
                    ParseNodeType.DataConnection);
                if (i2 > i)
                {
                    dataConnections.Add(dataConItem);
                    nodeItems.Add(datanCodeConItem);
                    i = SkipSpace(exp, i2);
                    continue;
                }


                i2 = GetConnectionItem(context, exp, i, out var sigConItem, out var signNodeConItem,
                    ParseNodeType.SignalConnection);
                if (i2 > i)
                {
                    signalConnections.Add(sigConItem);
                    nodeItems.Add(signNodeConItem);
                    i = SkipSpace(exp, i2);
                    continue;
                }


                i2 = GetKvcItem(context, nakdeMode, exp, i, out var otherItem, out var nodeOtherItem);
                if (i2 == i)
                    break;
                if (otherItem.Key == null)
                {
                    if (retExp != null)
                    {
                        serrors.Add(new SyntaxErrorData(nodeOtherItem.Pos, nodeItems.Count,
                            "Duplicate return statement"));
                        return index;
                    }

                    retExp = otherItem.ValueExpression;
                }
                else
                    kvs.Add(otherItem);

                nodeItems.Add(nodeOtherItem);
                i = SkipSpace(exp, i2);
            } while (true);

            if (!nakdeMode)
            {
                i2 = GetLiteralMatch(exp, i, "}");
                if (i2 == i)
                {
                    serrors.Add(new SyntaxErrorData(i, 0, "'}' expected"));
                    return index;
                }

                i = SkipSpace(exp, i2);
            }

            if (nakdeMode)
            {
                if (kvs.Count == 0 && retExp == null && dataConnections.Count == 0 && signalConnections.Count == 0)
                    return index;
            }

            kvcExpr = new KvcExpression();
            var error = kvcExpr.SetKeyValues(kvs.ToArray(), retExp, dataConnections.ToArray(),
                signalConnections.ToArray());
            if (error != null)
            {
                serrors.Add(new SyntaxErrorData(index, i - index, error));
                return index;
            }

            parseNode = new ParseNode(ParseNodeType.KeyValueCollection, index, i - index, nodeItems);
            return i;
        }

        static int GetSpaceSepratedListExpression(IFsDataProvider context, String exp, int index,
            out ListExpression listExpr, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            listExpr = null;
            var i = SkipSpace(exp, index);

            var listItems = new List<ExpressionBlock>();
            var nodeListItems = new List<ParseNode>();
            var i2 = GetExpression(context, exp, i, out var firstItem, out var nodeFirstItem, serrors);
            if (i2 > i)
            {
                listItems.Add(firstItem);
                nodeListItems.Add(nodeFirstItem);
                i = i2;
                do
                {
                    i2 = GetLiteralMatch(exp, i, " ");
                    if (i2 == i)
                        break;
                    i = i2;
                    i = SkipSpace(exp, i);
                    i2 = GetExpression(context, exp, i, out var otherItem, out var nodeOtherItem, serrors);
                    if (i2 == i)
                        break;
                    listItems.Add(otherItem);
                    nodeListItems.Add(nodeOtherItem);
                    i = i2;
                } while (true);
            }

            listExpr = new ListExpression { ValueExpressions = listItems.ToArray() };
            parseNode = new ParseNode(ParseNodeType.List, index, i - index, nodeListItems);
            return i;
        }


        static int GetSpaceSepratedStringListExpression(IFsDataProvider context, String exp, int index,
            out List<string> stringList, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            stringList = null;
            var i = SkipSpace(exp, index);

            var listItems = new List<String>();
            var nodeListItems = new List<ParseNode>();
            String firstItem;
            ParseNode firstNode;

            String otherItem;
            ParseNode otherNode;
            var i2 = GetSimpleString(exp, i, out firstItem, out firstNode, serrors);
            if (i2 == i)
                i2 = GetSpaceLessString(exp, i, out firstItem, out firstNode);
            if (i2 > i)
            {
                listItems.Add(firstItem);
                nodeListItems.Add(firstNode);
                i = i2;
                do
                {
                    i2 = GetLiteralMatch(exp, i, " ");
                    if (i2 == i)
                        break;
                    i = i2;
                    i = SkipSpace(exp, i);
                    i2 = GetSimpleString(exp, i, out otherItem, out otherNode, serrors);
                    if (i2 == i)
                        i2 = GetSpaceLessString(exp, i, out otherItem, out otherNode);

                    if (i2 == i)
                        break;
                    listItems.Add(otherItem);
                    nodeListItems.Add(otherNode);
                    i = i2;
                } while (true);
            }

            stringList = listItems;
            parseNode = new ParseNode(ParseNodeType.List, index, i - index, nodeListItems);
            return i;
        }

        static int GetListExpression(IFsDataProvider context, String exp, int index, out ListExpression listExpr,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            listExpr = null;
            var i = SkipSpace(exp, index);
            var i2 = GetLiteralMatch(exp, i, "[");
            if (i2 == i)
                return index; //we didn't find '['
            i = i2;

            var listItems = new List<ExpressionBlock>();
            var nodeListItems = new List<ParseNode>();
            i = SkipSpace(exp, i);
            i2 = GetExpression(context, exp, i, out var firstItem, out var nodeFirstItem, serrors);
            if (i2 > i)
            {
                listItems.Add(firstItem);
                nodeListItems.Add(nodeFirstItem);
                i = i2;
                do
                {
                    i = SkipSpace(exp, i);
                    i2 = GetLiteralMatch(exp, i, ",");
                    if (i2 == i)
                        break;
                    i = i2;

                    i = SkipSpace(exp, i);
                    i2 = GetExpression(context, exp, i, out var otherItem, out var nodeOtherItem, serrors);
                    if (i2 == i)
                        break;
                    listItems.Add(otherItem);
                    nodeListItems.Add(nodeOtherItem);
                    i = i2;
                } while (true);
            }

            i = SkipSpace(exp, i);
            i2 = GetLiteralMatch(exp, i, "]");
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "']' expected"));
                return index;
            }

            i = i2;
            listExpr = new ListExpression { ValueExpressions = listItems.ToArray() };
            parseNode = new ParseNode(ParseNodeType.List, index, i - index, nodeListItems);
            return i;
        }

        static int GetCommentBlock(String exp, int index, out ParseNode parseNode)
        {
            parseNode = null;
            var i = GetLiteralMatch(exp, index, "//");
            if (i == index)
                return index;
            var i2 = exp.IndexOf("\n", i);
            if (i2 == -1)
                i = exp.Length;
            else
                i = i2 + 1;
            parseNode = new ParseNode(ParseNodeType.Comment, index, i - index);
            return i;
        }

        static int GetCaseExpression(IFsDataProvider context, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            prog = null;
            parseNode = null;
            var i = index;
            var i2 = GetLiteralMatch(exp, i, KW_CASE);
            if (i2 == i)
                return index;
            i = SkipSpace(exp, i2);
            var pars = new List<ExpressionBlock>();
            var childNodes = new List<ParseNode>();
            do
            {
                if (pars.Count == 0)
                {
                    i2 = GetExpression(context, exp, i, out var part1, out var part1Node, serrors);
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 1, "Case condition expected"));
                        return index;
                    }

                    pars.Add(part1);
                    childNodes.Add(part1Node);
                    i = SkipSpace(exp, i2);
                }
                else
                {
                    i2 = GetLiteralMatch(exp, i, ",", ";");
                    if (i2 == i)
                        break;
                    i = SkipSpace(exp, i2);
                    i2 = GetExpression(context, exp, i, out var part1, out var part1Node, serrors);
                    if (i2 == i)
                        break;
                    pars.Add(part1);
                    childNodes.Add(part1Node);
                    i = SkipSpace(exp, i2);
                }

                i2 = GetLiteralMatch(exp, i, ":");
                if (i2 == i)
                {
                    break;
                }

                i = SkipSpace(exp, i2);
                i2 = GetExpression(context, exp, i, out var part2, out var part2Node, serrors);
                if (i2 == i)
                {
                    serrors.Add(new SyntaxErrorData(i, 1, "Case value expected"));
                    return index;
                }

                pars.Add(part2);
                childNodes.Add(part2Node);
                i = SkipSpace(exp, i2);
            } while (true);

            prog = new FunctionCallExpression
            {
                Function = new LiteralBlock(context.GetData(KW_CASE)),
                Pos = index,
                Length = i - index,
                Parameters = pars.ToArray(),
            };
            parseNode = new ParseNode(ParseNodeType.Case, index, i - index);
            parseNode.Childs = childNodes;
            return i;
        }

        static int GetSwitchExpression(IFsDataProvider context, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            prog = null;
            parseNode = null;
            var i = index;
            var i2 = GetLiteralMatch(exp, i, KW_SWITCH);
            if (i2 == i)
                return index;
            i = SkipSpace(exp, i2);
            var pars = new List<ExpressionBlock>();
            var childNodes = new List<ParseNode>();
            i2 = GetExpression(context, exp, i, out var partSelector, out var nodeSelector, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 1, "Switch selector expected"));
                return index;
            }

            pars.Add(partSelector);
            childNodes.Add(nodeSelector);
            i = SkipSpace(exp, i2);
            do
            {
                i2 = GetLiteralMatch(exp, i, ",", ";");
                if (i2 == i)
                    break;
                i = SkipSpace(exp, i2);
                i2 = GetExpression(context, exp, i, out var part1, out var part1Node, serrors);
                if (i2 == i)
                {
                    break;
                }

                i = SkipSpace(exp, i2);
                pars.Add(part1);
                childNodes.Add(part1Node);

                i2 = GetLiteralMatch(exp, i, ":");
                if (i2 == i)
                {
                    break;
                }

                i = SkipSpace(exp, i2);
                i2 = GetExpression(context, exp, i, out var part2, out var part2Node, serrors);
                if (i2 == i)
                {
                    serrors.Add(new SyntaxErrorData(i, 1, "Selector result expected"));
                    return index;
                }

                pars.Add(part2);
                childNodes.Add(part2Node);
                i = SkipSpace(exp, i2);
            } while (true);

            prog = new FunctionCallExpression
            {
                Function = new LiteralBlock(context.GetData(KW_SWITCH)),
                Pos = index,
                Length = i - index,
                Parameters = pars.ToArray(),
            };
            parseNode = new ParseNode(ParseNodeType.Case, index, i - index);
            parseNode.Childs = childNodes;
            return i;
        }

        static int GetMemberAccess(IFsDataProvider context, ExpressionBlock source, String exp, int index,
            out ExpressionBlock prog, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var i2 = GetMemberAccess(context, ".", source, exp, index, out prog, out parseNode, serrors);
            if (i2 == index)
                return GetMemberAccess(context, "?.", source, exp, index, out prog, out parseNode, serrors);
            return i2;
        }

        static int GetMemberAccess(IFsDataProvider context, string oper, ExpressionBlock source, String exp, int index,
            out ExpressionBlock prog, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            prog = null;
            var i = SkipSpace(exp, index);
            var i2 = GetLiteralMatch(exp, i, oper);
            if (i2 == i)
                return index;
            i = i2;
            i = SkipSpace(exp, i);
            i2 = GetIdentifier(exp, i, out var member, out var memberLower, out parseNode);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, "member identifier expected"));
                return index;
            }

            i = i2;
            prog = new FunctionCallExpression
            {
                Function = new LiteralBlock(context.GetData(oper)),
                Parameters = new ExpressionBlock[] { source, new LiteralBlock(member) },
                Pos = source.Pos,
                Length = i - source.Pos
            };
            return i;
        }

        static int GetFunctionCallParametersList(IFsDataProvider context, ExpressionBlock func, String exp, int index,
            out ExpressionBlock prog, out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var i = GetFunctionCallParametersList(context, "(", ")", func, exp, index, out prog, out parseNode,
                serrors);
            if (i == index)
                return GetFunctionCallParametersList(context, "[", "]", func, exp, index, out prog, out parseNode,
                    serrors);
            return i;
        }

        static int GetFunctionCallParametersList(IFsDataProvider context, String openBrance, String closeBrance,
            ExpressionBlock func, String exp, int index, out ExpressionBlock prog, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            prog = null;

            //make sure we have open brace
            var i = SkipSpace(exp, index);
            var i2 = GetLiteralMatch(exp, i, openBrance);
            if (i == i2)
                return index; //we didn't find '('
            i = i2;
            var pars = new List<ExpressionBlock>();
            var parseNodes = new List<ParseNode>();
            //lets get first parameter
            i = SkipSpace(exp, i);
            i2 = GetExpression(context, exp, i, out var par1, out var parseNode1, serrors);
            if (i2 > i)
            {
                i = i2;
                pars.Add(par1);
                parseNodes.Add(parseNode1);
                do
                {
                    i2 = SkipSpace(exp, i);
                    if (i2 >= exp.Length || exp[i2++] != ',') //stop collection of paramters if there is no ','
                        break;
                    i = i2;
                    i = SkipSpace(exp, i);
                    i2 = GetExpression(context, exp, i, out var par2, out var parseNode2, serrors);
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 0, "Parameter for call expected"));
                        return index;
                    }

                    i = i2;
                    pars.Add(par2);
                    parseNodes.Add(parseNode2);
                } while (true);
            }

            i = SkipSpace(exp, i);
            i2 = GetLiteralMatch(exp, i, closeBrance);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"'{closeBrance}' expected"));
                return index;
            }

            i = i2;


            prog = new FunctionCallExpression
            {
                Function = func,
                Parameters = pars.ToArray(),
                Pos = func.Pos,
                Length = i - func.Pos,
            };
            parseNode = new ParseNode(ParseNodeType.FunctionParameterList, index, i - index, parseNodes);
            return i;
        }

        static int GetInfixFunctionCall(IFsDataProvider parseContext, string exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var childNodes = new List<ParseNode>();
            var allOperands = new List<ExpressionBlock>();

            var i = GetCallAndMemberAccess(parseContext, exp, index, out var firstParam, out var firstPramNode,
                serrors);
            if (i == index)
            {
                prog = null;
                parseNode = null;
                return index;
            }

            allOperands.Add(firstParam);
            childNodes.Add(firstPramNode);
            i = SkipSpace(exp, i);

            var i2 = GetIdentifier(exp, i, out var iden, out var idenLower, out var idenNode);
            if (i2 == i)
            {
                prog = null;
                parseNode = null;
                return index;
            }

            childNodes.Add(idenNode);
            i = SkipSpace(exp, i2);

            i2 = GetInfixExpression(parseContext, exp, i, out var secondParam, out var secondParamNode, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"Right side operand expected for {iden}"));
                prog = null;
                parseNode = null;
                return index;
            }

            allOperands.Add(secondParam);
            childNodes.Add(secondParamNode);
            i = SkipSpace(exp, i2);


            while (true)
            {
                i2 = GetLiteralMatch(exp, i, "~");
                if (i2 == i)
                    break;
                i = SkipSpace(exp, i2);
                i2 = GetInfixExpression(parseContext, exp, i, out var moreOperand, out var morePrseNode, serrors);
                if (i2 == i)
                    break;
                i = SkipSpace(exp, i2);

                allOperands.Add(moreOperand);
                childNodes.Add(morePrseNode);
            }


            if (allOperands.Count < 2)
            {
                prog = null;
                parseNode = null;
                return index;
            }

            var func = parseContext.GetData(idenLower);
            var firstChild = childNodes.FirstOrDefault();
            var lastChild = childNodes.LastOrDefault();

            var startPos = firstChild?.Pos ?? index;
            var endPos = lastChild != null ? lastChild.Pos + lastChild.Length : startPos;
            if (endPos < startPos)
                endPos = startPos;
            var spanLength = endPos - startPos;

            ExpressionBlock functionBlock;
            var operatorPos = idenNode?.Pos ?? startPos;
            var operatorLength = idenNode?.Length ?? 0;

            if (func == null)
            {
                functionBlock = new ReferenceBlock(iden, idenLower)
                {
                    Pos = operatorPos,
                    Length = operatorLength
                };
            }
            else
            {
                functionBlock = new LiteralBlock(func)
                {
                    Pos = operatorPos,
                    Length = operatorLength
                };
            }

            prog = new FunctionCallExpression
            {
                Function = functionBlock,
                Parameters = allOperands.ToArray(),
                Pos = startPos,
                Length = spanLength
            };

            parseNode = new ParseNode(ParseNodeType.GeneralInfixExpression, startPos, spanLength, childNodes);

            return i;
        }

        static int GetPrefixOperator(IFsDataProvider parseContext, string exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            int i = 0;
            string oper = null;
            foreach (var op in s_prefixOp)
            {
                i = GetLiteralMatch(exp, index, op[0]);
                if (i > index)
                {
                    oper = op[1];
                    break;
                }
            }

            if (i == index)
            {
                prog = null;
                parseNode = null;
                return index;
            }

            i = SkipSpace(exp, i);
            var func = parseContext.GetData(oper);
            if (func == null)
            {
                serrors.Add(new SyntaxErrorData(index, i - index, $"Prefix operator {oper} not defined"));
                prog = null;
                parseNode = null;
                return index;
            }

            var i2 = GetCallAndMemberAccess(parseContext, exp, i, out var operand, out var operandNode, serrors);
            if (i2 == i)
            {
                serrors.Add(new SyntaxErrorData(i, 0, $"Operant for {oper} expected"));
                prog = null;
                parseNode = null;
                return index;
            }

            i = SkipSpace(exp, i2);

            prog = new FunctionCallExpression
            {
                Function = new LiteralBlock(func),
                Parameters = new[] { operand },
                Pos = index,
                Length = i - index,
            };
            parseNode = new ParseNode(ParseNodeType.PrefixOperatorExpression, index, i - index);
            return i;
        }

        static int GetCallAndMemberAccess(IFsDataProvider parseContext, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            prog = null;
            var i = GetUnit(parseContext, exp, index, out var theUnit, out parseNode, serrors);
            if (i == index)
                return index;

            do
            {
                //lets see if this is part of a function call
                var i2 = GetFunctionCallParametersList(parseContext, theUnit, exp, i, out var funcCall,
                    out var nodeParList, serrors);
                if (i2 > i)
                {
                    i = i2;
                    theUnit = funcCall;
                    parseNode = new ParseNode(ParseNodeType.FunctionCall, index, i - index,
                        new[] { parseNode, nodeParList });
                    continue;
                }

                i2 = GetMemberAccess(parseContext, theUnit, exp, i, out var memberAccess, out var nodeMemberAccess,
                    serrors);
                if (i2 > i)
                {
                    i = i2;
                    theUnit = memberAccess;
                    parseNode = new ParseNode(ParseNodeType.MemberAccess, index, i - index,
                        new[] { parseNode, nodeMemberAccess });
                    continue;
                }

                i2 = GetKvcExpression(parseContext, false, exp, i, out var kvc, out var nodeKvc, serrors);
                if (i2 > i)
                {
                    i = i2;
                    theUnit = new SelectorExpression
                    {
                        Source = theUnit,
                        Selector = kvc,
                        Pos = i,
                        Length = i2 - i
                    };
                    parseNode = new ParseNode(ParseNodeType.Selection, index, i - index, new[] { parseNode, nodeKvc });
                    continue;
                }

                prog = theUnit;
                return i;
            } while (true);
        }

        static int GetUnit(IFsDataProvider provider, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            ParseNode nodeUnit;
            parseNode = null;
            prog = null;
            int i;


            //get string
            i = GetStringTemplate(provider, exp, index, out var template, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = template;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //get string 
            i = GetSimpleString(exp, index, out var str, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = new LiteralBlock(str);
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //get number
            i = GetNumber(exp, index, out var numberVal, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = new LiteralBlock(numberVal);
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //list expression
            i = GetListExpression(provider, exp, index, out var lst, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = lst;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }


            //kvc expression
            i = GetKvcExpression(provider, false, exp, index, out var json, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = json;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            i = GetCaseExpression(provider, exp, i, out var caseExp, out var caseNode, serrors);
            if (i > index)
            {
                parseNode = caseNode;
                prog = caseExp;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            i = GetSwitchExpression(provider, exp, i, out var switchExp, out var switchNode, serrors);
            if (i > index)
            {
                parseNode = switchNode;
                prog = switchExp;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //expression function
            i = GetLambdaExpression(provider, exp, index, out var ef, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = new LiteralBlock(ef);
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }


            //null, true, false
            i = GetKeyWordLiteral(exp, index, out var kw, out nodeUnit);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = new LiteralBlock(kw);
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //get error
            i = GetLiteralMatch(exp, index, KW_ERROR);
            if (i > index)
            {
                parseNode = new ParseNode(ParseNodeType.ErrorKeyWord, index, i - index);
                prog = new LiteralBlock(SignalSinkInfo.ErrorDelegate)
                {
                    Pos = index,
                    Length = i - index,
                };
                return i;
            }

            //get identifier
            i = GetIdentifier(exp, index, out var ident, out var identLower, out nodeUnit);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog = new ReferenceBlock(ident);
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            i = GetExpInParenthesis(provider, exp, index, out prog, out nodeUnit, serrors);
            if (i > index)
            {
                parseNode = nodeUnit;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }

            //get prefix operator
            i = GetPrefixOperator(provider, exp, index, out var prefixOp, out var prefixOpNode, serrors);
            if (i > index)
            {
                prog = prefixOp;
                parseNode = prefixOpNode;
                prog.Pos = index;
                prog.Length = i - index;
                return i;
            }


            return index;
        }

        class InfixFuncElement
        {
            public IFsFunction F;
            public int ParCount;

            public InfixFuncElement(IFsFunction F)
            {
                this.F = F;
                ParCount = 1;
            }
        }

        static int GetExpression(IFsDataProvider parseContext, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var i = GetInfixFunctionCall(parseContext, exp, index, out prog, out parseNode, serrors);
            if (i > index)
                return i;

            i = GetInfixExpression(parseContext, exp, index, out prog, out parseNode, serrors);
            if (i > index)
                return i;

            return index;
        }

        static int GetRootExpression(IFsDataProvider parseContext, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var thisErrors = new List<SyntaxErrorData>();
            var i = GetKvcExpression(parseContext, true, exp, index, out var kvc, out parseNode, thisErrors);
            if (i > index)
            {
                prog = kvc;
                serrors.AddRange(thisErrors);
                return i;
            }

            thisErrors = new List<SyntaxErrorData>();
            i = GetExpression(parseContext, exp, index, out prog, out parseNode, thisErrors);
            if (i > index)
            {
                serrors.AddRange(thisErrors);
                return i;
            }
            return index;
        }

        static int GetInfixExpressionSingleLevel(IFsDataProvider parseContext, int level, string[] candidates,
            String exp, int index,
            out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            prog = null; 
            parseNode = null;
            var i = index;
            while (true)
            {
                int i2;
                IFsFunction oper = null;
                ParseNode operatorNode = null;
                string symbol = null;
                
                if (prog == null) //if we parsing the first operaand
                {
                    //get an infix with one level higher or call expression when we are parsing for highest precidence operators
                    if (level == 0)
                    {
                        i2 = GetCallAndMemberAccess(parseContext, exp, i, out prog, out parseNode, serrors);
                    }
                    else
                    {
                        i2 = GetInfixExpressionSingleLevel(parseContext, level - 1, s_operatorSymols[level - 1], exp, i,
                            out prog, out parseNode,
                            serrors);
                    }

                    if (i2 == i)
                        return i;

                    i = SkipSpace(exp, i2);
                    continue;
                }

                var indexBeforeOperator = i;
                i2 = GetOperator(parseContext, candidates, exp, i, out symbol, out oper,
                    out operatorNode);
                if (i2 == i)
                    break;

                i = SkipSpace(exp, i2);

                var operands = new List<ExpressionBlock>();
                var operandNodes = new List<ParseNode>();
                operands.Add(prog);
                operandNodes.Add(parseNode);
                while (true)
                {
                    ExpressionBlock nextOperand;
                    ParseNode nextOperandNode;
                    if (level == 0)
                        i2 = GetCallAndMemberAccess(parseContext, exp, i, out nextOperand, out nextOperandNode,
                            serrors);
                    else
                        i2 = GetInfixExpressionSingleLevel(parseContext, level - 1, s_operatorSymols[level - 1],
                            exp, i, out nextOperand, out nextOperandNode, serrors);
                    if (i2 == i)
                        return indexBeforeOperator;
                    operands.Add(nextOperand);
                    operandNodes.Add(nextOperandNode);
                    i = SkipSpace(exp, i2);

                    i2 = GetLiteralMatch(exp, i, symbol);
                    if (i2 == i)
                        break;
                    i = SkipSpace(exp, i2);
                }

                if (operands.Count > 1)
                {
                    var func = parseContext.GetData(symbol);
                    if (symbol == "|")
                    {
                        if (operands.Count > 2)
                        {
                            serrors.Add(new SyntaxErrorData(i, 0, "Only two parameters expected for | "));
                            return i;
                        }

                        prog = new ListExpression
                        {
                            ValueExpressions = operands.ToArray(),
                            Pos = prog.Pos,
                            Length = operands[^1].Pos + operands[^1].Length - prog.Length
                        };
                    }
                    else if (func is SigSequenceFunction)
                    {
                        prog = new FunctionCallExpression
                        {
                            Function = new LiteralBlock(func),
                            Parameters = new ExpressionBlock[]
                            {
                                new ListExpression
                                {
                                    ValueExpressions = operands.ToArray()
                                }
                            },
                            Pos = prog.Pos,
                            Length = operands[^1].Pos + operands[^1].Length - prog.Length
                        };
                    }
                    else
                    {
                        prog = new FunctionCallExpression
                        {
                            Function = new LiteralBlock(func),
                            Parameters = operands.ToArray(),
                            Pos = prog.Pos,
                            Length = operands[^1].Pos + operands[^1].Length - prog.Length
                        };
                    }

                    ParseNode firstOperandNode = null;
                    ParseNode lastOperandNode = null;
                    var childNodes = new List<ParseNode>(operandNodes.Count);
                    foreach (var operandNode in operandNodes)
                    {
                        if (operandNode == null)
                        {
                            continue;
                        }

                        if (firstOperandNode == null)
                        {
                            firstOperandNode = operandNode;
                        }

                        childNodes.Add(operandNode);
                        lastOperandNode = operandNode;
                    }

                    if (firstOperandNode != null && lastOperandNode != null)
                    {
                        var startPos = firstOperandNode.Pos;
                        var endPos = lastOperandNode.Pos + lastOperandNode.Length;
                        var length = Math.Max(0, endPos - startPos);
                        parseNode = new ParseNode(ParseNodeType.InfixExpression, startPos, length, childNodes);
                    }
                }
            }
            return i;
        }

        static int GetInfixExpression(IFsDataProvider parseContext, String exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            var i = GetInfixExpressionSingleLevel(parseContext, s_operatorSymols.Length - 1, s_operatorSymols[^1], exp,
                index, out prog,
                out parseNode, serrors);
            return i;
        }

        public static int GetFSTemplate(IFsDataProvider provider, string exp, int index, out ExpressionBlock prog,
            out ParseNode parseNode, List<SyntaxErrorData> serrors)
        {
            parseNode = null;
            prog = null;
            var parts = new List<ExpressionBlock>();
            var nodeParts = new List<ParseNode>();

            var i = index;
            var sb = new StringBuilder();
            int i2;
            var lastIndex = i;
            while (true)
            {
                i2 = GetLiteralMatch(exp, i, "$${");
                if (i2 > i)
                {
                    sb.Append("${");
                    i = i2;
                }

                i2 = GetLiteralMatch(exp, i, "${");
                if (i2 > i)
                {
                    if (sb.Length > 0)
                    {
                        parts.Add(new LiteralBlock(sb.ToString()));
                        nodeParts.Add(new ParseNode(ParseNodeType.LiteralString, lastIndex, i - lastIndex));
                        sb = new StringBuilder();
                    }

                    i = i2;

                    i = SkipSpace(exp, i);
                    i2 = GetExpression(provider, exp, i, out var expr, out var nodeExpr, serrors);
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 0, "expression expected"));
                        return index;
                    }

                    i = SkipSpace(exp, i);

                    parts.Add(expr);
                    nodeParts.Add(nodeExpr);
                    i = i2;

                    i2 = GetLiteralMatch(exp, i, "}");
                    if (i2 == i)
                    {
                        serrors.Add(new SyntaxErrorData(i, 0, "'}' expected"));
                        return index;
                    }

                    i = i2;
                    lastIndex = i;
                    if (i < exp.Length)
                        continue;
                    else
                        break;
                }

                sb.Append(exp[i]);
                i++;
                if (i == exp.Length)
                    break;
            }

            if (sb.Length > 0)
            {
                parts.Add(new LiteralBlock(sb.ToString()));
                nodeParts.Add(new ParseNode(ParseNodeType.LiteralString, lastIndex, i - lastIndex));
            }

            if (parts.Count == 0)
            {
                prog = new LiteralBlock("");
                parseNode = new ParseNode(ParseNodeType.LiteralString, index, i - index);
            }

            if (parts.Count == 1)
            {
                prog = parts[0];
                parseNode = nodeParts[0];
            }
            else
            {
                prog = new FunctionCallExpression
                {
                    Function = new LiteralBlock(provider.GetData(TemplateMergeMergeFunction.SYMBOL)),
                    Parameters = parts.ToArray()
                };
                parseNode = new ParseNode(ParseNodeType.StringTemplate, index, i - index, nodeParts);
            }

            return i;
        }

        public static ExpressionBlock Parse(IFsDataProvider context, String exp, List<SyntaxErrorData> serrors)
        {
            return Parse(context, exp, out var node, serrors);
        }

        public static List<string> ParseSpaceSepratedList(IFsDataProvider context, String exp,
            List<SyntaxErrorData> serrors)
        {
            var i = GetSpaceSepratedStringListExpression(context, exp, 0, out var prog, out var parseNode, serrors);
            return prog;
        }


        public static ExpressionBlock Parse(IFsDataProvider context, String exp, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            var i = GetRootExpression(context, exp, 0, out var prog, out parseNode, serrors);
            return prog;
        }

        public static ExpressionBlock ParseFsTemplate(IFsDataProvider context, String exp, out ParseNode parseNode,
            List<SyntaxErrorData> serrors)
        {
            var i = GetFSTemplate(context, exp, 0, out var block, out parseNode, serrors);
            return block;
        }

        public static ExpressionBlock ParseFsTemplate(IFsDataProvider context, String exp,
            List<SyntaxErrorData> serrors)
        {
            return ParseFsTemplate(context, exp, out var node, serrors);
        }
    }
}

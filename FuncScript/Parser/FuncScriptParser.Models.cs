using System;
using System.Collections.Generic;
using FuncScript.Block;

namespace FuncScript.Core
{
    public partial class FuncScriptParser
    {
        public class ParseContext
        {
            public ParseContext(IFsDataProvider provider, string expression, List<SyntaxErrorData> errorsList)
            {
                Provider = provider;
                Expression = expression ?? throw new ArgumentNullException(nameof(expression));
                ErrorsList = errorsList ?? throw new ArgumentNullException(nameof(errorsList));
            }

            public IFsDataProvider Provider { get; }

            public string Expression { get; }

            public List<SyntaxErrorData> ErrorsList { get; }

            public ParseContext CreateChild(string expression, List<SyntaxErrorData> errorsList)
            {
                return new ParseContext(Provider, expression, errorsList ?? new List<SyntaxErrorData>());
            }
        }

        public class ParseResult
        {
            public ParseResult(int nextIndex, ParseNode parseNode)
            {
                NextIndex = nextIndex;
                ParseNode = parseNode;
            }


            public int NextIndex { get; }

            public ParseNode ParseNode { get; }

            public static ParseBlockResult NoAdvance(int index) => new ParseBlockResult(index, null, null);

            public bool HasProgress(int currentIndex) => NextIndex > currentIndex;
        }

        public class ParseBlockResult:ParseResult
        {
            public ParseBlockResult(int nextIndex, ExpressionBlock expressionBlock, ParseNode parseNode)
            :base(nextIndex,parseNode)
            {
                ExpressionBlock = expressionBlock;
            }


            public ExpressionBlock ExpressionBlock { get; }

        }

        public class ValueParseResult<T> : ParseResult
        {
            public ValueParseResult(int nextIndex, T value, ParseNode parseNode)
                : base(nextIndex, parseNode)
            {
                Value = value;
            }

            public ValueParseResult(int nextIndex, T value, ExpressionBlock expressionBlock, ParseNode parseNode)
                : base(nextIndex, parseNode)
            {
                Value = value;
            }

            public T Value { get; }
        }

        public class CommentParseResult : ValueParseResult<string>
        {
            public CommentParseResult(int nextIndex, string text, ParseNode parseNode)
                : base(nextIndex, text, parseNode)
            {
            }

            public string Text => Value;
        }
    }
}

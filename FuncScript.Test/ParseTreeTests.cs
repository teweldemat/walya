using global::FuncScript.Block;
using global::FuncScript.Core;
using global::FuncScript.Model;
using FuncScriptParser = global::FuncScript.Core.FuncScriptParser;
using ParseNode = global::FuncScript.Core.FuncScriptParser.ParseNode;
using ParseContext = global::FuncScript.Core.FuncScriptParser.ParseContext;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using static global::FuncScript.Core.FuncScriptParser;

namespace FuncScript.Test
{
    internal class ParseTreeTests
    {
        static ParseBlockResultWithNode ParseExpression(IFsDataProvider provider, string expression,
            List<FuncScriptParser.SyntaxErrorData> errors)
        {
            var context = new ParseContext(provider, expression, errors);
            return FuncScriptParser.Parse(context);
        }

        static ParseNode Flatten(ParseNode node)
        {
            if(node.Childs.Count==1)
            {
                return node.Childs[0];
            }
            
            if(node.Childs.Count>1)
            {
                for (int i = 0; i < node.Childs.Count; i++)
                    node.Childs[i] = Flatten(node.Childs[i]);
            }
            return node;
        }
        [Test]
        public void PasreKvcTest()
        {
            var g = new DefaultFsDataProvider();
            var expText = "{a,b,c}";
            var list = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(g, expText, list);
            Assert.AreEqual(expText.Length, result.NextIndex);
            Assert.IsNotNull(result.ExpressionBlock);
            Assert.IsNotNull(result.ParseNode);
            var node = Flatten(result.ParseNode);
            Assert.AreEqual(Tuple.Create(ParseNodeType.KeyValueCollection,0,expText.Length), Tuple.Create(node.NodeType,node.Pos,node.Length));

        }

        [Test]
        public void ParseSimpleInfixExpressionPositions()
        {
            var provider = new DefaultFsDataProvider();
            var expText = "1+2";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expText, errors);
            var block = result.ExpressionBlock;
            var node = result.ParseNode;

            Assert.IsNotNull(block, "Expression should be parsed into a block instance");
            Assert.IsNotNull(node, "Parse node is expected for valid expression");
            Assert.IsEmpty(errors, "No syntax errors should be reported for a simple expression");
            Assert.AreEqual(expText.Length, result.NextIndex, "Parser should consume entire expression");

            //check parse node tree
            Assert.AreEqual(ParseNodeType.InfixExpression, node.NodeType, "Root node should represent an infix expression");
            Assert.AreEqual(0, node.Pos, "Infix expression should start at the beginning of the expression");
            Assert.AreEqual(expText.Length, node.Length, "Infix expression span should cover the full expression text");

            Assert.AreEqual(3, node.Childs.Count);

            var left = node.Childs[0];
            Assert.AreEqual(ParseNodeType.LiteralInteger, left.NodeType);
            Assert.AreEqual(0, left.Pos);
            Assert.AreEqual(1, left.Length);

            var op = node.Childs[1];
            Assert.AreEqual(ParseNodeType.Operator, op.NodeType);
            Assert.AreEqual(1, op.Pos);
            Assert.AreEqual(1, op.Length);

            var right = node.Childs[2];
            Assert.AreEqual(ParseNodeType.LiteralInteger, right.NodeType);
            Assert.AreEqual(2, right.Pos);
            Assert.AreEqual(1, right.Length);

            //check evaluation tree
            Assert.That(block, Is.TypeOf<FunctionCallExpression>());
            Assert.That(block.CodeLocation.Position, Is.EqualTo(0));
            Assert.That(block.CodeLocation.Length, Is.EqualTo(3));

            var function = (FunctionCallExpression)block;
            Assert.That(function.Function.CodeLocation.Position, Is.EqualTo(1));
            Assert.That(function.Function.CodeLocation.Length, Is.EqualTo(1));

            var leftExp = function.Parameters[0];
            Assert.That(leftExp.CodeLocation.Position, Is.EqualTo(0));
            Assert.That(leftExp.CodeLocation.Length, Is.EqualTo(1));

            var rightExp = function.Parameters[1];
            Assert.That(rightExp.CodeLocation.Position, Is.EqualTo(2));
            Assert.That(rightExp.CodeLocation.Length, Is.EqualTo(1));


        }

        [Test]
        public void IfThenElseParseTreeIncludesKeywords()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "if 1=1 then 3 else 4";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var block = result.ExpressionBlock;
            var node = result.ParseNode;

            Assert.IsNotNull(block);
            Assert.IsNotNull(node);
            Assert.IsEmpty(errors);
            Assert.AreEqual(expression.Length, result.NextIndex);

            Assert.AreEqual(ParseNodeType.FunctionCall, node.NodeType);
            Assert.AreEqual(0, node.Pos);
            Assert.AreEqual(expression.Length, node.Length);
            Assert.That(node.Childs, Has.Count.EqualTo(2));

            var identifierNode = node.Childs[0];
            Assert.AreEqual(ParseNodeType.Identifier, identifierNode.NodeType);
            Assert.AreEqual(0, identifierNode.Pos);
            Assert.AreEqual(2, identifierNode.Length);

            var parameterNode = node.Childs[1];
            Assert.AreEqual(ParseNodeType.FunctionParameterList, parameterNode.NodeType);

            var keywordNodes = parameterNode.Childs
                .Where(ch => ch.NodeType == ParseNodeType.KeyWord)
                .ToList();

            Assert.That(keywordNodes, Has.Count.EqualTo(2));

            var thenNode = keywordNodes.Single(ch => expression.Substring(ch.Pos, ch.Length)
                .Equals("then", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(7, thenNode.Pos);
            Assert.AreEqual(4, thenNode.Length);

            var elseNode = keywordNodes.Single(ch => expression.Substring(ch.Pos, ch.Length)
                .Equals("else", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(14, elseNode.Pos);
            Assert.AreEqual(4, elseNode.Length);

            Assert.That(block, Is.TypeOf<FunctionCallExpression>());
        }

        void AssertTreeSpanConsitency(ParseNode node)
        {
            var left = node.Pos;
            foreach (var ch in node.Childs)
            {
                if (ch.Pos < left)
                    throw new AssertionException("Node ordering and position inconsitence.");
                AssertTreeSpanConsitency(ch);
                left = ch.Pos + ch.Length;
                if(left>node.Pos+node.Length)
                    throw new AssertionException($"Child node {node.NodeType} span overflow the span of its parent {node.NodeType}");
            }
        }
        [Test]
        public void TestColoring()
        {
            var provider = new DefaultFsDataProvider();
            var expText = "1+sin(45)";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expText, errors);
            var block = result.ExpressionBlock;
            var node = result.ParseNode;
            Assert.IsNotNull(block);
            Assert.IsNotNull(node);
            Assert.IsEmpty(errors);
            Assert.AreEqual(expText.Length, result.NextIndex);
            AssertTreeSpanConsitency(node);
            var color = FuncScriptRuntime.ColorParseTree(node).ToArray();
            Assert.That(color, Has.Length.EqualTo(6));

            var p = 0;
            var i = 0;
            var c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.LiteralInteger));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p++;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.Operator));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p += 1;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.Identifier));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(3));
            p += 3;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.FunctionParameterList));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p++;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.LiteralInteger));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(2));
            p += 2;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.FunctionParameterList));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p++;
            i++;
        }

        [Test]
        public void TestColoring2()
        {
            var provider = new DefaultFsDataProvider();
            var expText = "(x)=>45";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expText, errors);
            var block = result.ExpressionBlock;
            var node = result.ParseNode;
            Assert.IsNotNull(block);
            Assert.IsNotNull(node);
            Assert.IsEmpty(errors);
            Assert.AreEqual(expText.Length, result.NextIndex);
            AssertTreeSpanConsitency(node);
            var color = FuncScriptRuntime.ColorParseTree(node).ToArray();
            Assert.That(color, Has.Length.EqualTo(5));

            var p = 0;
            var i = 0;
            var c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.IdentiferList));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p++;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.Identifier));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p += 1;
            i++;
            
            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.IdentiferList));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(1));
            p += 1;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.LambdaExpression));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(2));
            p+=2;
            i++;

            c = color[i];
            Assert.That(c.NodeType, Is.EqualTo(ParseNodeType.LiteralInteger));
            Assert.That(c.Pos, Is.EqualTo(p));
            Assert.That(c.Length, Is.EqualTo(2));
            p += 2;
            i++;
        
        }

        void AssertRootNode(ParseNode rootNode,string expression)
        {
            Assert.That(rootNode.NodeType, Is.EqualTo(ParseNodeType.RootExpression));
            Assert.That(rootNode.Pos, Is.EqualTo(0));
            Assert.That(rootNode.Length, Is.EqualTo(expression.Length), "Root node length should cover the entire expression");

        }
        [Test]
        public void CaseParseNodeLengthMatchesExpressionSpan()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "case true: 1";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var node = result.ParseNode;

            Assert.That(errors, Is.Empty, "Parsing a simple case expression should not report errors");
            Assert.That(node, Is.Not.Null, "Parser should produce a parse node for a valid case expression");
            
            AssertRootNode(node,expression);
            AssertTreeSpanConsitency(node);
            Assert.That(node.Childs.Count, Is.EqualTo(1));
            node = node.Childs[0];
            
            Assert.That(node.NodeType, Is.EqualTo(ParseNodeType.Case));
            Assert.That(node.Pos, Is.EqualTo(0));
            Assert.That(node.Length, Is.EqualTo(expression.Length), "Case node length should cover the entire expression");
            Assert.That(result.NextIndex, Is.EqualTo(expression.Length));
        }

        [Test]
        public void SwitchParseNodeLengthMatchesExpressionSpan()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "switch 1, 1: \"one\"";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var node = result.ParseNode;

            Assert.That(errors, Is.Empty, "Parsing a switch expression should not report errors");
            Assert.That(node, Is.Not.Null, "Parser should produce a parse node for a valid switch expression");
            Assert.That(node.NodeType, Is.EqualTo(ParseNodeType.Case));
            Assert.That(node.Pos, Is.EqualTo(0));
            Assert.That(node.Length, Is.EqualTo(expression.Length), "Switch node length should cover the entire expression");
            Assert.That(result.NextIndex, Is.EqualTo(expression.Length));
        }

        [Test]
        public void GeneralInfixParseNodeUsesChildSpan()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "  [\"a\",\"b\"] join \",\"";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var node = result.ParseNode;

            Assert.That(errors, Is.Empty, "General infix parsing should succeed");
            Assert.That(node, Is.Not.Null);
            
            AssertRootNode(node,expression);
            AssertTreeSpanConsitency(node);
            Assert.That(node.Childs.Count, Is.EqualTo(1));
            node = node.Childs[0];

            Assert.That(node.NodeType, Is.EqualTo(ParseNodeType.GeneralInfixExpression));

            var expectedStart = expression.IndexOf('[');
            Assert.That(node.Pos, Is.EqualTo(expectedStart));

            var expectedLength = expression.Length - expectedStart;
            Assert.That(node.Length, Is.EqualTo(expectedLength), "General infix node should span its operand range");
            Assert.That(result.NextIndex, Is.EqualTo(expression.Length));
        }

        [Test]
        public void GeneralInfixExpressionBlockLengthMatchesParseSpan()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "  [\"a\",\"b\"] join \",\"";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var block = result.ExpressionBlock;

            Assert.That(errors, Is.Empty, "General infix parsing should succeed");
            Assert.That(block, Is.TypeOf<FunctionCallExpression>());

            var expectedStart = expression.IndexOf('[');
            Assert.That(block.CodeLocation.Position, Is.EqualTo(expectedStart));

            var expectedLength = expression.Length - expectedStart;
            Assert.That(block.CodeLocation.Length, Is.EqualTo(expectedLength), "Function call block should cover the entire infix expression");
            Assert.That(result.NextIndex, Is.EqualTo(expression.Length));
        }

        [Test]
        public void GeneralInfixFunctionLiteralCapturesIdentifierSpan()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "  [\"a\",\"b\"] join \",\"";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var block = result.ExpressionBlock;

            Assert.That(errors, Is.Empty, "General infix parsing should succeed");
            var call = block as FunctionCallExpression;
            Assert.IsNotNull(call, "General infix parsing should produce a function call expression");

            var functionIdentifierIndex = expression.IndexOf("join", StringComparison.Ordinal);
            Assert.That(call.Function.CodeLocation.Position, Is.EqualTo(functionIdentifierIndex));
            Assert.That(call.Function.CodeLocation.Length, Is.EqualTo("join".Length));
            Assert.That(result.NextIndex, Is.EqualTo(expression.Length));
        }
        
        [Test]
        public void WhiteSpace1()
        {
            var provider = new DefaultFsDataProvider();
            var expression = "  x";
            var errors = new List<FuncScriptParser.SyntaxErrorData>();
            var result = ParseExpression(provider, expression, errors);
            var block = result.ExpressionBlock;

            Assert.That(errors, Is.Empty);
            var refBlock = block as ReferenceBlock;
            Assert.IsNotNull(refBlock, "General infix parsing should produce a function call expression");

            Assert.That(result.ParseNode.NodeType,Is.EqualTo(ParseNodeType.RootExpression));
            Assert.That(result.ParseNode.Childs,Has.Count.EqualTo(2));
            var i = 0;
            var l = 1;
            var node = result.ParseNode.Childs[0];
            Assert.That(node.NodeType,Is.EqualTo(ParseNodeType.WhiteSpace));
            Assert.That(node.Pos,Is.EqualTo(i));
            Assert.That(node.Length,Is.EqualTo(l));
            i += l;
            
            l = 1;
            node = result.ParseNode.Childs[0];
            Assert.That(node.NodeType,Is.EqualTo(ParseNodeType.Identifier));
            Assert.That(node.Pos,Is.EqualTo(i));
            Assert.That(node.Length,Is.EqualTo(l));
        }
    }
}

const { expect } = require('chai');
const {
  evaluate,
  valueOf,
  typeOf,
  FSDataType,
  FuncScriptParser
} = require('@tewelde/funcscript');
const { ParseNodeType } = require('@tewelde/funcscript/parser');
const {
  assertRootNode,
  assertTreeSpanConsistency,
  assertNodeSequence
} = require('../helpers/parse-tree-assertions');
const { parseExpression, DefaultFsDataProvider } = require('../helpers/parser');

// Mirrors the structure of FuncScript.Test/ParseTreeTests.cs in C#.
describe('ParseTreeTests', () => {
  describe('health check test', () => {
    it('health check test', () => {
      const expression = '1+2';
      const provider = new DefaultFsDataProvider();
      const { block, parseNode, errors, nextIndex } = parseExpression(expression, provider);

      expect(errors, 'parsing should not produce errors').to.be.empty;
      expect(block, 'parser should produce an expression block').to.exist;
      expect(parseNode, 'parser should produce a parse node').to.exist;
      expect(nextIndex).to.equal(expression.length);

      assertRootNode(parseNode, expression);
      assertTreeSpanConsistency(parseNode);
      assertNodeSequence(parseNode.Childs, 0, [ParseNodeType.InfixExpression, expression.length]);

      const result = evaluate(expression, provider);
      expect(typeOf(result)).to.equal(FSDataType.Integer);
      expect(valueOf(result)).to.equal(3);
    });
  });

  // TODO: port remaining ParseTreeTests.cs cases.
});

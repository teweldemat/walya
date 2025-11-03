const {
  FuncScriptParser,
  DefaultFsDataProvider,
  ParseNode
} = require('@tewelde/funcscript');
const { ParseNodeType } = require('@tewelde/funcscript/parser');

function wrapWithRoot(parseNode, expressionLength) {
  if (!parseNode) {
    return null;
  }
  if (parseNode.NodeType === ParseNodeType.RootExpression) {
    return parseNode;
  }

  const length = typeof expressionLength === 'number' ? expressionLength : parseNode.Length;
  return new ParseNode(ParseNodeType.RootExpression, 0, length, [parseNode]);
}

function parseExpression(expression, provider = new DefaultFsDataProvider()) {
  const errors = [];
  const result = FuncScriptParser.parse(provider, expression, errors);
  const root = wrapWithRoot(result.parseNode, expression.length);
  return {
    ...result,
    errors,
    parseNode: root,
    provider,
    expression
  };
}

module.exports = {
  parseExpression,
  DefaultFsDataProvider
};

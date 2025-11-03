const { expect } = require('chai');
const { FuncScriptParser, ParseNodeType } = require('@tewelde/funcscript/parser');
const { DefaultFsDataProvider } = require('@tewelde/funcscript');

function parseExpression(expression) {
  const errors = [];
  const result = FuncScriptParser.parse(new DefaultFsDataProvider(), expression, errors);
  return { ...result, errors };
}

function collectNodes(node) {
  if (!node) {
    return [];
  }
  const nodes = [node];
  if (Array.isArray(node.Childs)) {
    for (const child of node.Childs) {
      nodes.push(...collectNodes(child));
    }
  }
  return nodes;
}

describe('FuncScriptParser2', () => {
  it('captures key/value pairs in parse tree', () => {
    const expression = '{foo:1; bar:2;}';
    const result = parseExpression(expression);
    expect(result.errors).to.be.empty;
    expect(result.parseNode).to.exist;
    expect(result.nextIndex).to.equal(expression.length);

    const nodes = collectNodes(result.parseNode);
    const kvNodes = nodes.filter((n) => n.NodeType === ParseNodeType.KeyValuePair);
    expect(kvNodes.length).to.equal(2);
  });

  it('captures return expression inside KVC', () => {
    const expression = '{foo:1; return foo;}';
    const result = parseExpression(expression);
    expect(result.errors).to.be.empty;
    const nodes = collectNodes(result.parseNode);
    const keywordNode = nodes.find((n) => n.NodeType === ParseNodeType.KeyWord && n.Length === 6);
    expect(keywordNode).to.exist;
  });

  it('records whitespace nodes for multiline list', () => {
    const expression = '[1,\n 2,\n 3]';
    const result = parseExpression(expression);
    expect(result.errors).to.be.empty;
    const whitespace = collectNodes(result.parseNode).filter((n) => n.NodeType === ParseNodeType.WhiteSpace);
    expect(whitespace.length).to.be.greaterThan(0);
  });

  it('captures leading whitespace before expression', () => {
    const expression = '   {foo:1;}';
    const result = parseExpression(expression);
    expect(result.errors).to.be.empty;
    const whitespace = collectNodes(result.parseNode).filter((n) => n.NodeType === ParseNodeType.WhiteSpace);
    expect(whitespace.length).to.be.greaterThan(0);
  });
});

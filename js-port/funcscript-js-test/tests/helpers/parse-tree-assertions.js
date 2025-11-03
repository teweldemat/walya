const { expect } = require('chai');
const { ParseNodeType } = require('@tewelde/funcscript/parser');

function assertRootNode(rootNode, expression) {
  expect(rootNode, 'root node should be defined').to.exist;
  expect(rootNode.NodeType).to.equal(ParseNodeType.RootExpression);
  expect(rootNode.Pos).to.equal(0);
  expect(rootNode.Length).to.equal(expression.length);
}

function assertTreeSpanConsistency(node) {
  if (!node || !Array.isArray(node.Childs)) {
    return;
  }

  let cursor = node.Pos;
  for (const child of node.Childs) {
    expect(child.Pos, 'child nodes should be ordered by starting position').to.be.at.least(cursor);
    assertTreeSpanConsistency(child);
    cursor = child.Pos + child.Length;
    expect(cursor, 'child node should not extend beyond parent span').to.be.at.most(node.Pos + node.Length);
  }
}

function assertNodeSequence(actualNodes, startPos, ...expected) {
  expect(actualNodes, 'node count mismatch').to.have.lengthOf(expected.length);

  let cursor = startPos;
  expected.forEach(([nodeType, length], index) => {
    const node = actualNodes[index];
    expect(node.NodeType, `node ${index} type mismatch`).to.equal(nodeType);
    expect(node.Pos, `node ${index} position mismatch`).to.equal(cursor);
    expect(node.Length, `node ${index} length mismatch`).to.equal(length);
    cursor += length;
  });
}

module.exports = {
  assertRootNode,
  assertTreeSpanConsistency,
  assertNodeSequence
};

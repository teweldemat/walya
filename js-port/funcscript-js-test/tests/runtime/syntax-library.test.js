const { expect } = require('chai');
const {
  evaluate,
  typeOf,
  valueOf,
  FSDataType,
  DefaultFsDataProvider
} = require('@tewelde/funcscript');
const { toPlain } = require('../helpers/runtime');

describe('SyntaxLibrary', () => {
  const provider = new DefaultFsDataProvider();

  function expectPlain(expression, expected) {
    const result = evaluate(expression, provider);
    expect(toPlain(result)).to.deep.equal(expected);
  }

  it('evaluates relational operators', () => {
    expectPlain('1<3', true);
    expectPlain('3>=3', true);
    expectPlain('5>3', true);
    expectPlain('"90"<"99"', true);
    expectPlain('null=null', true);
  });

  it('evaluates IF expressions', () => {
    expectPlain('if 1=1 then "yes" else "no"', 'yes');
    expectPlain('if 1=2 then "yes" else "no"', 'no');
  });

  it('evaluates modulo and division', () => {
    expectPlain('3%2', 1);
    expectPlain('3/2', 1);
  });

  it('evaluates boolean operators', () => {
    expectPlain('true and false', false);
    expectPlain('true or false', true);
  });

  it('supports IN function', () => {
    expectPlain('1 in [1,2]', true);
    expectPlain('0 in [1,2]', false);
  });

  it('evaluates templates', () => {
    expectPlain('{ x:5; return f"ab{x}";}', 'ab5');
  });

  it('strips comments inline', () => {
    expectPlain('1+2//comment', 3);
    expectPlain('1+2//comment\n+5', 8);
  });

  it('formats numbers', () => {
    const result = evaluate('format(12.123,"#,0.00")', provider);
    expect(typeOf(result)).to.equal(FSDataType.String);
    expect(valueOf(result)).to.equal('12.12');
  });
});

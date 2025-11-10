const { expect } = require('chai');
const { evaluate, DefaultFsDataProvider, typeOf, valueOf, FSDataType, FsError } = require('@tewelde/funcscript');

describe('Division operators', () => {
  const provider = new DefaultFsDataProvider();

  function evalTyped(expression) {
    return evaluate(expression, provider);
  }

  it('keeps integer/bigint results for exact slash division', () => {
    const cases = [
      { expression: '4/2', expected: 2, type: FSDataType.Integer },
      { expression: '8/2/2', expected: 2, type: FSDataType.Integer },
      { expression: '12l/3', expected: 4n, type: FSDataType.BigInteger },
      { expression: '25L/5/5', expected: 1n, type: FSDataType.BigInteger }
    ];

    for (const { expression, expected, type } of cases) {
      const result = evalTyped(expression);
      expect(typeOf(result), expression).to.equal(type);
      expect(valueOf(result), expression).to.equal(expected);
    }
  });

  it('promotes slash division to float when remainder exists', () => {
    const cases = [
      { expression: '1/2', expected: 0.5 },
      { expression: '3/2', expected: 1.5 },
      { expression: '6/4/2', expected: 0.75 },
      { expression: '6/3/4', expected: 0.5 },
      { expression: '9/3/2/2', expected: 0.75 },
      { expression: '9l/2', expected: 4.5 },
      { expression: '5/2l', expected: 2.5 }
    ];

    for (const { expression, expected } of cases) {
      const result = evalTyped(expression);
      expect(typeOf(result), expression).to.equal(FSDataType.Float);
      expect(valueOf(result), expression).to.be.closeTo(expected, 1e-10);
    }
  });

  it('performs integer division with div operator', () => {
    const cases = [
      { expression: '9 div 2', expected: 4, type: FSDataType.Integer },
      { expression: '18 div 3 div 2', expected: 3, type: FSDataType.Integer },
      { expression: '9l div 2', expected: 4n, type: FSDataType.BigInteger },
      { expression: '9 div 2l', expected: 4n, type: FSDataType.BigInteger },
      { expression: '50l div 4 div 2', expected: 6n, type: FSDataType.BigInteger },
      { expression: '-9 div 2', expected: -4, type: FSDataType.Integer }
    ];

    for (const { expression, expected, type } of cases) {
      const result = evalTyped(expression);
      expect(typeOf(result), expression).to.equal(type);
      expect(valueOf(result), expression).to.equal(expected);
    }
  });

  it('rejects non-integer operands for div operator', () => {
    const cases = ['4.0 div 2', '4 div 2.0', '4 div "2"', '4 div null'];
    for (const expression of cases) {
      const result = evalTyped(expression);
      expect(typeOf(result), expression).to.equal(FSDataType.Error);
      const err = valueOf(result);
      expect(err.errorType, expression).to.equal(FsError.ERROR_TYPE_MISMATCH);
    }
  });
});

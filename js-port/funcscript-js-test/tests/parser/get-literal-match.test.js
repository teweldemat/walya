const { expect } = require('chai');
const { getLiteralMatch } = require('@tewelde/funcscript/parser');

// Mirrors FuncScript.Test/GetLiteralMatchTests.cs (stress test scaled for JS runtime).
describe('GetLiteralMatch', () => {
  it('matches exact keyword', () => {
    const index = getLiteralMatch('Hello, world!', 0, 'Hello');
    expect(index).to.equal(5);
  });

  it('returns start index when no match', () => {
    const index = getLiteralMatch('Hello, world!', 0, 'Goodbye');
    expect(index).to.equal(0);
  });

  it('is case insensitive', () => {
    const index = getLiteralMatch('Hello, world!', 0, 'HELLO');
    expect(index).to.equal(5);
  });

  it('accepts multiple candidates', () => {
    const index = getLiteralMatch('Hello, world!', 0, 'Goodbye', 'Hello', 'Hi');
    expect(index).to.equal(5);
  });

  it('returns index when start is beyond input', () => {
    const index = getLiteralMatch('Hello, world!', 20, 'Hello');
    expect(index).to.equal(20);
  });

  it('handles empty expression', () => {
    const index = getLiteralMatch('', 0, 'Hello');
    expect(index).to.equal(0);
  });

  it('matches when starting inside the string', () => {
    const index = getLiteralMatch('Hello, world!', 7, 'world');
    expect(index).to.equal(12);
  });

  it('matches keyword at end of string', () => {
    const index = getLiteralMatch('Hello, world!', 12, '!');
    expect(index).to.equal(13);
  });

  it('throws on null expression', () => {
    expect(() => getLiteralMatch(null, 0, 'Hello')).to.throw();
  });

  it('stress test keeps performance reasonable', () => {
    const prefix = 'x'.repeat(50_000);
    const suffix = 'y'.repeat(50_000);
    const expression = `${prefix}Hello, world!${suffix}`;
    const keywords = Array.from({ length: 2000 }, (_, i) => `kw${i}`);
    keywords[123] = 'Hello, world!';

    const start = performance.now();
    const index = getLiteralMatch(expression, prefix.length, ...keywords);
    const duration = performance.now() - start;

    expect(index).to.equal(prefix.length + 'Hello, world!'.length);
    expect(duration).to.be.below(250);
  });
});

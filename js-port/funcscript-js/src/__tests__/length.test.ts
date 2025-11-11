import { describe, expect, it } from 'vitest';
import { Engine, DefaultFsDataProvider, valueOf } from '../funcscript.js';

describe('length function', () => {
  it('returns the number of items in a list', () => {
    const provider = new DefaultFsDataProvider();
    const typed = Engine.evaluate('length([1,2,3,4])', provider);
    expect(valueOf(typed)).toBe(4);
  });

  it('returns the number of characters in a string', () => {
    const provider = new DefaultFsDataProvider();
    const typed = Engine.evaluate('length("hello world")', provider);
    expect(valueOf(typed)).toBe(11);
  });
});

import { describe, expect, it } from 'vitest';
import { Engine, DefaultFsDataProvider } from '../funcscript.js';

describe('math runtime values', () => {
  it('exposes random pi and e', () => {
    const provider = new DefaultFsDataProvider();
    provider.set('math', provider.get('math'));

    const random = Engine.evaluate('math.random()', provider);
    const pi = Engine.evaluate('math.pi', provider);
    const e = Engine.evaluate('math.e', provider);

    expect(typeof random).toBe('number');
    expect(random).toBeGreaterThanOrEqual(0);
    expect(random).toBeLessThanOrEqual(1);

    expect(pi).toBeCloseTo(Math.PI, 12);
    expect(e).toBeCloseTo(Math.E, 12);
  });
});

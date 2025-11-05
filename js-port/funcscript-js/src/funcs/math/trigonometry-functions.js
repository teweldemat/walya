const { BaseFunction, CallType } = require('../../core/function-base');
const { ensureTyped, typeOf, valueOf, makeValue } = require('../../core/value');
const { FSDataType } = require('../../core/fstypes');
const { makeError, FsError } = require('../helpers');

function ensureNumeric(symbol, parameter, parameterName = 'number') {
  const typed = ensureTyped(parameter);
  if (typeOf(typed) === FSDataType.Integer || typeOf(typed) === FSDataType.Float || typeOf(typed) === FSDataType.BigInteger) {
    return { ok: true, value: Number(valueOf(typed)) };
  }
  return { ok: false, error: makeError(FsError.ERROR_TYPE_MISMATCH, `${symbol}: ${parameterName} expected`) };
}

class SineFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'sin';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.sin(result.value));
  }
}

class CosineFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'cos';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.cos(result.value));
  }
}

class TangentFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'tan';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.tan(result.value));
  }
}

class ArcSineFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'asin';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.asin(result.value));
  }
}

class ArcCosineFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'acos';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.acos(result.value));
  }
}

class ArcTangentFunction extends BaseFunction {
  constructor() {
    super();
    this.symbol = 'atan';
    this.callType = CallType.Prefix;
  }

  get maxParameters() {
    return 1;
  }

  evaluate(provider, parameters) {
    const result = ensureNumeric(this.symbol, parameters.getParameter(provider, 0));
    if (!result.ok) {
      return result.error;
    }
    return makeValue(FSDataType.Float, Math.atan(result.value));
  }
}

module.exports = {
  SineFunction,
  CosineFunction,
  TangentFunction,
  ArcSineFunction,
  ArcCosineFunction,
  ArcTangentFunction
};

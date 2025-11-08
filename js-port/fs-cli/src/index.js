const runtime = require('@tewelde/funcscript');
const pkg = require('../package.json');

const {
  evaluate,
  test,
  DefaultFsDataProvider,
  ensureTyped,
  typeOf,
  valueOf,
  FSDataType,
  getTypeName
} = runtime;

function toPlainValue(value, seenKvcs = new WeakSet(), seenLists = new WeakSet()) {
  const typed = ensureTyped(value);
  const dataType = typeOf(typed);
  switch (dataType) {
    case FSDataType.Null:
      return null;
    case FSDataType.Boolean:
    case FSDataType.Integer:
    case FSDataType.Float:
      return valueOf(typed);
    case FSDataType.BigInteger:
      return valueOf(typed).toString();
    case FSDataType.String:
      return valueOf(typed);
    case FSDataType.DateTime: {
      const date = valueOf(typed);
      return date instanceof Date ? date.toISOString() : String(date);
    }
    case FSDataType.Guid:
      return String(valueOf(typed));
    case FSDataType.ByteArray: {
      const buffer = valueOf(typed);
      return Buffer.from(buffer).toString('base64');
    }
    case FSDataType.List: {
      const list = valueOf(typed);
      if (seenLists.has(list)) {
        return '[Circular List]';
      }
      seenLists.add(list);
      const arr = [];
      for (const entry of list) {
        arr.push(toPlainValue(entry, seenKvcs, seenLists));
      }
      seenLists.delete(list);
      return arr;
    }
    case FSDataType.KeyValueCollection: {
      const collection = valueOf(typed);
      if (seenKvcs.has(collection)) {
        return '[Circular Object]';
      }
      seenKvcs.add(collection);
      const obj = {};
      for (const [key, val] of collection.getAll()) {
        obj[key] = toPlainValue(val, seenKvcs, seenLists);
      }
      seenKvcs.delete(collection);
      return obj;
    }
    case FSDataType.Error: {
      const err = valueOf(typed) || {};
      const payload = {
        errorType: err.errorType || 'Error',
        errorMessage: err.errorMessage || ''
      };
      if (err.errorData !== undefined && err.errorData !== null) {
        try {
          payload.errorData = Array.isArray(err.errorData) && err.errorData.length === 2 && typeof err.errorData[0] === 'number'
            ? toPlainValue(err.errorData, seenKvcs, seenLists)
            : err.errorData;
        } catch (inner) {
          payload.errorData = String(inner?.message || inner);
        }
      }
      return payload;
    }
    case FSDataType.Function:
      return '[Function]';
    default:
      return valueOf(typed);
  }
}

function formatJson(value, pretty = true) {
  return JSON.stringify(value, null, pretty ? 2 : 0);
}

function printHelp() {
  const lines = [
    'FuncScript CLI',
    '',
    'Usage:',
    "  fs-cli '<expr>'",
    "  fs-cli --test '<expr>' '<test-expr>'",
    '',
    'Flags:',
    '  --test, -t        Run the given test expression against the expression output.',
    '  --json            Print JSON output only.',
    '  --compact         Use compact JSON output.',
    '  --version, -v     Print CLI version.',
    '  --help, -h        Show this message.',
    '',
    'Examples:',
    "  fs-cli '1 + 2'",
    '  fs-cli --test "a + b" "{ suite: { cases: [{a:1,b:2}], test: (result, data) => result = data.a + data.b }; return [suite]; }"'
  ];
  console.log(lines.join('\n'));
}

function parseArgs(rawArgs) {
  if (!Array.isArray(rawArgs)) {
    return { mode: 'help' };
  }

  const options = {
    mode: 'eval',
    json: false,
    pretty: true,
    expression: null,
    testExpression: null
  };

  const positionals = [];

  for (const arg of rawArgs) {
    switch (arg) {
      case '--help':
      case '-h':
        return { ...options, mode: 'help' };
      case '--version':
      case '-v':
        return { ...options, mode: 'version' };
      case '--self-test':
        return { ...options, mode: 'self-test' };
      case '--test':
      case '-t':
        options.mode = 'test';
        break;
      case '--json':
        options.json = true;
        options.pretty = true;
        break;
      case '--compact':
        options.pretty = false;
        options.json = true;
        break;
      default:
        positionals.push(arg);
        break;
    }
  }

  if (options.mode === 'test') {
    [options.expression, options.testExpression] = positionals;
  } else {
    [options.expression] = positionals;
  }

  return options;
}

function ensureExpression(value, label) {
  if (!value || typeof value !== 'string') {
    throw new Error(`Missing ${label}.`);
  }
  return value;
}

function evaluateExpression(expression) {
  const provider = new DefaultFsDataProvider();
  const typedResult = evaluate(expression, provider);
  const plain = toPlainValue(typedResult);
  const typeName = getTypeName(typeOf(ensureTyped(typedResult)));
  return { type: typeName, value: plain };
}

function runTestSuite(expression, testExpression) {
  const provider = new DefaultFsDataProvider();
  return test(expression, testExpression, provider);
}

function printEvaluation(expression, options) {
  const { json, pretty } = options;
  const result = evaluateExpression(expression);
  if (json) {
    console.log(formatJson(result, pretty));
    return;
  }
  console.log(`Type: ${result.type}`);
  console.log('Value:');
  console.log(formatJson(result.value, pretty));
}

function printTestResults(expression, testExpression, options) {
  const { json, pretty } = options;
  const result = runTestSuite(expression, testExpression);
  const summary = result?.summary || { cases: 0, passed: 0, failed: 0, suites: 0 };
  console.log(`Suites: ${summary.suites} | Cases: ${summary.cases} | Passed: ${summary.passed} | Failed: ${summary.failed}`);
  if (json) {
    console.log(formatJson(result, pretty));
  } else {
    console.log('Details:');
    console.log(formatJson(result, pretty));
  }
  if (summary.failed > 0) {
    process.exitCode = 1;
  }
}

function runSelfTest() {
  const evalResult = evaluateExpression('1 + 2');
  if (evalResult.value !== 3) {
    throw new Error('Self-test evaluation failed.');
  }

  const testExpression = `
{
  sampleSuite: {
    name: "addition";
    cases: [
      { "a": 1, "b": 2 }
    ];
    test: (result, data) => result = data.a + data.b
  };

  return [sampleSuite];
}`;

  const testResult = runTestSuite('a + b', testExpression);
  if (testResult.summary.failed !== 0) {
    throw new Error('Self-test suite reported failures.');
  }
  console.log('Self-test passed.');
}

function runCli(rawArgs) {
  try {
    const options = parseArgs(rawArgs);
    switch (options.mode) {
      case 'help':
        printHelp();
        break;
      case 'version':
        console.log(pkg.version);
        break;
      case 'self-test':
        runSelfTest();
        break;
      case 'test': {
        const expression = ensureExpression(options.expression, 'expression');
        const testExpression = ensureExpression(options.testExpression, 'test expression');
        printTestResults(expression, testExpression, options);
        break;
      }
      case 'eval':
      default: {
        const expression = ensureExpression(options.expression, 'expression');
        printEvaluation(expression, options);
        break;
      }
    }
  } catch (error) {
    console.error(error?.message || error);
    if (error?.stack) {
      console.error(error.stack);
    }
    process.exitCode = 1;
  }
}

module.exports = {
  toPlainValue,
  evaluateExpression,
  runTestSuite,
  runCli
};

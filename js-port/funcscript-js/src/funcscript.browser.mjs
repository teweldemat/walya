import * as parserModuleRaw from './parser/funcscript-parser.mjs';
import * as dataProvidersModule from './core/data-provider.js';
import * as valueModuleRaw from './core/value.js';
import * as fstypesModuleRaw from './core/fstypes.js';
import * as functionBaseModuleRaw from './core/function-base.js';
import * as expressionFunctionModuleRaw from './core/expression-function.js';
import * as fsListModuleRaw from './model/fs-list.js';
import * as keyValueCollectionModuleRaw from './model/key-value-collection.js';
import * as fsErrorModuleRaw from './model/fs-error.js';
import * as parseNodeModuleRaw from './parser/parse-node.js';
import * as buildBrowserBuiltinMapModule from './funcs/index.browser.js';

const interopDefault = (mod) => (mod && 'default' in mod ? mod.default : mod);

const parserModule = interopDefault(parserModuleRaw);
const dataProviders = interopDefault(dataProvidersModule);
const valueModule = interopDefault(valueModuleRaw);
const fstypesModule = interopDefault(fstypesModuleRaw);
const functionBaseModule = interopDefault(functionBaseModuleRaw);
const expressionFunctionModule = interopDefault(expressionFunctionModuleRaw);
const fsListModule = interopDefault(fsListModuleRaw);
const keyValueCollectionModule = interopDefault(keyValueCollectionModuleRaw);
const fsErrorModule = interopDefault(fsErrorModuleRaw);
const parseNodeModule = interopDefault(parseNodeModuleRaw);
const buildBrowserBuiltinMap = interopDefault(buildBrowserBuiltinMapModule);

const { FuncScriptParser } = parserModule;
const { MapDataProvider, FsDataProvider, KvcProvider } = dataProviders;
const {
  ensureTyped,
  normalize,
  makeValue,
  typeOf,
  valueOf,
  typedNull,
  isTyped,
  expectType,
  convertToCommonNumericType
} = valueModule;
const { FSDataType, getTypeName } = fstypesModule;
const { CallType, BaseFunction, ParameterList } = functionBaseModule;
const { ExpressionFunction } = expressionFunctionModule;
const { FsList, ArrayFsList } = fsListModule;
const { KeyValueCollection, SimpleKeyValueCollection } = keyValueCollectionModule;
const { FsError } = fsErrorModule;
const { ParseNode } = parseNodeModule;

const builtinProvider = new MapDataProvider(buildBrowserBuiltinMap());

class DefaultFsDataProvider extends MapDataProvider {
  constructor(map = {}, parent = builtinProvider) {
    super(map, parent);
  }
}

function evaluate(expression, provider = new DefaultFsDataProvider()) {
  const { block } = FuncScriptParser.parse(provider, expression);
  if (!block) {
    throw new Error('Failed to parse expression');
  }
  return ensureTyped(block.evaluate(provider));
}

function colorParseTree(node) {
  if (!node || typeof node.Length !== 'number' || node.Length <= 0) {
    return [];
  }

  const childs = Array.isArray(node.Childs) ? node.Childs : [];
  if (childs.length === 0) {
    return [node];
  }

  const result = [];
  const nodePos = typeof node.Pos === 'number' ? node.Pos : Number(node.Pos ?? 0) || 0;
  const nodeEnd = nodePos + node.Length;

  let cursor = nodePos;
  for (const child of childs) {
    if (!child || typeof child.Pos !== 'number' || typeof child.Length !== 'number') {
      continue;
    }
    const childPos = child.Pos;
    if (childPos > cursor) {
      result.push(new ParseNode(node.NodeType, cursor, childPos - cursor));
    }
    result.push(...colorParseTree(child));
    cursor = childPos + child.Length;
  }

  if (cursor < nodeEnd) {
    result.push(new ParseNode(node.NodeType, cursor, nodeEnd - cursor));
  }

  return result;
}

const Engine = {
  evaluate,
  colorParseTree,
  FuncScriptParser,
  DefaultFsDataProvider,
  FsDataProvider,
  MapDataProvider,
  KvcProvider,
  ensureTyped,
  normalize,
  makeValue,
  typeOf,
  valueOf,
  typedNull,
  isTyped,
  expectType,
  convertToCommonNumericType,
  FSDataType,
  getTypeName,
  CallType,
  BaseFunction,
  ParameterList,
  ExpressionFunction,
  FsList,
  ArrayFsList,
  KeyValueCollection,
  SimpleKeyValueCollection,
  FsError,
  buildBuiltinMap: buildBrowserBuiltinMap
};

const funcscript = {
  Engine,
  evaluate,
  colorParseTree,
  FuncScriptParser,
  DefaultFsDataProvider,
  FsDataProvider,
  MapDataProvider,
  KvcProvider,
  ensureTyped,
  normalize,
  makeValue,
  typeOf,
  valueOf,
  typedNull,
  isTyped,
  expectType,
  convertToCommonNumericType,
  FSDataType,
  getTypeName,
  CallType,
  BaseFunction,
  ParameterList,
  ExpressionFunction,
  FsList,
  ArrayFsList,
  KeyValueCollection,
  SimpleKeyValueCollection,
  FsError,
  buildBuiltinMap: buildBrowserBuiltinMap
};

export {
  Engine,
  evaluate,
  colorParseTree,
  FuncScriptParser,
  DefaultFsDataProvider,
  FsDataProvider,
  MapDataProvider,
  KvcProvider,
  ensureTyped,
  normalize,
  makeValue,
  typeOf,
  valueOf,
  typedNull,
  isTyped,
  expectType,
  convertToCommonNumericType,
  FSDataType,
  getTypeName,
  CallType,
  BaseFunction,
  ParameterList,
  ExpressionFunction,
  FsList,
  ArrayFsList,
  KeyValueCollection,
  SimpleKeyValueCollection,
  FsError,
  buildBrowserBuiltinMap as buildBuiltinMap
};

export default funcscript;

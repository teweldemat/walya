import { Engine } from '@tewelde/funcscript';
export const parseNodePalette = [
    '#569CD6', // keywords
    '#9CDCFE', // identifiers
    '#DCDCAA', // function names
    '#CE9178', // strings
    '#B5CEA8', // numbers
    '#4EC9B0', // types / special identifiers
    '#D4D4D4', // punctuation / operators
    '#6A9955' // comments
];
const categoryColors = {
    keyword: '#569CD6',
    string: '#CE9178',
    number: '#B5CEA8',
    identifier: '#9CDCFE',
    function: '#DCDCAA',
    operator: '#D4D4D4',
    punctuation: '#D4D4D4',
    comment: '#6A9955',
    type: '#4EC9B0',
    boolean: '#569CD6',
    default: '#D4D4D4',
    whitespace: null
};
const explicitNodeTypeCategory = new Map([
    ['WhiteSpace', 'whitespace'],
    ['RootExpression', 'default'],
    ['Comment', 'comment'],
    ['LiteralString', 'string'],
    ['StringTemplate', 'string'],
    ['StringDelimeter', 'string'],
    ['LiteralInteger', 'number'],
    ['LiteralDouble', 'number'],
    ['LiteralLong', 'number'],
    ['LiteralFloat', 'number'],
    ['LiteralBoolean', 'boolean'],
    ['KeyWord', 'keyword'],
    ['Case', 'keyword'],
    ['IfExpression', 'keyword'],
    ['SwitchExpression', 'keyword'],
    ['LambdaExpression', 'function'],
    ['GeneralInfixExpression', 'function'],
    ['FunctionCall', 'function'],
    ['Identifier', 'identifier'],
    ['IdentiferList', 'identifier'],
    ['Key', 'identifier'],
    ['Selection', 'identifier'],
    ['MemberAccess', 'identifier'],
    ['Operator', 'operator'],
    ['LambdaArrow', 'operator'],
    ['ThirdOperandDelimeter', 'operator'],
    ['PrefixOperatorExpression', 'operator'],
    ['FunctionParameterList', 'punctuation'],
    ['ExpressionInBrace', 'punctuation'],
    ['OpenBrace', 'punctuation'],
    ['CloseBrance', 'punctuation'],
    ['ListSeparator', 'punctuation'],
    ['Colon', 'punctuation'],
    ['List', 'punctuation'],
    ['KeyValueCollection', 'punctuation'],
    ['KeyValuePair', 'punctuation'],
    ['DataConnection', 'type'],
    ['SignalConnection', 'type'],
    ['NormalErrorSink', 'type'],
    ['SigSequence', 'type']
]);
const inferCategory = (nodeTypeRaw) => {
    const nodeType = nodeTypeRaw?.trim() ?? '';
    if (!nodeType) {
        return 'default';
    }
    const explicit = explicitNodeTypeCategory.get(nodeType);
    if (explicit) {
        return explicit;
    }
    const lower = nodeType.toLowerCase();
    if (lower.includes('whitespace')) {
        return 'whitespace';
    }
    if (lower.includes('comment')) {
        return 'comment';
    }
    if (lower.includes('string') || lower.includes('template')) {
        return 'string';
    }
    if (lower.includes('literal') && (lower.includes('int') || lower.includes('double') || lower.includes('long') || lower.includes('float'))) {
        return 'number';
    }
    if (lower.includes('keyword') || lower === 'case' || lower === 'ifexpression' || lower.includes('switch')) {
        return 'keyword';
    }
    if (lower.includes('boolean')) {
        return 'boolean';
    }
    if (lower.includes('identifier')) {
        return 'identifier';
    }
    if (lower.includes('function')) {
        if (lower.includes('parameter')) {
            return 'punctuation';
        }
        return 'function';
    }
    if (lower.includes('operator') || lower.includes('arrow')) {
        return 'operator';
    }
    if (lower.includes('brace') ||
        lower.includes('separator') ||
        lower.includes('colon') ||
        lower.includes('list') ||
        lower.includes('delimeter')) {
        return 'punctuation';
    }
    if (lower.includes('keyvalue') || lower.includes('selection') || lower.includes('member')) {
        return 'identifier';
    }
    if (lower.includes('error') || lower.includes('signal')) {
        return 'keyword';
    }
    return 'default';
};
const getSegmentColor = (nodeType) => {
    const category = inferCategory(nodeType);
    return categoryColors[category] ?? categoryColors.default;
};
const sanitizeRange = (start, end, length) => {
    const safeStart = Math.max(0, Math.min(start, length));
    const safeEnd = Math.max(safeStart, Math.min(end, length));
    return safeEnd > safeStart ? { start: safeStart, end: safeEnd } : null;
};
export function computeColoredSegments(expression, parseNode) {
    const length = expression.length;
    if (!parseNode || length === 0) {
        return length
            ? [
                {
                    start: 0,
                    end: length,
                    nodeType: 'Expression',
                    color: null
                }
            ]
            : [];
    }
    let rawSegments = [];
    try {
        const segments = Engine.colorParseTree(parseNode);
        if (Array.isArray(segments)) {
            rawSegments = segments;
        }
    }
    catch (error) {
        return length
            ? [
                {
                    start: 0,
                    end: length,
                    nodeType: 'Expression',
                    color: null
                }
            ]
            : [];
    }
    const normalized = rawSegments
        .map((segment) => {
        const data = segment;
        const pos = typeof data.Pos === 'number' ? data.Pos : typeof data.pos === 'number' ? data.pos : 0;
        const len = typeof data.Length === 'number' ? data.Length : typeof data.length === 'number' ? data.length : 0;
        const nodeType = typeof data.NodeType === 'string'
            ? data.NodeType
            : typeof data.nodeType === 'string'
                ? data.nodeType
                : 'Node';
        const safe = sanitizeRange(pos, pos + len, length);
        if (!safe) {
            return null;
        }
        return {
            start: safe.start,
            end: safe.end,
            nodeType
        };
    })
        .filter((segment) => Boolean(segment))
        .sort((a, b) => (a.start === b.start ? a.end - b.end : a.start - b.start));
    const segments = [];
    let cursor = 0;
    for (const segment of normalized) {
        const start = Math.max(cursor, segment.start);
        const end = Math.max(start, segment.end);
        if (start > cursor) {
            segments.push({
                start: cursor,
                end: start,
                nodeType: 'Whitespace',
                color: null
            });
        }
        if (end > start) {
            const color = getSegmentColor(segment.nodeType);
            if (!color) {
                segments.push({
                    start,
                    end,
                    nodeType: segment.nodeType,
                    color: null
                });
            }
            else {
                segments.push({
                    start,
                    end,
                    nodeType: segment.nodeType,
                    color
                });
            }
        }
        cursor = end;
    }
    if (cursor < length) {
        segments.push({
            start: cursor,
            end: length,
            nodeType: 'Whitespace',
            color: null
        });
    }
    if (segments.length === 0 && length > 0) {
        segments.push({
            start: 0,
            end: length,
            nodeType: 'Expression',
            color: null
        });
    }
    return segments;
}

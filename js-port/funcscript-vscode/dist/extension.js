"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const analysis_1 = require("./analysis");
const tokenTypesLegend = [
    'comment',
    'string',
    'number',
    'keyword',
    'variable',
    'operator',
    'property'
];
const tokenModifiersLegend = [];
const legend = new vscode.SemanticTokensLegend(tokenTypesLegend, tokenModifiersLegend);
const NODE_TYPE_TOKEN = {
    Comment: 'comment',
    LiteralString: 'string',
    StringTemplate: 'string',
    LiteralInteger: 'number',
    LiteralDouble: 'number',
    LiteralLong: 'number',
    KeyWord: 'keyword',
    ErrorKeyWord: 'keyword',
    Case: 'keyword',
    Identifier: 'variable',
    Key: 'property',
    MemberAccess: 'property',
    Operator: 'operator'
};
class DocumentAnalysisCache {
    constructor() {
        this.cache = new Map();
    }
    get(document) {
        const key = document.uri.toString();
        const current = this.cache.get(key);
        if (current && current.version === document.version) {
            return current.outcome;
        }
        const outcome = (0, analysis_1.analyzeText)(document.getText());
        this.cache.set(key, { version: document.version, outcome });
        return outcome;
    }
    evict(document) {
        this.cache.delete(document.uri.toString());
    }
}
const isMeaningful = (text) => text.trim().length > 0;
const toRange = (document, start, end, docLength) => {
    const safeStart = Math.max(0, Math.min(start, docLength));
    const safeEnd = Math.max(safeStart, Math.min(end, docLength));
    const startPos = document.positionAt(safeStart);
    const endPos = document.positionAt(safeEnd);
    return new vscode.Range(startPos, endPos);
};
class FuncScriptSemanticTokensProvider {
    constructor(cache) {
        this.cache = cache;
    }
    async provideDocumentSemanticTokens(document, _token) {
        const analysis = this.cache.get(document);
        const { segments, text } = analysis;
        if (segments.length === 0) {
            return new vscode.SemanticTokens(new Uint32Array());
        }
        const builder = new vscode.SemanticTokensBuilder(legend);
        const docLength = analysis.text.length;
        for (const segment of segments) {
            const tokenType = NODE_TYPE_TOKEN[segment.nodeType];
            if (!tokenType) {
                continue;
            }
            const segmentText = text.slice(segment.start, segment.end);
            if (!isMeaningful(segmentText)) {
                continue;
            }
            const range = toRange(document, segment.start, segment.end, docLength);
            builder.push(range, tokenType);
        }
        return builder.build();
    }
}
const clampPosition = (document, index, docLength) => {
    const safeIndex = Math.max(0, Math.min(index, docLength));
    return document.positionAt(safeIndex);
};
const foldRegionToRange = (document, region, docLength) => {
    const start = clampPosition(document, region.start, docLength);
    const end = clampPosition(document, Math.max(region.end - 1, region.start), docLength);
    if (end.line <= start.line) {
        return null;
    }
    return { start, end };
};
class FuncScriptFoldingRangeProvider {
    constructor(cache) {
        this.cache = cache;
    }
    provideFoldingRanges(document, _context, _token) {
        const analysis = this.cache.get(document);
        const docLength = analysis.text.length;
        const regions = (0, analysis_1.collectFoldRegions)(analysis.parseNode, docLength);
        if (!regions.length) {
            return [];
        }
        const byLine = new Map();
        for (const region of regions) {
            const range = foldRegionToRange(document, region, docLength);
            if (!range) {
                continue;
            }
            const { start, end } = range;
            const existing = byLine.get(start.line);
            if (!existing || existing.end < end.line) {
                const foldingRange = new vscode.FoldingRange(start.line, end.line);
                byLine.set(start.line, foldingRange);
            }
        }
        return Array.from(byLine.values()).sort((a, b) => a.start - b.start);
    }
}
function activate(context) {
    const cache = new DocumentAnalysisCache();
    const selector = { language: 'funcscript' };
    context.subscriptions.push(vscode.languages.registerDocumentSemanticTokensProvider(selector, new FuncScriptSemanticTokensProvider(cache), legend));
    context.subscriptions.push(vscode.languages.registerFoldingRangeProvider(selector, new FuncScriptFoldingRangeProvider(cache)));
    context.subscriptions.push(vscode.workspace.onDidCloseTextDocument((document) => cache.evict(document)));
}
function deactivate() { }
//# sourceMappingURL=extension.js.map
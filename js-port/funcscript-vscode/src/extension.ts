import * as vscode from 'vscode';
import { analyzeText, collectFoldRegions, ParseOutcome, ParseSegment } from './analysis';
import type { ParseNode } from '@tewelde/funcscript/parser';

const tokenTypesLegend: vscode.SemanticTokensLegend['tokenTypes'] = [
  'comment',
  'string',
  'number',
  'keyword',
  'variable',
  'operator',
  'property'
];

const tokenModifiersLegend: vscode.SemanticTokensLegend['tokenModifiers'] = [];

const legend = new vscode.SemanticTokensLegend(tokenTypesLegend, tokenModifiersLegend);

type TokenType = (typeof tokenTypesLegend)[number];

type NodeTypeClassifier = Record<string, TokenType>;

const NODE_TYPE_TOKEN: NodeTypeClassifier = {
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
  private readonly cache = new Map<string, { version: number; outcome: ParseOutcome }>();

  get(document: vscode.TextDocument): ParseOutcome {
    const key = document.uri.toString();
    const current = this.cache.get(key);
    if (current && current.version === document.version) {
      return current.outcome;
    }

    const outcome = analyzeText(document.getText());
    this.cache.set(key, { version: document.version, outcome });
    return outcome;
  }

  evict(document: vscode.TextDocument) {
    this.cache.delete(document.uri.toString());
  }
}

const isMeaningful = (text: string) => text.trim().length > 0;

const toRange = (
  document: vscode.TextDocument,
  start: number,
  end: number,
  docLength: number
) => {
  const safeStart = Math.max(0, Math.min(start, docLength));
  const safeEnd = Math.max(safeStart, Math.min(end, docLength));
  const startPos = document.positionAt(safeStart);
  const endPos = document.positionAt(safeEnd);
  return new vscode.Range(startPos, endPos);
};

class FuncScriptSemanticTokensProvider implements vscode.DocumentSemanticTokensProvider {
  constructor(private readonly cache: DocumentAnalysisCache) {}

  async provideDocumentSemanticTokens(
    document: vscode.TextDocument,
    _token: vscode.CancellationToken
  ): Promise<vscode.SemanticTokens> {
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

const clampPosition = (
  document: vscode.TextDocument,
  index: number,
  docLength: number
) => {
  const safeIndex = Math.max(0, Math.min(index, docLength));
  return document.positionAt(safeIndex);
};

const foldRegionToRange = (
  document: vscode.TextDocument,
  region: { start: number; end: number },
  docLength: number
) => {
  const start = clampPosition(document, region.start, docLength);
  const end = clampPosition(document, Math.max(region.end - 1, region.start), docLength);
  if (end.line <= start.line) {
    return null;
  }
  return { start, end };
};

class FuncScriptFoldingRangeProvider implements vscode.FoldingRangeProvider {
  constructor(private readonly cache: DocumentAnalysisCache) {}

  provideFoldingRanges(
    document: vscode.TextDocument,
    _context: vscode.FoldingContext,
    _token: vscode.CancellationToken
  ): vscode.ProviderResult<vscode.FoldingRange[]> {
    const analysis = this.cache.get(document);
    const docLength = analysis.text.length;
    const regions = collectFoldRegions(analysis.parseNode as ParseNode | null, docLength);
    if (!regions.length) {
      return [];
    }

    const byLine = new Map<number, vscode.FoldingRange>();

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

export function activate(context: vscode.ExtensionContext) {
  const cache = new DocumentAnalysisCache();
  const selector: vscode.DocumentSelector = { language: 'funcscript' };

  context.subscriptions.push(
    vscode.languages.registerDocumentSemanticTokensProvider(
      selector,
      new FuncScriptSemanticTokensProvider(cache),
      legend
    )
  );

  context.subscriptions.push(
    vscode.languages.registerFoldingRangeProvider(
      selector,
      new FuncScriptFoldingRangeProvider(cache)
    )
  );

  context.subscriptions.push(
    vscode.workspace.onDidCloseTextDocument((document) => cache.evict(document))
  );
}

export function deactivate() {}

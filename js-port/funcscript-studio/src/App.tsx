import { ChangeEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import './App.css';
import { FuncScriptTester, type FuncScriptTesterVariableInput } from '@tewelde/funcscript-editor';

type StoredTestCase = {
  id: string;
  name: string;
  variables: FuncScriptTesterVariableInput[];
  updatedAt: string;
};

type StoredFormula = {
  id: string;
  name: string;
  expression: string;
  updatedAt: string;
  testCases: StoredTestCase[];
};

const FORMULA_STORAGE_KEY = 'funscript-studio:formulas';
const DEFAULT_FORMULA_NAME = 'Net Income Example';
const DEFAULT_FORMULA = 'gross * (1 - taxRate)';
const DEFAULT_VARIABLES: FuncScriptTesterVariableInput[] = [
  { name: 'gross', expression: '5200' },
  { name: 'taxRate', expression: '0.12' }
];

const createStableId = (prefix: string) => {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    try {
      return `${prefix}-${crypto.randomUUID()}`;
    } catch {
      // fall through
    }
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const createFormulaId = () => createStableId('formula');
const createTestCaseId = () => createStableId('testcase');

const sanitizeVariables = (variables: FuncScriptTesterVariableInput[]): FuncScriptTesterVariableInput[] =>
  variables.map((variable) => ({
    name: typeof variable?.name === 'string' ? variable.name.trim() : '',
    expression: typeof variable?.expression === 'string' ? variable.expression : ''
  }));

const cloneVariables = (variables: FuncScriptTesterVariableInput[]): FuncScriptTesterVariableInput[] =>
  sanitizeVariables(variables);

const createVariablesSignature = (variables: FuncScriptTesterVariableInput[] | undefined): string =>
  JSON.stringify(sanitizeVariables(Array.isArray(variables) ? variables : []));

const EMPTY_VARIABLES_SIGNATURE = '[]';

const sortTestCases = (cases: StoredTestCase[]): StoredTestCase[] =>
  [...cases].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));

const sortFormulas = (formulas: StoredFormula[]): StoredFormula[] =>
  [...formulas].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));

const normalizeTestCases = (input: unknown): StoredTestCase[] => {
  if (!Array.isArray(input)) {
    return [];
  }
  const now = new Date().toISOString();
  const normalized: StoredTestCase[] = [];
  input.forEach((candidate) => {
    if (!candidate || typeof candidate !== 'object') {
      return;
    }
    const { id, name, variables, updatedAt } = candidate as Partial<StoredTestCase> & {
      variables?: unknown;
    };
    const safeName = typeof name === 'string' && name.trim().length > 0 ? name : 'Untitled';
    const parsedVariables = Array.isArray(variables)
      ? sanitizeVariables(variables as FuncScriptTesterVariableInput[])
      : [];
    normalized.push({
      id: typeof id === 'string' ? id : createTestCaseId(),
      name: safeName,
      variables: parsedVariables,
      updatedAt: typeof updatedAt === 'string' ? updatedAt : now
    });
  });
  return sortTestCases(normalized);
};

const loadSavedFormulas = (): StoredFormula[] => {
  if (typeof window === 'undefined') {
    return [];
  }
  try {
    const raw = window.localStorage.getItem(FORMULA_STORAGE_KEY);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }
    const normalized: StoredFormula[] = [];
    parsed.forEach((entry) => {
      if (!entry || typeof entry !== 'object') {
        return;
      }
      const { id, name, expression, updatedAt, testCases } = entry as Partial<StoredFormula> & {
        testCases?: unknown;
      };
      if (typeof name !== 'string' || typeof expression !== 'string') {
        return;
      }
      normalized.push({
        id: typeof id === 'string' ? id : createFormulaId(),
        name,
        expression,
        updatedAt: typeof updatedAt === 'string' ? updatedAt : new Date().toISOString(),
        testCases: normalizeTestCases(testCases)
      });
    });
    return sortFormulas(normalized);
  } catch {
    return [];
  }
};

const persistSavedFormulas = (formulas: StoredFormula[]) => {
  if (typeof window === 'undefined') {
    return;
  }
  try {
    window.localStorage.setItem(FORMULA_STORAGE_KEY, JSON.stringify(formulas));
  } catch {
    // ignore
  }
};

const App = (): JSX.Element => {
  const initialFormulasRef = useRef<StoredFormula[] | null>(null);
  if (initialFormulasRef.current === null) {
    const stored = loadSavedFormulas();
    if (stored.length === 0) {
      const timestamp = new Date().toISOString();
      initialFormulasRef.current = [
        {
          id: createFormulaId(),
          name: DEFAULT_FORMULA_NAME,
          expression: DEFAULT_FORMULA,
          updatedAt: timestamp,
          testCases: [
            {
              id: createTestCaseId(),
              name: 'Baseline',
              variables: cloneVariables(DEFAULT_VARIABLES),
              updatedAt: timestamp
            }
          ]
        }
      ];
    } else {
      initialFormulasRef.current = stored;
    }
  }

  const initialFormulas = initialFormulasRef.current ?? [];
  const initialFormula = initialFormulas[0] ?? {
    id: createFormulaId(),
    name: DEFAULT_FORMULA_NAME,
    expression: DEFAULT_FORMULA,
    updatedAt: new Date().toISOString(),
    testCases: [
      {
        id: createTestCaseId(),
        name: 'Baseline',
        variables: cloneVariables(DEFAULT_VARIABLES),
        updatedAt: new Date().toISOString()
      }
    ]
  };
  const initialTestCases = initialFormula.testCases.length
    ? initialFormula.testCases
    : [
        {
          id: createTestCaseId(),
          name: 'Baseline',
          variables: cloneVariables(DEFAULT_VARIABLES),
          updatedAt: new Date().toISOString()
        }
      ];
  const initialTestCase = initialTestCases[0];

  const [savedFormulas, setSavedFormulas] = useState<StoredFormula[]>(initialFormulas);
  const [selectedFormulaId, setSelectedFormulaId] = useState<string>(initialFormula.id);
  const [expression, setExpression] = useState<string>(initialFormula.expression);
  const [savedTestCases, setSavedTestCases] = useState<StoredTestCase[]>(initialTestCases);
  const [selectedTestCaseId, setSelectedTestCaseId] = useState<string>(initialTestCase?.id ?? '');
  const [currentVariables, setCurrentVariables] = useState<FuncScriptTesterVariableInput[]>(
    cloneVariables(initialTestCase?.variables ?? DEFAULT_VARIABLES)
  );

  useEffect(() => {
    persistSavedFormulas(savedFormulas);
  }, [savedFormulas]);

  useEffect(() => {
    if (!selectedFormulaId) {
      return;
    }
    setSavedFormulas((prev) => {
      const index = prev.findIndex((formula) => formula.id === selectedFormulaId);
      if (index < 0) {
        return prev;
      }
      const formula = prev[index];
      const existing = JSON.stringify(
        formula.testCases.map((testCase) => ({
          name: testCase.name,
          variables: sanitizeVariables(testCase.variables)
        }))
      );
      const nextSignature = JSON.stringify(
        savedTestCases.map((testCase) => ({
          name: testCase.name,
          variables: sanitizeVariables(testCase.variables)
        }))
      );
      if (existing === nextSignature) {
        return prev;
      }
      const updated = [...prev];
      updated[index] = {
        ...formula,
        testCases: sortTestCases(savedTestCases)
      };
      return updated;
    });
  }, [savedTestCases, selectedFormulaId]);

  const selectedFormula = useMemo(
    () => savedFormulas.find((formula) => formula.id === selectedFormulaId) ?? null,
    [savedFormulas, selectedFormulaId]
  );

  const selectedTestCase = useMemo(
    () => savedTestCases.find((testCase) => testCase.id === selectedTestCaseId) ?? null,
    [savedTestCases, selectedTestCaseId]
  );

  const currentVariablesSignature = useMemo(
    () => createVariablesSignature(currentVariables),
    [currentVariables]
  );

  const selectedTestCaseSignature = useMemo(
    () => createVariablesSignature(selectedTestCase?.variables ?? []),
    [selectedTestCase]
  );

  const isDirty = useMemo(() => {
    if (!selectedFormula) {
      return expression.trim().length > 0;
    }
    return expression !== selectedFormula.expression;
  }, [expression, selectedFormula]);

  const isTestCaseDirty = useMemo(() => {
    if (!selectedTestCase) {
      return currentVariablesSignature !== EMPTY_VARIABLES_SIGNATURE;
    }
    return currentVariablesSignature !== selectedTestCaseSignature;
  }, [selectedTestCase, currentVariablesSignature, selectedTestCaseSignature]);

  const handleExpressionChange = useCallback((next: string) => {
    setExpression(next);
  }, []);

  const handleSelectFormula = useCallback(
    (event: ChangeEvent<HTMLSelectElement>) => {
      const nextId = event.target.value;
      if (!nextId) {
        return;
      }
      const match = savedFormulas.find((formula) => formula.id === nextId);
      if (!match) {
        return;
      }
      setSelectedFormulaId(match.id);
      setExpression(match.expression);
      const nextTestCases = match.testCases.length ? sortTestCases(match.testCases) : [];
      setSavedTestCases(nextTestCases);
      const nextSelected = nextTestCases[0];
      setSelectedTestCaseId(nextSelected?.id ?? '');
      setCurrentVariables(
        cloneVariables(nextSelected?.variables ?? (nextTestCases.length ? [] : DEFAULT_VARIABLES))
      );
    },
    [savedFormulas]
  );

  const handleSaveFormula = useCallback(() => {
    if (typeof window === 'undefined') {
      return;
    }
    const defaultName = selectedFormula?.name ?? '';
    const proposedName = window.prompt('Save formula as…', defaultName || 'New Formula');
    if (!proposedName) {
      return;
    }
    const trimmedName = proposedName.trim();
    if (!trimmedName) {
      return;
    }
    const timestamp = new Date().toISOString();
    const sortedCases = sortTestCases(savedTestCases);
    let resultingSelectionId = selectedFormulaId;

    setSavedFormulas((previous) => {
      const existingIndex = previous.findIndex(
        (formula) => formula.name.toLowerCase() === trimmedName.toLowerCase()
      );
      if (existingIndex >= 0) {
        const updated = [...previous];
        const original = updated[existingIndex];
        const updatedEntry: StoredFormula = {
          ...original,
          name: trimmedName,
          expression,
          updatedAt: timestamp,
          testCases: sortedCases
        };
        updated[existingIndex] = updatedEntry;
        resultingSelectionId = updatedEntry.id;
        return sortFormulas(updated);
      }
      const newEntry: StoredFormula = {
        id: createFormulaId(),
        name: trimmedName,
        expression,
        updatedAt: timestamp,
        testCases: sortedCases
      };
      resultingSelectionId = newEntry.id;
      return sortFormulas([...previous, newEntry]);
    });

    setSelectedFormulaId(resultingSelectionId);
  }, [expression, savedTestCases, selectedFormula, selectedFormulaId]);

  const handleVariablesChange = useCallback((next: FuncScriptTesterVariableInput[]) => {
    const sanitized = sanitizeVariables(next);
    const nextSignature = createVariablesSignature(sanitized);
    setCurrentVariables((prev) =>
      createVariablesSignature(prev) === nextSignature ? prev : sanitized
    );
  }, []);

  const handleSelectTestCase = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    const nextId = event.target.value;
    setSelectedTestCaseId(nextId);
  }, []);

  const handleApplyTestCase = useCallback(() => {
    if (!selectedTestCase) {
      return;
    }
    setCurrentVariables(cloneVariables(selectedTestCase.variables));
  }, [selectedTestCase]);

  const handleOverwriteTestCase = useCallback(() => {
    if (!selectedTestCase) {
      return;
    }
    const timestamp = new Date().toISOString();
    const nextVariables = cloneVariables(currentVariables);
    setSavedTestCases((previous) => {
      const index = previous.findIndex((testCase) => testCase.id === selectedTestCase.id);
      if (index < 0) {
        return previous;
      }
      const updated = [...previous];
      updated[index] = {
        ...previous[index],
        variables: nextVariables,
        updatedAt: timestamp
      };
      return sortTestCases(updated);
    });
  }, [currentVariables, selectedTestCase]);

  const handleSaveTestCase = useCallback(() => {
    if (typeof window === 'undefined') {
      return;
    }
    const defaultName = selectedTestCase?.name ? `${selectedTestCase.name} Copy` : 'New Test Case';
    const proposedName = window.prompt('Save test case as…', defaultName);
    if (!proposedName) {
      return;
    }
    const trimmedName = proposedName.trim();
    if (!trimmedName) {
      return;
    }
    const timestamp = new Date().toISOString();
    const newTestCase: StoredTestCase = {
      id: createTestCaseId(),
      name: trimmedName,
      variables: cloneVariables(currentVariables),
      updatedAt: timestamp
    };
    setSavedTestCases((previous) => sortTestCases([...previous, newTestCase]));
    setSelectedTestCaseId(newTestCase.id);
  }, [currentVariables, selectedTestCase]);

  const handleDeleteTestCase = useCallback(() => {
    if (!selectedTestCase) {
      return;
    }
    if (typeof window !== 'undefined') {
      const confirmed = window.confirm(`Delete the test case "${selectedTestCase.name}"?`);
      if (!confirmed) {
        return;
      }
    }
    setSavedTestCases((previous) => {
      const remaining = previous.filter((testCase) => testCase.id !== selectedTestCase.id);
      const sorted = sortTestCases(remaining);
      const nextSelected = sorted[0]?.id ?? '';
      setSelectedTestCaseId((current) => (current === selectedTestCase.id ? nextSelected : current));
      return sorted;
    });
  }, [selectedTestCase]);

  const testerSaveKey = useMemo(
    () => `funscript-studio:tester:${selectedFormulaId || 'default'}`,
    [selectedFormulaId]
  );

  return (
    <div className="app-root">
      <header className="app-header">
        <h1>FuncScript Tester</h1>
        <div className="formula-controls">
          <label className="formula-select-group" aria-label="Load saved formula">
            <span className="formula-label">Load</span>
            <select
              className="formula-select"
              value={selectedFormulaId}
              onChange={handleSelectFormula}
            >
              {savedFormulas.map((formula) => (
                <option key={formula.id} value={formula.id}>
                  {formula.name}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="save-button"
            onClick={handleSaveFormula}
            disabled={!isDirty}
          >
            <span className="save-icon" aria-hidden="true">
              <svg viewBox="0 0 20 20" focusable="false" role="img">
                <path
                  d="M4.75 2.5h8.69c.2 0 .39.08.53.22l3.31 3.31c.14.14.22.33.22.53v10.72c0 .83-.67 1.5-1.5 1.5H4.75c-.83 0-1.5-.67-1.5-1.5V4c0-.83.67-1.5 1.5-1.5Zm5.5 2.25H5.88c-.2 0-.38.16-.38.38v3.25c0 .21.17.37.38.37h4.37c.2 0 .37-.16.37-.37V5.13c0-.22-.17-.38-.37-.38Zm3.5 0H13c-.21 0-.38.17-.38.38v3.25c0 .21.17.37.38.37h.75c.41 0 .75-.34.75-.75V4.75c0-.41-.34-.75-.75-.75Zm-6.5 7.5c-.62 0-1.13.5-1.13 1.12v3.01c0 .62.51 1.12 1.13 1.12h4.5c.62 0 1.12-.5 1.12-1.12v-3c0-.63-.5-1.13-1.12-1.13h-4.5Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            <span>Save</span>
          </button>
        </div>
      </header>
      <div className="tester-wrapper">
        <div className="tester-shell">
          <FuncScriptTester
            value={expression}
            onChange={handleExpressionChange}
            saveKey={testerSaveKey}
            variables={currentVariables}
            onVariablesChange={handleVariablesChange}
          />
        </div>
        <aside className="testcase-panel">
          <div className="testcase-panel__header">
            <h2>Test Cases</h2>
            <p>Save reusable variable sets for this formula.</p>
          </div>
          <div className="testcase-panel__controls">
            <label className="testcase-select-group" aria-label="Select test case">
              <span className="testcase-label">Saved Cases</span>
              <select
                className="testcase-select"
                value={selectedTestCaseId}
                onChange={handleSelectTestCase}
              >
                <option value="">Unsaved variables</option>
                {savedTestCases.map((testCase) => (
                  <option key={testCase.id} value={testCase.id}>
                    {testCase.name}
                  </option>
                ))}
              </select>
            </label>
            <div className="testcase-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={handleApplyTestCase}
                disabled={!selectedTestCase}
              >
                Load
              </button>
              <button
                type="button"
                className="ghost-button"
                onClick={handleOverwriteTestCase}
                disabled={!selectedTestCase || !isTestCaseDirty}
              >
                Update
              </button>
              <button type="button" className="ghost-button" onClick={handleSaveTestCase}>
                Save As
              </button>
              <button
                type="button"
                className="ghost-button danger"
                onClick={handleDeleteTestCase}
                disabled={!selectedTestCase}
              >
                Delete
              </button>
            </div>
            <div className="testcase-status">
              {selectedTestCase ? (
                <span className={isTestCaseDirty ? 'status-badge warning' : 'status-badge success'}>
                  {isTestCaseDirty ? 'Modified' : 'In sync'}
                </span>
              ) : (
                <span className="status-badge neutral">Unsaved variables</span>
              )}
            </div>
          </div>
          <div className="testcase-panel__body">
            <h3>Current Variables</h3>
            {currentVariables.length === 0 ? (
              <p className="empty-hint">Variables will appear after the editor references them.</p>
            ) : (
              <ul className="variable-list">
                {currentVariables.map((variable, index) => (
                  <li key={`${variable.name || 'var'}-${index}`}>
                    <div className="variable-name">{variable.name || <em>Unnamed</em>}</div>
                    <code className="variable-expression">{variable.expression || '∅'}</code>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </aside>
      </div>
    </div>
  );
};

export default App;

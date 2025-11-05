import {
  ChangeEvent,
  FocusEvent,
  KeyboardEvent,
  TouchEvent as ReactTouchEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState
} from 'react';
import {
  applyTransformToPoints,
  defaultGraphicsExpression,
  defaultViewExpression,
  evaluateExpression,
  interpretGraphics,
  interpretView,
  prepareGraphics,
  prepareProvider,
  projectPointBuilder,
  transformCircleRadius,
  type EvaluationResult
} from './graphics';
import type { PreparedGraphics, PreparedPrimitive, PreparedTransform, ViewExtent } from './graphics';
import './App.css';
import { FuncScriptEditor } from '@tewelde/funcscript-editor';
import examples from './examples';

const MIN_LEFT_WIDTH = 260;
const MIN_RIGHT_WIDTH = 320;
const DEFAULT_RATIO = 0.45;
const BACKGROUND_COLOR = '#0f172a';
const GRID_COLOR = 'rgba(148, 163, 184, 0.2)';
const GRAPHICS_TAB_ID = 'graphics';
const VIEW_TAB_ID = 'view';

type CustomTabState = {
  id: string;
  name: string;
  expression: string;
};

const createCustomTabId = () => `custom-${Math.random().toString(36).slice(2, 10)}-${Date.now().toString(36)}`;

const isValidTabName = (name: string) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(name);

const buildDefaultTabName = (existingNames: Set<string>) => {
  let index = 1;
  let candidate = `model${index}`;
  while (existingNames.has(candidate.toLowerCase())) {
    index += 1;
    candidate = `model${index}`;
  }
  return candidate;
};

const getExpressionTabButtonId = (tabId: string) => {
  if (tabId === GRAPHICS_TAB_ID) {
    return 'graphics-expression-tab';
  }
  if (tabId === VIEW_TAB_ID) {
    return 'view-expression-tab';
  }
  return `custom-expression-tab-${tabId}`;
};

const getExpressionTabPanelId = (tabId: string) => {
  if (tabId === GRAPHICS_TAB_ID) {
    return 'graphics-expression-panel';
  }
  if (tabId === VIEW_TAB_ID) {
    return 'view-expression-panel';
  }
  return `custom-expression-panel-${tabId}`;
};

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

const drawScene = (
  canvas: HTMLCanvasElement,
  context: CanvasRenderingContext2D,
  extent: ViewExtent | null,
  graphics: PreparedGraphics,
  renderWarnings: string[],
  padding: number
) => {
  const { width: pixelWidth, height: pixelHeight } = canvas;
  context.setTransform(1, 0, 0, 1, 0, 0);
  context.clearRect(0, 0, pixelWidth, pixelHeight);
  context.fillStyle = BACKGROUND_COLOR;
  context.fillRect(0, 0, pixelWidth, pixelHeight);

  if (!extent || graphics.layers.length === 0) {
    return;
  }

  const projector = projectPointBuilder(extent, pixelWidth, pixelHeight, padding);
  const { project, scale } = projector;

  const applyStroke = (stroke: string | null, width: number) => {
    context.lineWidth = Math.max(1, width * scale);
    context.lineJoin = 'round';
    context.lineCap = 'round';
    context.strokeStyle = stroke ?? '#e2e8f0';
  };

  const drawAxes = () => {
    context.save();
    context.setLineDash([4, 6]);
    context.lineWidth = 1;
    context.strokeStyle = GRID_COLOR;
    context.beginPath();
    if (extent.minY <= 0 && extent.maxY >= 0) {
      const left = project([extent.minX, 0]);
      const right = project([extent.maxX, 0]);
      context.moveTo(left.x, left.y);
      context.lineTo(right.x, right.y);
    }
    if (extent.minX <= 0 && extent.maxX >= 0) {
      const bottom = project([0, extent.minY]);
      const top = project([0, extent.maxY]);
      context.moveTo(bottom.x, bottom.y);
      context.lineTo(top.x, top.y);
    }
    context.stroke();
    context.restore();
  };

  const drawPrimitive = (primitive: PreparedPrimitive) => {
    switch (primitive.type) {
      case 'line': {
        const startWorld = applyTransformToPoints([primitive.from], primitive.transform)[0];
        const endWorld = applyTransformToPoints([primitive.to], primitive.transform)[0];
        const start = project(startWorld);
        const end = project(endWorld);
        context.save();
        applyStroke(primitive.stroke, primitive.width);
        if (primitive.dash && primitive.dash.length > 0) {
          context.setLineDash(primitive.dash.map((segment) => Math.max(0, segment) * scale));
        } else {
          context.setLineDash([]);
        }
        context.beginPath();
        context.moveTo(start.x, start.y);
        context.lineTo(end.x, end.y);
        context.stroke();
        context.restore();
        break;
      }
      case 'rect': {
        const [x, y] = primitive.position;
        const [w, h] = primitive.size;
        const corners: Array<[number, number]> = [
          [x, y],
          [x + w, y],
          [x + w, y + h],
          [x, y + h]
        ];
        const worldCorners = applyTransformToPoints(corners, primitive.transform);
        const projected = worldCorners.map(project);
        context.save();
        context.beginPath();
        projected.forEach((point, index) => {
          if (index === 0) {
            context.moveTo(point.x, point.y);
          } else {
            context.lineTo(point.x, point.y);
          }
        });
        context.closePath();
        if (primitive.fill) {
          context.fillStyle = primitive.fill;
          context.fill();
        }
        if (primitive.stroke && primitive.width > 0) {
          applyStroke(primitive.stroke, primitive.width);
          context.stroke();
        }
        context.restore();
        break;
      }
      case 'circle': {
        const centerWorld = applyTransformToPoints([primitive.center], primitive.transform)[0];
        const radiusWorld = transformCircleRadius(
          primitive.radius,
          primitive.transform as PreparedTransform | null,
          renderWarnings,
          'circle'
        );
        const center = project(centerWorld);
        context.save();
        context.beginPath();
        context.arc(center.x, center.y, Math.max(0, radiusWorld * scale), 0, Math.PI * 2);
        if (primitive.fill) {
          context.fillStyle = primitive.fill;
          context.fill();
        }
        if (primitive.stroke && primitive.width > 0) {
          applyStroke(primitive.stroke, primitive.width);
          context.stroke();
        }
        context.restore();
        break;
      }
      case 'polygon': {
        const worldPoints = applyTransformToPoints(primitive.points, primitive.transform);
        if (worldPoints.length < 3) {
          return;
        }
        const projected = worldPoints.map(project);
        context.save();
        context.beginPath();
        projected.forEach((point, index) => {
          if (index === 0) {
            context.moveTo(point.x, point.y);
          } else {
            context.lineTo(point.x, point.y);
          }
        });
        context.closePath();
        if (primitive.fill) {
          context.fillStyle = primitive.fill;
          context.fill();
        }
        if (primitive.stroke && primitive.width > 0) {
          applyStroke(primitive.stroke, primitive.width);
          context.stroke();
        }
        context.restore();
        break;
      }
      case 'text': {
        const worldPos = applyTransformToPoints([primitive.position], primitive.transform)[0];
        const projected = project(worldPos);
        const transform = primitive.transform;
        let fontScale = 1;
        if (transform) {
          const [sx, sy] = transform.scale;
          fontScale = Math.abs(sx + sy) / 2;
          if (Math.abs(sx - sy) > 1e-4) {
            renderWarnings.push('Text transform uses non-uniform scale; averaging factors.');
          }
        }
        context.save();
        context.fillStyle = primitive.color;
        context.textAlign = primitive.align;
        context.textBaseline = 'middle';
        context.font = `${Math.max(12, primitive.fontSize * fontScale * scale)}px "Inter", "Roboto", sans-serif`;
        context.fillText(primitive.text, projected.x, projected.y);
        context.restore();
        break;
      }
      default:
        break;
    }
  };

  drawAxes();

  for (const layer of graphics.layers) {
    for (const primitive of layer) {
      drawPrimitive(primitive);
    }
  }

};

const App = (): JSX.Element => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canvasWrapperRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  const initialExample = examples.length > 0 ? examples[0] : null;

  const [leftWidth, setLeftWidth] = useState(() => {
    if (typeof window === 'undefined') {
      return 420;
    }
    return Math.round(window.innerWidth * DEFAULT_RATIO) || 420;
  });
  const [dragging, setDragging] = useState(false);
  const [viewExpression, setViewExpression] = useState(
    initialExample ? initialExample.view : defaultViewExpression
  );
  const [graphicsExpression, setGraphicsExpression] = useState(
    initialExample ? initialExample.graphics : defaultGraphicsExpression
  );
  const [activeExpressionTab, setActiveExpressionTab] = useState<string>('graphics');
  const [customTabs, setCustomTabs] = useState<CustomTabState[]>([]);
  const [draftCustomTabs, setDraftCustomTabs] = useState<CustomTabState[] | null>(null);
  const [tabNameDraft, setTabNameDraft] = useState<string | null>(null);
  const [tabNameDraftError, setTabNameDraftError] = useState<string | null>(null);
  const newTabInputRef = useRef<HTMLInputElement | null>(null);
  const [pendingFocusEditorId, setPendingFocusEditorId] = useState<string | null>(null);
  const newTabInputPrimedRef = useRef(false);
  const draftCommittedRef = useRef(false);

  useEffect(() => {
    if (tabNameDraft !== null) {
      if (!newTabInputPrimedRef.current && newTabInputRef.current) {
        newTabInputPrimedRef.current = true;
        newTabInputRef.current.focus();
        newTabInputRef.current.select();
      }
    } else {
      newTabInputPrimedRef.current = false;
    }
  }, [tabNameDraft]);

  useEffect(() => {
    if (!pendingFocusEditorId) {
      return;
    }
    if (typeof document === 'undefined') {
      return;
    }
    const selector = `[data-editor-id="${pendingFocusEditorId}"] .cm-content`;
    const editorElement = document.querySelector(selector);
    if (editorElement instanceof HTMLElement) {
      editorElement.focus();
    }
    setPendingFocusEditorId(null);
  }, [pendingFocusEditorId]);
  const [renderWarnings, setRenderWarnings] = useState<string[]>([]);
  const [canvasSize, setCanvasSize] = useState<{ cssWidth: number; cssHeight: number; dpr: number }>(
    () => ({ cssWidth: 0, cssHeight: 0, dpr: 1 })
  );
  const [time, setTime] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [selectedExampleId, setSelectedExampleId] = useState<string>(
    initialExample ? initialExample.id : 'custom'
  );

  const applyWidthFromClientX = useCallback(
    (clientX: number) => {
      const container = containerRef.current;
      if (!container) {
        return;
      }
      const rect = container.getBoundingClientRect();
      const clampedLeft = clamp(
        clientX - rect.left,
        MIN_LEFT_WIDTH,
        Math.max(MIN_LEFT_WIDTH, rect.width - MIN_RIGHT_WIDTH)
      );
      setLeftWidth(Math.round(clampedLeft));
    },
    []
  );

  const stopDragging = useCallback(() => {
    setDragging(false);
  }, []);

  useEffect(() => {
    if (!dragging) {
      document.body.style.removeProperty('cursor');
      document.body.style.removeProperty('user-select');
      return;
    }

    const handleMouseMove = (event: MouseEvent) => {
      applyWidthFromClientX(event.clientX);
    };

    const handleTouchMove = (event: TouchEvent) => {
      if (event.touches.length > 0) {
        applyWidthFromClientX(event.touches[0].clientX);
      }
    };

    const handlePointerUp = () => {
      stopDragging();
    };

    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('touchmove', handleTouchMove, { passive: false });
    window.addEventListener('mouseup', handlePointerUp);
    window.addEventListener('touchend', handlePointerUp);
    window.addEventListener('touchcancel', handlePointerUp);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('touchmove', handleTouchMove);
      window.removeEventListener('mouseup', handlePointerUp);
      window.removeEventListener('touchend', handlePointerUp);
      window.removeEventListener('touchcancel', handlePointerUp);
      document.body.style.removeProperty('cursor');
      document.body.style.removeProperty('user-select');
    };
  }, [applyWidthFromClientX, dragging, stopDragging]);

  const handleSplitterMouseDown = useCallback(() => {
    setDragging(true);
  }, []);

  const handleSplitterTouchStart = useCallback((event: ReactTouchEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(true);
    if (event.touches.length > 0) {
      applyWidthFromClientX(event.touches[0].clientX);
    }
  }, [applyWidthFromClientX]);

  const handleExampleSelect = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    setSelectedExampleId(event.target.value);
  }, []);

  const handleViewExpressionChange = useCallback(
    (value: string) => {
      setViewExpression(value);
      if (selectedExampleId !== 'custom') {
        setSelectedExampleId('custom');
      }
    },
    [selectedExampleId]
  );

  const handleGraphicsExpressionChange = useCallback(
    (value: string) => {
      setGraphicsExpression(value);
      if (selectedExampleId !== 'custom') {
        setSelectedExampleId('custom');
      }
    },
    [selectedExampleId]
  );

  const handleExpressionTabSelect = useCallback((tabId: string) => {
    setActiveExpressionTab(tabId);
  }, []);

  const handleCustomTabExpressionChange = useCallback((tabId: string, next: string) => {
    setDraftCustomTabs((current) => {
      const target = current ?? customTabs;
      return target.map((tab) => (tab.id === tabId ? { ...tab, expression: next } : tab));
    });
  }, [customTabs]);

  const handleCustomTabExpressionBlur = useCallback(
    (event: FocusEvent<HTMLDivElement>) => {
      const { currentTarget, relatedTarget } = event;
      if (relatedTarget instanceof Node && currentTarget.contains(relatedTarget)) {
        return;
      }
      if (draftCustomTabs) {
        setCustomTabs(draftCustomTabs);
      }
    },
    [draftCustomTabs]
  );

  const handleAddTabClick = useCallback(() => {
    if (tabNameDraft !== null) {
      if (newTabInputRef.current) {
        newTabInputRef.current.focus();
        newTabInputRef.current.select();
      }
      return;
    }
    const existingNames = new Set<string>([GRAPHICS_TAB_ID, VIEW_TAB_ID]);
    for (const tab of customTabs) {
      existingNames.add(tab.name.toLowerCase());
    }
    const defaultName = buildDefaultTabName(existingNames);
    setTabNameDraft(defaultName);
    setTabNameDraftError(null);
    draftCommittedRef.current = false;
  }, [customTabs, tabNameDraft]);

  const commitCustomTabDraft = useCallback(
    (rawName?: string) => {
      if (tabNameDraft === null) {
        return false;
      }
      const inputName = rawName ?? tabNameDraft;
      const trimmedName = inputName.trim();
      if (!trimmedName) {
        setTabNameDraftError('Name is required.');
        return false;
      }
      if (!isValidTabName(trimmedName)) {
        setTabNameDraftError('Use letters, digits, or underscores; start with a letter or underscore.');
        return false;
      }
      const lowerName = trimmedName.toLowerCase();
      const existingNames = new Set<string>([GRAPHICS_TAB_ID, VIEW_TAB_ID]);
      for (const tab of customTabs) {
        existingNames.add(tab.name.toLowerCase());
      }
      if (existingNames.has(lowerName)) {
        setTabNameDraftError('That name is already in use.');
        return false;
      }

      const newTab: CustomTabState = {
        id: createCustomTabId(),
        name: trimmedName,
        expression: '{\n  return 0;\n}'
      };
      setCustomTabs((current) => [...current, newTab]);
      setActiveExpressionTab(newTab.id);
      setTabNameDraft(null);
      setTabNameDraftError(null);
      setPendingFocusEditorId(newTab.id);
      draftCommittedRef.current = true;
      return true;
    },
    [customTabs, tabNameDraft]
  );

  const handleTabNameDraftChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setTabNameDraft(event.target.value);
    setTabNameDraftError(null);
    draftCommittedRef.current = false;
  }, []);

  const handleTabNameDraftKeyDown = useCallback(
    (event: KeyboardEvent<HTMLInputElement>) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        const committed = commitCustomTabDraft(event.currentTarget.value);
        if (committed && event.currentTarget) {
          event.currentTarget.blur();
        }
      } else if (event.key === 'Escape') {
        event.preventDefault();
        setTabNameDraft(null);
        setTabNameDraftError(null);
      }
    },
    [commitCustomTabDraft]
  );

  const handleTabNameDraftBlur = useCallback(
    (event: FocusEvent<HTMLInputElement>) => {
      if (draftCommittedRef.current) {
        draftCommittedRef.current = false;
        return;
      }
      const committed = commitCustomTabDraft(event.currentTarget.value);
      if (!committed) {
        requestAnimationFrame(() => {
          if (newTabInputRef.current) {
            newTabInputRef.current.focus();
            newTabInputRef.current.select();
          }
        });
      }
    },
    [commitCustomTabDraft]
  );

  const handleSplitterKeyDown = useCallback((event: KeyboardEvent<HTMLDivElement>) => {
    const container = containerRef.current;
    const containerWidth = container ? container.getBoundingClientRect().width : window.innerWidth;
    const maxLeft = Math.max(MIN_LEFT_WIDTH, containerWidth - MIN_RIGHT_WIDTH);
    const step = event.shiftKey ? 32 : 12;
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      setLeftWidth((current) => clamp(current - step, MIN_LEFT_WIDTH, maxLeft));
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      setLeftWidth((current) => clamp(current + step, MIN_LEFT_WIDTH, maxLeft));
    }
    if (event.key === 'Home') {
      event.preventDefault();
      setLeftWidth(MIN_LEFT_WIDTH);
    }
    if (event.key === 'End') {
      event.preventDefault();
      setLeftWidth(maxLeft);
    }
  }, []);

  useEffect(() => {
    setDraftCustomTabs(null);
  }, [customTabs]);

  useEffect(() => {
    const handleResize = () => {
      const container = containerRef.current;
      if (!container) {
        return;
      }
      const rect = container.getBoundingClientRect();
      const maxLeft = Math.max(MIN_LEFT_WIDTH, rect.width - MIN_RIGHT_WIDTH);
      setLeftWidth((width) => clamp(width, MIN_LEFT_WIDTH, maxLeft));
    };

    handleResize();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);
  const evaluationState = useMemo(() => {
    const provider = prepareProvider();
    provider.set('t', time);

    const evaluationMap = new Map<string, EvaluationResult>();

    for (const tab of customTabs) {
      const result = evaluateExpression(provider, tab.expression);
      evaluationMap.set(tab.id, result);
      if (result.typed) {
        provider.set(tab.name, result.typed);
      }
    }

    const viewResult = evaluateExpression(provider, viewExpression);
    const graphicsResult = evaluateExpression(provider, graphicsExpression);

    return {
      customEvaluations: evaluationMap,
      viewEvaluation: viewResult,
      graphicsEvaluation: graphicsResult
    };
  }, [customTabs, graphicsExpression, time, viewExpression]);

  const customTabEvaluations = evaluationState.customEvaluations;
  const viewEvaluation = evaluationState.viewEvaluation;
  const graphicsEvaluation = evaluationState.graphicsEvaluation;

  const viewInterpretation = useMemo(() => interpretView(viewEvaluation.value), [viewEvaluation.value]);

  const graphicsInterpretation = useMemo(() => interpretGraphics(graphicsEvaluation.value), [
    graphicsEvaluation.value
  ]);

  const preparedGraphics = useMemo(
    () => prepareGraphics(viewInterpretation.extent, graphicsInterpretation.layers),
    [viewInterpretation.extent, graphicsInterpretation.layers]
  );

  const drawStateRef = useRef<{ preparedGraphics: PreparedGraphics; extent: ViewExtent | null }>({
    preparedGraphics,
    extent: viewInterpretation.extent
  });
  const rafRef = useRef<number | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const lastTimestampRef = useRef<number | null>(null);
  const playingRef = useRef(false);

  const requestFrame = useCallback((force?: boolean) => {
    if (rafRef.current !== null) {
      if (force) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      } else {
        return;
      }
    }
    rafRef.current = window.requestAnimationFrame(() => {
      rafRef.current = null;
      const canvas = canvasRef.current;
      if (!canvas) {
        return;
      }
      const context = canvas.getContext('2d');
      if (!context) {
        return;
      }
      const { preparedGraphics: latestGraphics, extent } = drawStateRef.current;
      const warnings: string[] = [];
      drawScene(canvas, context, extent, latestGraphics, warnings, 48);
      setRenderWarnings(warnings);
    });
  }, [setRenderWarnings]);

  const drawImmediate = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const context = canvas.getContext('2d');
    if (!context) {
      return;
    }
    const { preparedGraphics: latestGraphics, extent } = drawStateRef.current;
    const warnings: string[] = [];
    drawScene(canvas, context, extent, latestGraphics, warnings, 48);
    setRenderWarnings(warnings);
  }, [setRenderWarnings]);

  const animationLoop = useCallback(
    function animationLoop(timestamp: number) {
      if (!playingRef.current) {
        animationFrameRef.current = null;
        return;
      }
      if (lastTimestampRef.current === null) {
        lastTimestampRef.current = timestamp;
      }
      const deltaSeconds = (timestamp - lastTimestampRef.current) / 1000;
      lastTimestampRef.current = timestamp;
      setTime((previous) => previous + deltaSeconds);
      animationFrameRef.current = window.requestAnimationFrame(animationLoop);
    },
    []
  );

  const stopAnimation = useCallback(() => {
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    lastTimestampRef.current = null;
  }, []);

  const handlePlay = useCallback(() => {
    if (playingRef.current) {
      return;
    }
    playingRef.current = true;
    setIsPlaying(true);
    lastTimestampRef.current = null;
    animationFrameRef.current = window.requestAnimationFrame(animationLoop);
  }, [animationLoop]);

  const handlePause = useCallback(() => {
    if (!playingRef.current) {
      return;
    }
    playingRef.current = false;
    setIsPlaying(false);
    stopAnimation();
  }, [stopAnimation]);

  const handleReset = useCallback(() => {
    playingRef.current = false;
    setIsPlaying(false);
    stopAnimation();
    setTime(0);
    drawImmediate();
  }, [stopAnimation, drawImmediate]);

  useEffect(() => {
    if (selectedExampleId === 'custom') {
      return;
    }
    const example = examples.find((entry) => entry.id === selectedExampleId);
    if (!example) {
      return;
    }
    playingRef.current = false;
    setIsPlaying(false);
    stopAnimation();
    setTime(0);
    setViewExpression(example.view);
    setGraphicsExpression(example.graphics);
  }, [selectedExampleId, stopAnimation]);

  useEffect(() => {
    drawStateRef.current = {
      preparedGraphics,
      extent: viewInterpretation.extent
    };
    if (canvasRef.current) {
      requestFrame(true);
      drawImmediate();
    }
  }, [preparedGraphics, viewInterpretation.extent, requestFrame, drawImmediate]);

  useEffect(() => () => {
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
    }
  }, []);

  useEffect(() => () => {
    stopAnimation();
  }, [stopAnimation]);

  useEffect(() => {
    const wrapper = canvasWrapperRef.current;
    const canvas = canvasRef.current;
    if (!wrapper || !canvas) {
      return;
    }

    const updateCanvasSize = () => {
      const rect = wrapper.getBoundingClientRect();
      const cssWidth = Math.max(1, rect.width);
      const cssHeight = Math.max(1, rect.height);
      const dpr = window.devicePixelRatio || 1;
      const pixelWidth = Math.max(1, Math.round(cssWidth * dpr));
      const pixelHeight = Math.max(1, Math.round(cssHeight * dpr));

      if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
      }
      if (canvas.style.width !== `${cssWidth}px`) {
        canvas.style.width = `${cssWidth}px`;
      }
      if (canvas.style.height !== `${cssHeight}px`) {
        canvas.style.height = `${cssHeight}px`;
      }

      setCanvasSize((current) => {
        if (
          Math.abs(current.cssWidth - cssWidth) < 0.5 &&
          Math.abs(current.cssHeight - cssHeight) < 0.5 &&
          current.dpr === dpr
        ) {
          return current;
        }
        return { cssWidth, cssHeight, dpr };
      });

      requestFrame();
    };

    updateCanvasSize();

    const observer = new ResizeObserver(() => {
      updateCanvasSize();
    });
    observer.observe(wrapper);

    return () => {
      observer.disconnect();
    };
  }, [requestFrame]);

  const unknownTypesWarning =
    graphicsInterpretation.unknownTypes.length > 0
      ? `Unknown primitive type(s): ${graphicsInterpretation.unknownTypes.join(', ')}`
      : null;

  const totalPrimitives = useMemo(() => {
    if (preparedGraphics.layers.length === 0) {
      return 0;
    }
    return preparedGraphics.layers.reduce((sum, layer) => sum + layer.length, 0);
  }, [preparedGraphics.layers]);

  const canvasReady =
    canvasSize.cssWidth > 0 &&
    canvasSize.cssHeight > 0 &&
    preparedGraphics.layers.length > 0 &&
    viewInterpretation.extent !== null;

  const isGraphicsTabActive = activeExpressionTab === GRAPHICS_TAB_ID;
  const isViewTabActive = activeExpressionTab === VIEW_TAB_ID;

  return (
    <div ref={containerRef} className="app" aria-label="FuncScript graphics workspace">
      <section className="panel panel-left" style={{ width: `${leftWidth}px` }}>
        <header className="panel-heading">Preview</header>
        <div className="panel-body panel-body-left">
          <div className="panel-header-controls">
            <div className="panel-meta">
              <span>Primitives: {totalPrimitives}</span>
              <span>
                Canvas: {Math.round(canvasSize.cssWidth)}px × {Math.round(canvasSize.cssHeight)}px @ {canvasSize.dpr.toFixed(2)}x
              </span>
              <span>
                View span:{' '}
                {viewInterpretation.extent
                  ? `${(viewInterpretation.extent.maxX - viewInterpretation.extent.minX).toFixed(2)} × ${(viewInterpretation.extent.maxY - viewInterpretation.extent.minY).toFixed(2)}`
                  : '—'}
              </span>
            </div>
            <div className="animation-controls">
              <span className="time-display">t = {time.toFixed(2)}s</span>
              <div className="animation-buttons">
                <button
                  type="button"
                  className="control-button"
                  onClick={handlePlay}
                  disabled={isPlaying}
                  aria-label="Play"
                >
                  ▶
                </button>
                <button
                  type="button"
                  className="control-button"
                  onClick={handlePause}
                  disabled={!isPlaying}
                  aria-label="Pause"
                >
                  ⏸
                </button>
                <button
                  type="button"
                  className="control-button"
                  onClick={handleReset}
                  aria-label="Reset"
                >
                  ⟲
                </button>
              </div>
            </div>
          </div>

          <div ref={canvasWrapperRef} className="canvas-wrapper">
            <canvas ref={canvasRef} className="preview-canvas" />
            {!canvasReady ? (
              <div className="canvas-notice">
                <p>Awaiting view extent and primitive output.</p>
              </div>
            ) : null}
          </div>
        </div>
      </section>

      <div
        role="separator"
        aria-orientation="vertical"
        aria-label="Resize panels"
        tabIndex={0}
        className={`splitter${dragging ? ' splitter-dragging' : ''}`}
        onMouseDown={handleSplitterMouseDown}
        onTouchStart={handleSplitterTouchStart}
        onKeyDown={handleSplitterKeyDown}
      />

      <section className="panel panel-right">
        <header className="panel-heading">Expressions</header>
        <div className="panel-body panel-body-right">
          <div className="example-picker">
            <label htmlFor="example-select" className="input-label">
              Example
            </label>
            <select
              id="example-select"
              className="example-select"
              value={selectedExampleId}
              onChange={handleExampleSelect}
            >
              <option value="custom">Custom</option>
              {examples.map((example) => (
                <option key={example.id} value={example.id}>
                  {example.name}
                </option>
              ))}
            </select>
          </div>

          <div className="expression-tabs">
            <div className="expression-tabs-header">
              <div
                className="expression-tabs-list"
                role="tablist"
                aria-label="Expression editors"
              >
                <button
                  type="button"
                  role="tab"
                  id={getExpressionTabButtonId(GRAPHICS_TAB_ID)}
                  aria-controls={getExpressionTabPanelId(GRAPHICS_TAB_ID)}
                  aria-selected={isGraphicsTabActive}
                  tabIndex={isGraphicsTabActive ? 0 : -1}
                  className={`expression-tab${isGraphicsTabActive ? ' expression-tab-active' : ''}`}
                  onClick={() => handleExpressionTabSelect(GRAPHICS_TAB_ID)}
                >
                  Graphics
                </button>
                <button
                  type="button"
                  role="tab"
                  id={getExpressionTabButtonId(VIEW_TAB_ID)}
                  aria-controls={getExpressionTabPanelId(VIEW_TAB_ID)}
                  aria-selected={isViewTabActive}
                  tabIndex={isViewTabActive ? 0 : -1}
                  className={`expression-tab${isViewTabActive ? ' expression-tab-active' : ''}`}
                  onClick={() => handleExpressionTabSelect(VIEW_TAB_ID)}
                >
                  View extent
                </button>
                {customTabs.map((tab) => {
                  const customActive = activeExpressionTab === tab.id;
                  const evaluation = customTabEvaluations.get(tab.id);
                  const hasError = Boolean(evaluation?.error);
                  const className = [
                    'expression-tab',
                    customActive ? 'expression-tab-active' : '',
                    hasError ? 'expression-tab-error-state' : ''
                  ]
                    .filter(Boolean)
                    .join(' ');
                  return (
                    <button
                      key={tab.id}
                      type="button"
                      role="tab"
                      id={getExpressionTabButtonId(tab.id)}
                      aria-controls={getExpressionTabPanelId(tab.id)}
                      aria-selected={customActive}
                      tabIndex={customActive ? 0 : -1}
                      className={className}
                      onClick={() => handleExpressionTabSelect(tab.id)}
                    >
                      {tab.name}
                    </button>
                  );
                })}
                {tabNameDraft !== null ? (
                  <input
                    ref={newTabInputRef}
                    className="expression-tab-input"
                    value={tabNameDraft}
                    onChange={handleTabNameDraftChange}
                    onKeyDown={handleTabNameDraftKeyDown}
                    onBlur={handleTabNameDraftBlur}
                    aria-label="New tab name"
                  />
                ) : (
                  <button
                    type="button"
                    className="expression-tab expression-tab-add"
                    onClick={handleAddTabClick}
                    aria-label="Add expression tab"
                  >
                    +
                  </button>
                )}
              </div>
              {tabNameDraftError ? (
                <p className="expression-tab-error" role="alert">
                  {tabNameDraftError}
                </p>
              ) : null}
            </div>
            <div className="expression-tab-panels">
              <div
                role="tabpanel"
                id={getExpressionTabPanelId(GRAPHICS_TAB_ID)}
                aria-labelledby={getExpressionTabButtonId(GRAPHICS_TAB_ID)}
                hidden={activeExpressionTab !== GRAPHICS_TAB_ID}
                className="expression-tab-panel"
              >
                <label className="input-label" htmlFor="graphics-expression-editor">
                  Graphics expression
                </label>
                <div
                  className="editor-container editor-container-fill"
                  data-editor-id={GRAPHICS_TAB_ID}
                  id="graphics-expression-editor"
                >
                  <FuncScriptEditor
                    value={graphicsExpression}
                    onChange={handleGraphicsExpressionChange}
                    minHeight={0}
                    style={{ flex: 1 }}
                  />
                </div>
                <StatusMessage
                  error={graphicsEvaluation.error}
                  warning={graphicsInterpretation.warning ?? unknownTypesWarning}
                  info={preparedGraphics.warnings.concat(renderWarnings)}
                  success={preparedGraphics.layers.length > 0 ? 'Graphics ready.' : null}
                />
              </div>
              <div
                role="tabpanel"
                id={getExpressionTabPanelId(VIEW_TAB_ID)}
                aria-labelledby={getExpressionTabButtonId(VIEW_TAB_ID)}
                hidden={activeExpressionTab !== VIEW_TAB_ID}
                className="expression-tab-panel"
              >
                <label className="input-label" htmlFor="view-expression-editor">
                  View extent expression
                </label>
                <div
                  className="editor-container editor-container-fill"
                  data-editor-id={VIEW_TAB_ID}
                  id="view-expression-editor"
                >
                  <FuncScriptEditor
                    value={viewExpression}
                    onChange={handleViewExpressionChange}
                    minHeight={0}
                    style={{ flex: 1 }}
                  />
                </div>
                <StatusMessage
                  error={viewEvaluation.error}
                  warning={viewInterpretation.warning}
                  success={viewInterpretation.extent ? 'Extent ready.' : null}
                />
              </div>
              {customTabs.map((tab) => {
                const panelId = getExpressionTabPanelId(tab.id);
                const buttonId = getExpressionTabButtonId(tab.id);
                const evaluation = customTabEvaluations.get(tab.id);
                const draftTab =
                  draftCustomTabs?.find((draft) => draft.id === tab.id) ?? tab;
                return (
                  <div
                    key={tab.id}
                    role="tabpanel"
                    id={panelId}
                    aria-labelledby={buttonId}
                    hidden={activeExpressionTab !== tab.id}
                    className="expression-tab-panel"
                  >
                    <label className="input-label" htmlFor={`custom-expression-editor-${tab.id}`}>
                      {tab.name} expression
                    </label>
                    <div
                      className="editor-container editor-container-fill"
                      data-editor-id={tab.id}
                      id={`custom-expression-editor-${tab.id}`}
                      onBlur={handleCustomTabExpressionBlur}
                    >
                      <FuncScriptEditor
                        value={draftTab.expression}
                        onChange={(value: string) => handleCustomTabExpressionChange(tab.id, value)}
                        minHeight={0}
                        style={{ flex: 1 }}
                      />
                    </div>
                    <StatusMessage
                      error={evaluation?.error ?? null}
                      success={!evaluation?.error ? 'Value ready.' : null}
                    />
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};

const StatusMessage = ({
  error,
  warning,
  info,
  success
}: {
  error?: string | null;
  warning?: string | null;
  info?: string | string[] | null;
  success?: string | null;
}): JSX.Element | null => {
  if (error) {
    return <p className="status status-error">{error}</p>;
  }
  if (warning) {
    return <p className="status status-warning">{warning}</p>;
  }
  if (info && Array.isArray(info) && info.length > 0) {
    return (
      <ul className="status status-info">
        {info.map((entry, index) => (
          <li key={index}>{entry}</li>
        ))}
      </ul>
    );
  }
  if (info && typeof info === 'string') {
    return <p className="status status-info">{info}</p>;
  }
  if (success) {
    return <p className="status status-success">{success}</p>;
  }
  return null;
};

export default App;

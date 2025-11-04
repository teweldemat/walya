import { SyntheticEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { MouseEvent as ReactMouseEvent, TouchEvent as ReactTouchEvent } from 'react';
import {
  Alert,
  AppBar,
  Box,
  Chip,
  Container,
  CssBaseline,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Tab,
  Tabs,
  Stack,
  Toolbar,
  Typography
} from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import {
  Engine,
  type TypedValue,
  FsList,
  KeyValueCollection,
  FsError,
  FSDataType,
  DefaultFsDataProvider
} from '@tewelde/funcscript/browser';
import { FuncScriptEditor } from '@tewelde/funcscript-editor';
import { theme } from './theme';

type PrimitiveType = 'line' | 'rect' | 'circle' | 'polygon' | 'text';

type Primitive = {
  type: string;
  data: Record<string, unknown>;
};

type PrimitiveLayer = Primitive[];

type ViewExtent = {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
};

type PreparedLine = {
  type: 'line';
  from: [number, number];
  to: [number, number];
  stroke: string;
  width: number;
  dash: number[] | null;
};

type PreparedRect = {
  type: 'rect';
  position: [number, number];
  size: [number, number];
  stroke: string | null;
  fill: string | null;
  width: number;
};

type PreparedCircle = {
  type: 'circle';
  center: [number, number];
  radius: number;
  stroke: string | null;
  fill: string | null;
  width: number;
};

type PreparedPolygon = {
  type: 'polygon';
  points: Array<[number, number]>;
  stroke: string | null;
  fill: string | null;
  width: number;
};

type PreparedText = {
  type: 'text';
  position: [number, number];
  text: string;
  color: string;
  fontSize: number;
  align: CanvasTextAlign;
};

type PreparedPrimitive =
  | PreparedLine
  | PreparedRect
  | PreparedCircle
  | PreparedPolygon
  | PreparedText;

type PreparedLayers = PreparedPrimitive[][];

type PrimitiveDefinition = {
  type: PrimitiveType;
  label: string;
  description: string;
  structure: Primitive;
  notes?: string[];
};

type EvaluationResult = {
  value: unknown;
  typed: TypedValue | null;
  error: string | null;
};

type ViewInterpretation = {
  extent: ViewExtent | null;
  warning: string | null;
};

type GraphicsInterpretation = {
  layers: PrimitiveLayer[] | null;
  warning: string | null;
  unknownTypes: string[];
};

type PreparedGraphics = {
  layers: PreparedLayers;
  warnings: string[];
};

const defaultViewExpression = `{
  return { minX:-10, minY:-10, maxX:10, maxY:10 };
}`;

const defaultGraphicsExpression = `{
  baseColor:'#38bdf8';
  accentColor:'#f97316';
  layer1:[
    {
      type:'line',
      data:{
        from:[-8,-8],
        to:[8,8],
        stroke:baseColor,
        width:0.35
      }
    },
    {
      type:'line',
      data:{
        from:[-8,8],
        to:[8,-8],
        stroke:baseColor,
        width:0.35,
        dash:[1,0.6]
      }
    },
    {
      type:'polygon',
      data:{
        points:[[-6,2],[-2,6],[2,6],[6,2],[2,-6],[-2,-6]],
        stroke:'#e0f2fe',
        width:0.25
      }
    }
  ];
  layer2:[
    {
      type:'circle',
      data:{
        center:[0,0],
        radius:4.5,
        stroke:'#1f2937',
        fill:'rgba(15,23,42,0.35)',
        width:0.3
      }
    },
    {
      type:'rect',
      data:{
        position:[-6,-1.5],
        size:[12,3],
        fill:'rgba(15,23,42,0.65)',
        stroke:accentColor,
        width:0.25
      }
    },
    {
      type:'text',
      data:{
        position:[0,-0.2],
        text:'FuncScript',
        color:'#f8fafc',
        fontSize:1.6,
        align:'center'
      }
    }
  ];
  return [layer1, layer2];
}`;

const PRIMITIVE_DEFINITIONS: PrimitiveDefinition[] = [
  {
    type: 'line',
    label: 'Line',
    description: 'Straight segment connecting two points.',
    structure: {
      type: 'line',
      data: {
        from: [-5, -5],
        to: [5, 5],
        stroke: '#38bdf8',
        width: 0.4,
        dash: [1, 0.5]
      }
    },
    notes: ['Stroke width and dash lengths are expressed in world units.']
  },
  {
    type: 'rect',
    label: 'Rectangle',
    description: 'Axis-aligned rectangle defined by bottom-left position and size.',
    structure: {
      type: 'rect',
      data: {
        position: [-4, -2],
        size: [8, 4],
        fill: 'rgba(59, 130, 246, 0.25)',
        stroke: '#38bdf8',
        width: 0.3
      }
    },
    notes: ['Position uses the world coordinate system. Size is [width, height].']
  },
  {
    type: 'circle',
    label: 'Circle',
    description: 'Circle defined by center and radius.',
    structure: {
      type: 'circle',
      data: {
        center: [0, 0],
        radius: 4,
        fill: 'rgba(14, 165, 233, 0.2)',
        stroke: '#0ea5e9',
        width: 0.25
      }
    },
    notes: ['Radius is measured in world units.']
  },
  {
    type: 'polygon',
    label: 'Polygon',
    description: 'Closed shape defined by an ordered set of points.',
    structure: {
      type: 'polygon',
      data: {
        points: [[-4, -2], [-2, 3], [2, 3], [4, -2]],
        fill: 'rgba(236, 72, 153, 0.18)',
        stroke: '#f472b6',
        width: 0.3
      }
    },
    notes: ['Provide three or more points. The path is closed automatically.']
  },
  {
    type: 'text',
    label: 'Text',
    description: 'Renders text anchored at the provided position.',
    structure: {
      type: 'text',
      data: {
        position: [0, 0],
        text: 'Hello',
        color: '#f8fafc',
        fontSize: 1,
        align: 'center'
      }
    },
    notes: ['Font size is expressed in world units and scales with the current extent.']
  }
];

const toPlainValue = (value: TypedValue | null): unknown => {
  if (!value) {
    return null;
  }

  const type = Engine.typeOf(value);
  const raw = Engine.valueOf(value);

  switch (type) {
    case FSDataType.Null:
    case FSDataType.Boolean:
    case FSDataType.Integer:
    case FSDataType.Float:
    case FSDataType.String:
    case FSDataType.BigInteger:
    case FSDataType.Guid:
    case FSDataType.DateTime:
      return raw;
    case FSDataType.List: {
      const list = raw as FsList;
      const entries: unknown[] = [];
      if (list && typeof (list as unknown as { toArray?: () => TypedValue[] }).toArray === 'function') {
        for (const entry of (list as FsList).toArray()) {
          entries.push(toPlainValue(entry));
        }
        return entries;
      }
      if (typeof Symbol !== 'undefined' && Symbol.iterator in (list as object)) {
        for (const entry of list as unknown as Iterable<TypedValue>) {
          entries.push(toPlainValue(entry));
        }
        return entries;
      }
      return null;
    }
    case FSDataType.KeyValueCollection: {
      const collection = raw as KeyValueCollection;
      if (collection && typeof collection.getAll === 'function') {
        const result: Record<string, unknown> = {};
        for (const [key, typed] of collection.getAll()) {
          result[key] = toPlainValue(typed);
        }
        return result;
      }
      return raw;
    }
    case FSDataType.Error: {
      const error = raw as FsError;
      return {
        type: error?.errorType ?? 'Error',
        message: error?.errorMessage ?? 'Unknown error',
        data: error?.errorData ?? null
      };
    }
    case FSDataType.Function:
      return '[Function]';
    case FSDataType.ValRef:
      return '[ValRef]';
    case FSDataType.ValSink:
      return '[ValSink]';
    case FSDataType.SigSource:
      return '[SigSource]';
    case FSDataType.SigSink:
      return '[SigSink]';
    default:
      return raw;
  }
};

const ensureNumber = (value: unknown): number | null =>
  typeof value === 'number' && Number.isFinite(value) ? value : null;

const ensurePoint = (value: unknown): [number, number] | null => {
  if (!Array.isArray(value) || value.length !== 2) {
    return null;
  }
  const [x, y] = value;
  if (typeof x === 'number' && Number.isFinite(x) && typeof y === 'number' && Number.isFinite(y)) {
    return [x, y];
  }
  return null;
};

const ensurePoints = (value: unknown): Array<[number, number]> | null => {
  if (!Array.isArray(value) || value.length < 3) {
    return null;
  }
  const points: Array<[number, number]> = [];
  for (const entry of value) {
    const point = ensurePoint(entry);
    if (!point) {
      return null;
    }
    points.push(point);
  }
  return points;
};

const evaluateExpression = (
  provider: DefaultFsDataProvider,
  expression: string
): EvaluationResult => {
  const trimmed = expression.trim();
  if (!trimmed) {
    return {
      value: null,
      typed: null,
      error: null
    };
  }

  try {
    const typed = Engine.evaluate(trimmed, provider);
    const value = toPlainValue(typed);
    return { value, typed, error: null };
  } catch (err) {
    return {
      value: null,
      typed: null,
      error: err instanceof Error ? err.message : String(err)
    };
  }
};

const interpretView = (value: unknown): ViewInterpretation => {
  if (value === null || value === undefined) {
    return { extent: null, warning: 'View expression returned null. Provide numeric bounds.' };
  }
  if (Array.isArray(value) || typeof value !== 'object') {
    return { extent: null, warning: 'View expression must return a record with min/max values.' };
  }
  const record = value as Record<string, unknown>;
  const minX = ensureNumber(record.minX);
  const maxX = ensureNumber(record.maxX);
  const minY = ensureNumber(record.minY);
  const maxY = ensureNumber(record.maxY);
  if (minX === null || maxX === null || minY === null || maxY === null) {
    return {
      extent: null,
      warning: 'All extent fields (minX, maxX, minY, maxY) must be finite numbers.'
    };
  }
  if (maxX <= minX || maxY <= minY) {
    return {
      extent: null,
      warning: 'Extent must define a positive width and height (max greater than min).'
    };
  }
  return {
    extent: { minX, maxX, minY, maxY },
    warning: null
  };
};

const collectPrimitives = (
  node: unknown,
  path: string,
  warnings: string[],
  unknownTypes: Set<string>
): Primitive[] => {
  if (Array.isArray(node)) {
    const primitives: Primitive[] = [];
    node.forEach((child, index) => {
      primitives.push(...collectPrimitives(child, `${path}[${index}]`, warnings, unknownTypes));
    });
    return primitives;
  }

  if (node && typeof node === 'object') {
    const { type, data } = node as { type?: unknown; data?: unknown };
    if (typeof type === 'string' && data && typeof data === 'object' && !Array.isArray(data)) {
      if (!PRIMITIVE_DEFINITIONS.some((definition) => definition.type === type)) {
        unknownTypes.add(type);
      }
      return [
        {
          type,
          data: data as Record<string, unknown>
        }
      ];
    }
    warnings.push(`Primitive at ${path || 'root'} must include a string 'type' and a record 'data'.`);
    return [];
  }

  console.error('Skipping graphics entry at', path || 'root', 'because it is not a list or object:', node);
  warnings.push(`Skipping graphics entry at ${path || 'root'} because it is not a list or object.`);
  return [];
};

const interpretGraphics = (value: unknown): GraphicsInterpretation => {
  if (value === null || value === undefined) {
    return {
      layers: null,
      warning: 'Graphics expression returned null. Provide at least an empty list of layers.',
      unknownTypes: []
    };
  }

  const warnings: string[] = [];
  const unknown = new Set<string>();
  const layers: PrimitiveLayer[] = [];

  if (Array.isArray(value)) {
    value.forEach((entry, index) => {
      const primitives = collectPrimitives(entry, `layer[${index}]`, warnings, unknown);
      if (primitives.length > 0) {
        layers.push(primitives);
      }
    });
  } else if (value && typeof value === 'object') {
    const primitives = collectPrimitives(value, 'root', warnings, unknown);
    if (primitives.length > 0) {
      layers.push(primitives);
    }
  } else {
    console.error('Graphics expression must evaluate to a list or key-value collection. Received:', value);
    warnings.push('Graphics expression must evaluate to a list or key-value collection. See console for details.');
  }

  const warningMessage = warnings.length > 0 ? warnings.join(' ') : null;

  return {
    layers: layers.length > 0 ? layers : null,
    warning: warningMessage,
    unknownTypes: Array.from(unknown)
  };
};

const prepareGraphics = (
  extent: ViewExtent | null,
  layers: PrimitiveLayer[] | null
): PreparedGraphics => {
  if (!extent || !layers) {
    return {
      layers: [],
      warnings: extent ? [] : ['Cannot render without a valid view extent.']
    };
  }

  const warnings: string[] = [];
  const preparedLayers: PreparedLayers = [];

  for (let layerIndex = 0; layerIndex < layers.length; layerIndex += 1) {
    const layer = layers[layerIndex];
    const prepared: PreparedPrimitive[] = [];

    for (let primitiveIndex = 0; primitiveIndex < layer.length; primitiveIndex += 1) {
      const primitive = layer[primitiveIndex];
      const ctx = `layer ${layerIndex + 1}, primitive ${primitiveIndex + 1}`;

      switch (primitive.type) {
        case 'line': {
          const from = ensurePoint(primitive.data.from);
          const to = ensurePoint(primitive.data.to);
          if (!from || !to) {
            warnings.push(`Line in ${ctx} requires numeric from/to points.`);
            break;
          }
          const stroke = typeof primitive.data.stroke === 'string' ? primitive.data.stroke : '#38bdf8';
          const width = ensureNumber(primitive.data.width) ?? 0.25;
          const dash = Array.isArray(primitive.data.dash)
            ? primitive.data.dash.every((segment) => typeof segment === 'number' && segment >= 0)
              ? (primitive.data.dash as number[])
              : null
            : null;
          prepared.push({
            type: 'line',
            from,
            to,
            stroke,
            width,
            dash
          });
          break;
        }
        case 'rect': {
          const position = ensurePoint(primitive.data.position);
          const size = ensurePoint(primitive.data.size);
          if (!position || !size) {
            warnings.push(`Rectangle in ${ctx} requires position and size points.`);
            break;
          }
          const stroke = typeof primitive.data.stroke === 'string' ? primitive.data.stroke : null;
          const fill = typeof primitive.data.fill === 'string' ? primitive.data.fill : null;
          const width = ensureNumber(primitive.data.width) ?? 0.25;
          prepared.push({
            type: 'rect',
            position,
            size,
            stroke,
            fill,
            width
          });
          break;
        }
        case 'circle': {
          const center = ensurePoint(primitive.data.center);
          const radius = ensureNumber(primitive.data.radius);
          if (!center || radius === null || radius <= 0) {
            warnings.push(`Circle in ${ctx} requires center and positive radius.`);
            break;
          }
          const stroke = typeof primitive.data.stroke === 'string' ? primitive.data.stroke : null;
          const fill = typeof primitive.data.fill === 'string' ? primitive.data.fill : null;
          const width = ensureNumber(primitive.data.width) ?? 0.25;
          prepared.push({
            type: 'circle',
            center,
            radius,
            stroke,
            fill,
            width
          });
          break;
        }
        case 'polygon': {
          const points = ensurePoints(primitive.data.points);
          if (!points) {
            warnings.push(`Polygon in ${ctx} requires an array of at least 3 numeric points.`);
            break;
          }
          const stroke = typeof primitive.data.stroke === 'string' ? primitive.data.stroke : null;
          const fill = typeof primitive.data.fill === 'string' ? primitive.data.fill : null;
          const width = ensureNumber(primitive.data.width) ?? 0.25;
          prepared.push({
            type: 'polygon',
            points,
            stroke,
            fill,
            width
          });
          break;
        }
        case 'text': {
          const position = ensurePoint(primitive.data.position);
          const text = typeof primitive.data.text === 'string' ? primitive.data.text : null;
          if (!position || text === null) {
            warnings.push(`Text in ${ctx} requires position and text.`);
            break;
          }
          const color = typeof primitive.data.color === 'string' ? primitive.data.color : '#f8fafc';
          const fontSize = ensureNumber(primitive.data.fontSize) ?? 1;
          const alignValue = primitive.data.align;
          const align: CanvasTextAlign = alignValue === 'right' || alignValue === 'center' ? alignValue : 'left';
          prepared.push({
            type: 'text',
            position,
            text,
            color,
            fontSize,
            align
          });
          break;
        }
        default:
          warnings.push(`No renderer for primitive type "${primitive.type}" (${ctx}).`);
          break;
      }
    }

    preparedLayers.push(prepared);
  }

  return {
    layers: preparedLayers,
    warnings
  };
};

const serializeStructure = (structure: Primitive): string =>
  JSON.stringify(structure, null, 2)
    .replace(/"([^\"]+)":/g, '$1:')
    .replace(/"/g, '\'');

const App = (): JSX.Element => {
  const [viewExpression, setViewExpression] = useState(defaultViewExpression);
  const [graphicsExpression, setGraphicsExpression] = useState(defaultGraphicsExpression);
  const [viewParseError, setViewParseError] = useState<string | null>(null);
  const [graphicsParseError, setGraphicsParseError] = useState<string | null>(null);
  const [selectedPrimitive, setSelectedPrimitive] = useState<PrimitiveType>(PRIMITIVE_DEFINITIONS[0].type);
  const [rightPaneTab, setRightPaneTab] = useState<'preview' | 'results'>('preview');
  const [leftPaneTab, setLeftPaneTab] = useState<'view' | 'graphics' | 'reference'>('graphics');
  const [leftPaneRatio, setLeftPaneRatio] = useState(0.55);
  const [previewSize, setPreviewSize] = useState<{ width: number; height: number }>({ width: 0, height: 0 });

  const providerRef = useRef(new DefaultFsDataProvider());
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const previewContainerRef = useRef<HTMLDivElement | null>(null);
  const previewFrameRef = useRef<HTMLDivElement | null>(null);
  const splitterDraggingRef = useRef(false);

  const leftPaneWidth = useMemo(() => `${Math.round(leftPaneRatio * 1000) / 10}%`, [leftPaneRatio]);

  const handleViewChange = useCallback((value: string) => {
    setViewExpression(value);
  }, []);

  const handleGraphicsChange = useCallback((value: string) => {
    setGraphicsExpression(value);
  }, []);

  const handleRightPaneTabChange = useCallback((_: SyntheticEvent, value: string) => {
    setRightPaneTab(value as 'preview' | 'results');
  }, []);

  const handleLeftPaneTabChange = useCallback((_: SyntheticEvent, value: string) => {
    setLeftPaneTab(value as 'view' | 'graphics' | 'reference');
  }, []);

  const handleSplitterMouseDown = useCallback((event: ReactMouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    splitterDraggingRef.current = true;
  }, []);

  const handleSplitterTouchStart = useCallback((event: ReactTouchEvent<HTMLDivElement>) => {
    event.preventDefault();
    splitterDraggingRef.current = true;
  }, []);

  const viewEvaluation = useMemo(() => {
    if (viewParseError) {
      return { value: null, typed: null, error: viewParseError } satisfies EvaluationResult;
    }
    return evaluateExpression(providerRef.current, viewExpression);
  }, [viewExpression, viewParseError]);

  const graphicsEvaluation = useMemo(() => {
    if (graphicsParseError) {
      return { value: null, typed: null, error: graphicsParseError } satisfies EvaluationResult;
    }
    return evaluateExpression(providerRef.current, graphicsExpression);
  }, [graphicsExpression, graphicsParseError]);

  const viewInterpretation = useMemo(() => interpretView(viewEvaluation.value), [viewEvaluation.value]);

  const graphicsInterpretation = useMemo(
    () => interpretGraphics(graphicsEvaluation.value),
    [graphicsEvaluation.value]
  );

  const viewAspectRatio = useMemo(() => {
    const extent = viewInterpretation.extent;
    if (!extent) {
      return 1;
    }
    const width = extent.maxX - extent.minX;
    const height = extent.maxY - extent.minY;
    if (width > 0 && height > 0) {
      return width / height;
    }
    return 1;
  }, [viewInterpretation.extent]);

  const preparedGraphics = useMemo(
    () => prepareGraphics(viewInterpretation.extent, graphicsInterpretation.layers),
    [viewInterpretation.extent, graphicsInterpretation.layers]
  );

  const updatePreviewSize = useCallback(() => {
    const container = previewContainerRef.current;
    if (!container) {
      return;
    }
    const rect = container.getBoundingClientRect();
    const width = rect.width;
    const height = rect.height;
    if (width <= 0 || height <= 0) {
      return;
    }
    setPreviewSize((current) => {
      if (Math.abs(current.width - width) < 0.5 && Math.abs(current.height - height) < 0.5) {
        return current;
      }
      return { width, height };
    });
  }, []);

  useEffect(() => {
    const container = previewContainerRef.current;
    if (!container) {
      return;
    }

    updatePreviewSize();

    const observer = new ResizeObserver(() => {
      updatePreviewSize();
    });
    observer.observe(container);

    return () => {
      observer.disconnect();
    };
  }, [updatePreviewSize]);

  useEffect(() => {
    const handlePointerMove = (clientX: number) => {
      if (!splitterDraggingRef.current) {
        return;
      }
      const container = containerRef.current;
      if (!container) {
        return;
      }
      const rect = container.getBoundingClientRect();
      if (rect.width <= 0) {
        return;
      }
      const relativeX = clientX - rect.left;
      const ratio = Math.min(0.8, Math.max(0.25, relativeX / rect.width));
      setLeftPaneRatio(ratio);
    };

    const handleMouseMove = (event: MouseEvent) => {
      handlePointerMove(event.clientX);
    };

    const handleTouchMove = (event: TouchEvent) => {
      if (!splitterDraggingRef.current) {
        return;
      }
      const touch = event.touches[0];
      if (touch) {
        event.preventDefault();
        handlePointerMove(touch.clientX);
      }
    };

    const stopDragging = () => {
      if (splitterDraggingRef.current) {
        splitterDraggingRef.current = false;
      }
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('touchmove', handleTouchMove, { passive: false });
    window.addEventListener('mouseup', stopDragging);
    window.addEventListener('touchend', stopDragging);
    window.addEventListener('touchcancel', stopDragging);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('touchmove', handleTouchMove);
      window.removeEventListener('mouseup', stopDragging);
      window.removeEventListener('touchend', stopDragging);
      window.removeEventListener('touchcancel', stopDragging);
    };
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const context = canvas.getContext('2d');
    if (!context) {
      return;
    }

    const containerWidth = previewSize.width;
    const containerHeight = previewSize.height;

    const containerAspect = containerWidth / containerHeight;
    let newWidth: number;
    let newHeight: number;

    if (containerAspect > viewAspectRatio) {
      newHeight = containerHeight;
      newWidth = newHeight * viewAspectRatio;
    } else {
      newWidth = containerWidth;
      newHeight = newWidth / viewAspectRatio;
    }

    const displayWidth = Math.max(1, Math.round(newWidth));
    const displayHeight = Math.max(1, Math.round(newHeight));

    if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
      canvas.width = displayWidth;
      canvas.height = displayHeight;
    }

    const { extent } = viewInterpretation;
    const layers = preparedGraphics.layers;

    context.clearRect(0, 0, canvas.width, canvas.height);
    context.fillStyle = '#0f172a';
    context.fillRect(0, 0, canvas.width, canvas.height);

    if (!extent || layers.length === 0) {
      return;
    }

    const padding = 32;
    const viewWidth = extent.maxX - extent.minX;
    const viewHeight = extent.maxY - extent.minY;
    if (viewWidth <= 0 || viewHeight <= 0) {
      return;
    }

    const scaleX = (canvas.width - padding * 2) / viewWidth;
    const scaleY = (canvas.height - padding * 2) / viewHeight;
    const scale = Math.max(0.0001, Math.min(scaleX, scaleY));

    const project = (point: [number, number]) => ({
      x: padding + (point[0] - extent.minX) * scale,
      y: canvas.height - (padding + (point[1] - extent.minY) * scale)
    });

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
      context.strokeStyle = 'rgba(148, 163, 184, 0.3)';
      context.beginPath();
      if (extent.minY <= 0 && extent.maxY >= 0) {
        const bottom = project([extent.minX, 0]);
        const top = project([extent.maxX, 0]);
        context.moveTo(bottom.x, bottom.y);
        context.lineTo(top.x, top.y);
      }
      if (extent.minX <= 0 && extent.maxX >= 0) {
        const left = project([0, extent.minY]);
        const right = project([0, extent.maxY]);
        context.moveTo(left.x, left.y);
        context.lineTo(right.x, right.y);
      }
      context.stroke();
      context.restore();
    };

    drawAxes();

    for (const layer of layers) {
      for (const primitive of layer) {
        switch (primitive.type) {
          case 'line': {
            const { from, to, stroke, width, dash } = primitive;
            const start = project(from);
            const end = project(to);
            context.save();
            applyStroke(stroke, width);
            if (dash && dash.length > 0) {
              context.setLineDash(dash.map((segment) => Math.max(0, segment) * scale));
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
            const { position, size, stroke, fill, width } = primitive;
            const bottomLeft = project(position);
            const topRight = project([position[0] + size[0], position[1] + size[1]]);
            const drawWidth = topRight.x - bottomLeft.x;
            const drawHeight = bottomLeft.y - topRight.y;
            context.save();
            if (fill) {
              context.fillStyle = fill;
              context.fillRect(bottomLeft.x, topRight.y, drawWidth, drawHeight);
            }
            if (stroke && width > 0) {
              applyStroke(stroke, width);
              context.strokeRect(bottomLeft.x, topRight.y, drawWidth, drawHeight);
            }
            context.restore();
            break;
          }
          case 'circle': {
            const { center, radius, stroke, fill, width } = primitive;
            const centerPoint = project(center);
            const scaledRadius = Math.max(0, radius * scale);
            context.save();
            context.beginPath();
            context.arc(centerPoint.x, centerPoint.y, scaledRadius, 0, Math.PI * 2);
            if (fill) {
              context.fillStyle = fill;
              context.fill();
            }
            if (stroke && width > 0) {
              applyStroke(stroke, width);
              context.stroke();
            }
            context.restore();
            break;
          }
          case 'polygon': {
            const { points, stroke, fill, width } = primitive;
            if (points.length < 3) {
              continue;
            }
            context.save();
            context.beginPath();
            const [first, ...rest] = points;
            const firstPoint = project(first);
            context.moveTo(firstPoint.x, firstPoint.y);
            for (const point of rest) {
              const projected = project(point);
              context.lineTo(projected.x, projected.y);
            }
            context.closePath();
            if (fill) {
              context.fillStyle = fill;
              context.fill();
            }
            if (stroke && width > 0) {
              applyStroke(stroke, width);
              context.stroke();
            }
            context.restore();
            break;
          }
          case 'text': {
            const { position, text, color, fontSize, align } = primitive;
            const origin = project(position);
            context.save();
            context.fillStyle = color;
            context.textAlign = align;
            context.textBaseline = 'middle';
            context.font = `${Math.max(12, fontSize * scale)}px "Inter", "Roboto", sans-serif`;
            context.fillText(text, origin.x, origin.y);
            context.restore();
            break;
          }
          default:
            break;
        }
      }
    }
  }, [viewInterpretation, preparedGraphics, previewSize]);

  const selectedDefinition = PRIMITIVE_DEFINITIONS.find(
    (definition) => definition.type === selectedPrimitive
  );

  const totalPrimitives = useMemo(() => {
    if (!graphicsInterpretation.layers) {
      return 0;
    }
    return graphicsInterpretation.layers.reduce((sum, layer) => sum + layer.length, 0);
  }, [graphicsInterpretation.layers]);

  const unknownPrimitiveChips = graphicsInterpretation.unknownTypes.map((type) => (
    <Chip key={type} label={`Unknown: ${type}`} color="warning" size="small" />
  ));

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AppBar position="sticky" color="transparent" elevation={0} sx={{ backdropFilter: 'blur(12px)' }}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            FuncScript Graphics Playground
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Define a viewport & layered primitives with FuncScript
          </Typography>
        </Toolbar>
      </AppBar>
      <Box sx={{ py: 4 }}>
        <Container
          disableGutters
          maxWidth={false}
          sx={{
            px: 0,
            height: { md: 'calc(100vh - 144px)' },
            maxWidth: '100vw'
          }}
        >
          <Box
            ref={containerRef}
            sx={{
              display: 'flex',
              flexDirection: { xs: 'column', md: 'row' },
              alignItems: 'stretch',
              gap: { xs: 3, md: 0 },
              height: { xs: 'auto', md: '100%' },
              minHeight: { xs: 'auto', md: 560 }
            }}
          >
            <Box
              sx={{
                flexBasis: { xs: '100%', md: leftPaneWidth },
                maxWidth: { xs: '100%', md: leftPaneWidth },
                minWidth: { xs: '100%', md: 320 },
                flexGrow: { xs: 1, md: 0 },
                flexShrink: 0,
                display: 'flex'
              }}
            >
              <Paper
                elevation={6}
                sx={{
                  flexGrow: 1,
                  display: 'flex',
                  flexDirection: 'column',
                  overflow: 'hidden',
                  p: { xs: 2, md: 3 }
                }}
              >
                <Tabs
                  value={leftPaneTab}
                  onChange={handleLeftPaneTabChange}
                  variant="fullWidth"
                  textColor="primary"
                  indicatorColor="primary"
                >
                  <Tab label="View Formula" value="view" />
                  <Tab label="Graphics Formula" value="graphics" />
                  <Tab label="Primitive Reference" value="reference" />
                </Tabs>
                <Divider />
                <Box
                  sx={{ flexGrow: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column', minHeight: 0 }}
                >
                  {leftPaneTab === 'view' ? (
                    <Stack
                      spacing={2}
                      sx={{ flexGrow: 1, overflow: 'hidden', minHeight: 0, py: 1, px: { xs: 0, md: 0 } }}
                    >
                      <Typography variant="h5">View Formula</Typography>
                      <Typography variant="body2" color="text.secondary">
                        Return <code>{'{ minX, minY, maxX, maxY }'}</code> describing the visible world bounds.
                      </Typography>
                      <Box sx={{ flexGrow: 1, minHeight: 200, minWidth: 0 }}>
                        <FuncScriptEditor
                          value={viewExpression}
                          onChange={handleViewChange}
                          onError={setViewParseError}
                          minHeight={200}
                          style={{ height: '100%' }}
                        />
                      </Box>
                      {viewEvaluation.error ? (
                        <Alert severity="error" variant="outlined">
                          {viewEvaluation.error}
                        </Alert>
                      ) : viewInterpretation.warning ? (
                        <Alert severity="warning" variant="outlined">
                          {viewInterpretation.warning}
                        </Alert>
                      ) : (
                        <Alert severity="success" variant="outlined">
                          Bounds ready. Width {viewInterpretation.extent
                            ? (viewInterpretation.extent.maxX - viewInterpretation.extent.minX).toFixed(2)
                            : '—'}{' '}
                          × Height {viewInterpretation.extent
                            ? (viewInterpretation.extent.maxY - viewInterpretation.extent.minY).toFixed(2)
                            : '—'} in world units.
                        </Alert>
                      )}
                    </Stack>
                  ) : null}

                  {leftPaneTab === 'graphics' ? (
                    <Stack
                      spacing={2}
                      sx={{ flexGrow: 1, overflow: 'hidden', minHeight: 0, py: 1, px: { xs: 0, md: 0 } }}
                    >
                      <Typography variant="h5">Graphics Formula</Typography>
                      <Typography variant="body2" color="text.secondary">
                        Return a list of layers populated with primitives (<code>{'{ type, data }'}</code>) to render.
                      </Typography>
                      <Box sx={{ flexGrow: 1, minHeight: 320, minWidth: 0 }}>
                        <FuncScriptEditor
                          value={graphicsExpression}
                          onChange={handleGraphicsChange}
                          onError={setGraphicsParseError}
                          minHeight={320}
                          style={{ height: '100%' }}
                        />
                      </Box>
                      {graphicsEvaluation.error ? (
                        <Alert severity="error" variant="outlined">
                          {graphicsEvaluation.error}
                        </Alert>
                      ) : graphicsInterpretation.warning ? (
                        <Alert severity="warning" variant="outlined">
                          {graphicsInterpretation.warning}
                        </Alert>
                      ) : preparedGraphics.warnings.length > 0 ? (
                        <Alert severity="info" variant="outlined">
                          {preparedGraphics.warnings.join(' ')}
                        </Alert>
                      ) : (
                        <Alert severity="success" variant="outlined">
                          {graphicsInterpretation.layers?.length ?? 0} layer(s), {totalPrimitives} primitive(s) ready to
                          render.
                        </Alert>
                      )}
                    </Stack>
                  ) : null}

                  {leftPaneTab === 'reference' ? (
                    <Stack spacing={3} sx={{ flexGrow: 1, overflowY: 'auto', py: 1, pr: { md: 1 } }}>
                      <Typography variant="h5">Primitive Reference</Typography>
                      <Typography variant="body2" color="text.secondary">
                        Select a primitive to inspect its expected <code>data</code> shape and notes.
                      </Typography>
                      <FormControl fullWidth size="small">
                        <InputLabel id="primitive-select-label">Primitive</InputLabel>
                        <Select
                          labelId="primitive-select-label"
                          label="Primitive"
                          value={selectedPrimitive}
                          onChange={(event) => setSelectedPrimitive(event.target.value as PrimitiveType)}
                        >
                          {PRIMITIVE_DEFINITIONS.map((definition) => (
                            <MenuItem key={definition.type} value={definition.type}>
                              {definition.label}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>
                      {selectedDefinition ? (
                        <Stack spacing={2}>
                          <Typography variant="subtitle1">{selectedDefinition.label}</Typography>
                          <Typography variant="body2" color="text.secondary">
                            {selectedDefinition.description}
                          </Typography>
                          <Box component="pre" sx={{
                            m: 0,
                            px: 2,
                            py: 2,
                            borderRadius: 2,
                            bgcolor: 'rgba(15,23,42,0.55)',
                            color: '#e2e8f0',
                            fontFamily: '"Source Code Pro", monospace',
                            fontSize: 13,
                            overflowX: 'auto'
                          }}>
                            {serializeStructure(selectedDefinition.structure)}
                          </Box>
                          {selectedDefinition.notes?.length ? (
                            <Stack spacing={1}>
                              {selectedDefinition.notes.map((note) => (
                                <Alert key={note} severity="info" variant="outlined">
                                  {note}
                                </Alert>
                              ))}
                            </Stack>
                          ) : null}
                        </Stack>
                      ) : null}
                    </Stack>
                  ) : null}
                </Box>
              </Paper>
            </Box>

            <Box
              role="separator"
              onMouseDown={handleSplitterMouseDown}
              onTouchStart={handleSplitterTouchStart}
              sx={{
                display: { xs: 'none', md: 'flex' },
                alignItems: 'center',
                justifyContent: 'center',
                width: { md: 12 },
                cursor: 'col-resize',
                px: 1,
                mx: 1
              }}
            >
              <Box sx={{ width: 3, height: '60%', borderRadius: 2, bgcolor: 'rgba(148,163,184,0.35)' }} />
            </Box>

            <Box
              sx={{
                flexGrow: 1,
                minWidth: { xs: '100%', md: 320 },
                display: 'flex'
              }}
            >
              <Paper
                elevation={6}
                sx={{
                  flexGrow: 1,
                  display: 'flex',
                  flexDirection: 'column',
                  overflow: 'hidden',
                  p: { xs: 2.5, md: 4 }
                }}
              >
                <Stack spacing={3} sx={{ height: '100%' }}>
                  <Box>
                    <Tabs
                      value={rightPaneTab}
                      onChange={handleRightPaneTabChange}
                      variant="fullWidth"
                      textColor="primary"
                      indicatorColor="primary"
                    >
                      <Tab label="Graphics Preview" value="preview" />
                      <Tab label="Result View" value="results" />
                    </Tabs>
                  </Box>
                  <Divider />
                  <Box sx={{ display: rightPaneTab === 'preview' ? 'flex' : 'none', flexGrow: 1, minHeight: 0 }}>
                    <Stack spacing={3} sx={{ height: '100%' }}>
                      <Typography variant="h5">Render Preview</Typography>
                      <Box
                        sx={{
                          flexGrow: 1,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          borderRadius: 3,
                          bgcolor: 'rgba(15,23,42,0.35)',
                          p: { xs: 2, md: 3 }
                        }}
                      >
                        <Box
                          ref={previewContainerRef}
                          sx={{
                            width: '100%',
                            height: '100%',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                          }}
                        >
                          <Box
                            ref={previewFrameRef}
                            sx={{
                              width: '100%',
                              maxWidth: '100%',
                              maxHeight: '100%',
                              borderRadius: 3,
                              overflow: 'hidden',
                              bgcolor: '#0f172a',
                              boxShadow: '0px 16px 32px rgba(15, 23, 42, 0.45)',
                              display: 'flex'
                            }}
                          >
                            <canvas
                              ref={canvasRef}
                              width={960}
                              height={520}
                              style={{ width: '100%', height: '100%', display: 'block' }}
                            />
                          </Box>
                        </Box>
                      </Box>
                    </Stack>
                  </Box>
                  <Box sx={{ display: rightPaneTab === 'results' ? 'block' : 'none', flexGrow: 1, minHeight: 0 }}>
                    <Stack spacing={3} sx={{ height: '100%', overflowY: 'auto', pr: { md: 1 } }}>
                      <Box>
                        <Typography variant="h6" gutterBottom>
                          View Expression Result
                        </Typography>
                        <Box component="pre" sx={{
                          m: 0,
                          px: 2,
                          py: 2,
                          borderRadius: 2,
                          bgcolor: 'rgba(15,23,42,0.55)',
                          color: '#e2e8f0',
                          fontFamily: '"Source Code Pro", monospace',
                          fontSize: 13,
                          overflowX: 'auto'
                        }}>
                          {JSON.stringify(viewEvaluation.value, null, 2)}
                        </Box>
                      </Box>
                      <Box>
                        <Typography variant="h6" gutterBottom>
                          Graphics Expression Result
                        </Typography>
                        <Box component="pre" sx={{
                          m: 0,
                          px: 2,
                          py: 2,
                          borderRadius: 2,
                          bgcolor: 'rgba(15,23,42,0.55)',
                          color: '#e2e8f0',
                          fontFamily: '"Source Code Pro", monospace',
                          fontSize: 13,
                          overflowX: 'auto'
                        }}>
                          {JSON.stringify(graphicsEvaluation.value, null, 2)}
                        </Box>
                      </Box>
                      <Stack direction="row" spacing={1} flexWrap="wrap">
                        {unknownPrimitiveChips}
                      </Stack>
                    </Stack>
                  </Box>
                </Stack>
              </Paper>
            </Box>
          </Box>
        </Container>
      </Box>
    </ThemeProvider>
  );
};

export default App;

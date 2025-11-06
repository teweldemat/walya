declare const require: any;

type ExampleModule = string | { default: string };

export type CustomTabDefinition = {
  name: string;
  expression: string;
};

export type ExampleDefinition = {
  id: string;
  name: string;
  view: string;
  graphics: string;
  customTabs: CustomTabDefinition[];
};

const fxContext = require.context('./', true, /\.fx$/);

const collectExampleIds = (context: any): Set<string> => {
  const ids = new Set<string>();
  const keys: string[] = context.keys();
  for (const key of keys) {
    const normalized = key.replace(/^\.\//, '');
    const [folder] = normalized.split('/');
    if (folder) {
      ids.add(folder);
    }
  }
  return ids;
};

const allIds = collectExampleIds(fxContext);

const readModule = (context: any, path: string): string | null => {
  if (!context) {
    return null;
  }
  try {
    const mod: ExampleModule = context(path);
    if (typeof mod === 'string') {
      return mod;
    }
    if (mod && typeof mod.default === 'string') {
      return mod.default;
    }
    return String(mod ?? '');
  } catch (err) {
    return null;
  }
};

const formatName = (id: string): string =>
  id
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase());

const isValidTabName = (name: string) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(name);

const collectCustomTabs = (id: string): CustomTabDefinition[] => {
  const prefix = `./${id}/`;
  const tabs: CustomTabDefinition[] = [];
  const seen = new Set<string>();

  for (const key of fxContext.keys()) {
    if (!key.startsWith(prefix)) {
      continue;
    }
    const remainder = key.slice(prefix.length);
    if (remainder.includes('/')) {
      continue;
    }
    if (remainder === 'view.fx' || remainder === 'main.fx') {
      continue;
    }

    const expression = readModule(fxContext, key);
    if (!expression) {
      continue;
    }
    const baseName = remainder.replace(/\.fx$/, '');
    if (!isValidTabName(baseName)) {
      continue;
    }
    const normalized = baseName.toLowerCase();
    if (seen.has(normalized)) {
      continue;
    }
    seen.add(normalized);
    tabs.push({ name: baseName, expression });
  }

  tabs.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
  return tabs;
};

const examples: ExampleDefinition[] = [];

for (const id of allIds) {
  const viewPath = `./${id}/view.fx`;
  const graphicsPath = `./${id}/main.fx`;
  const view = readModule(fxContext, viewPath);
  const graphics = readModule(fxContext, graphicsPath);
  if (!view || !graphics) {
    continue;
  }
  examples.push({
    id,
    name: formatName(id),
    view,
    graphics,
    customTabs: collectCustomTabs(id)
  });
}

examples.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

export default examples;

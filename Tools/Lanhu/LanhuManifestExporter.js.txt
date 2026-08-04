(async () => {
  const schemaVersion = '1.0';
  const exporterVersion = '3.1.1';
  const testMode = Boolean(globalThis.__LANHU_EXPORTER_TEST__);
  const bridgeJobId = String(globalThis.__GDK_LANHU_SYNC_JOB__ || '');

  function asArray(value) {
    return Array.isArray(value) ? value : [];
  }

  function numberValue(value, fallback = 0) {
    if (value && typeof value === 'object' && value.value !== undefined) {
      value = value.value;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  function firstNumber(values, fallback = 0) {
    for (const value of values) {
      const parsed = numberValue(value, Number.NaN);
      if (Number.isFinite(parsed)) return parsed;
    }

    return fallback;
  }

  function readRect(source) {
    if (!source || typeof source !== 'object') return null;

    const directWidth = numberValue(source.width, 0);
    const directHeight = numberValue(source.height, 0);
    if (directWidth > 0 && directHeight > 0) {
      return {
        x: firstNumber([source.left, source.x, source.position_x, source.positionX], 0),
        y: firstNumber([source.top, source.y, source.position_y, source.positionY], 0),
        width: directWidth,
        height: directHeight
      };
    }

    const candidates = [
      source._orgBounds,
      source.bounds,
      source.boundsWithFX,
      source.frame,
      source.absoluteBoundingBox,
      source.absoluteRenderBounds
    ];
    for (const bounds of candidates) {
      if (!bounds || typeof bounds !== 'object') continue;
      const left = firstNumber([bounds.left, bounds.x], 0);
      const top = firstNumber([bounds.top, bounds.y], 0);
      const width = firstNumber(
        [bounds.width],
        numberValue(bounds.right, left) - left);
      const height = firstNumber(
        [bounds.height],
        numberValue(bounds.bottom, top) - top);
      if (width > 0 && height > 0) {
        return { x: left, y: top, width, height };
      }
    }

    return null;
  }

  function childrenOf(source) {
    if (!source || typeof source !== 'object') return [];
    if (Array.isArray(source.__children)) return source.__children;
    if (Array.isArray(source.layers)) return source.layers;
    if (Array.isArray(source.children)) return source.children;
    return [];
  }

  function unionChildRects(source) {
    const rects = childrenOf(source).map(readRect).filter(Boolean);
    if (!rects.length) return null;
    const left = Math.min(...rects.map(rect => rect.x));
    const top = Math.min(...rects.map(rect => rect.y));
    const right = Math.max(...rects.map(rect => rect.x + rect.width));
    const bottom = Math.max(...rects.map(rect => rect.y + rect.height));
    return { x: left, y: top, width: right - left, height: bottom - top };
  }

  function effectiveRect(source, fallback) {
    return readRect(source) || unionChildRects(source) || fallback || null;
  }

  function buildFlatTree(items) {
    const sourceItems = asArray(items).filter(item => item && typeof item === 'object');
    if (!sourceItems.length) return null;

    const copies = new Map();
    for (const item of sourceItems) {
      const key = String(item.id ?? item.web_id ?? item.objectID ?? '');
      if (!key || copies.has(key)) continue;
      copies.set(key, Object.assign({}, item, { __children: [] }));
    }

    const roots = [];
    for (const copy of copies.values()) {
      const parentKey = String(copy.parentID ?? copy.parentId ?? copy.parent_id ?? '');
      const parent = parentKey ? copies.get(parentKey) : null;
      if (parent) parent.__children.push(copy);
      else roots.push(copy);
    }

    return roots.find(item => item.isPage) ||
      roots.find(item => childrenOf(item).length > 0) ||
      roots[0] ||
      null;
  }

  function findLayerRoot(layerDocument) {
    if (!layerDocument || typeof layerDocument !== 'object') return null;
    if (layerDocument.artboard && typeof layerDocument.artboard === 'object') {
      return layerDocument.artboard;
    }

    const info = asArray(layerDocument.info);
    const nestedInfo = info.find(item => item && (item.isPage || String(item.id) === '-777777')) ||
      info.find(item => childrenOf(item).length > 0);
    if (nestedInfo) return nestedInfo;
    if (info.length) return buildFlatTree(info);

    const array = asArray(layerDocument.array);
    if (array.length) return buildFlatTree(array);
    if (layerDocument.document && typeof layerDocument.document === 'object') {
      return layerDocument.document;
    }

    return null;
  }

  function byte(value, normalized) {
    const scale = normalized ? 255 : 1;
    return Math.max(0, Math.min(255, Math.round(numberValue(value, 0) * scale)));
  }

  function colorToHex(value, fallback = '#FFFFFFFF') {
    if (!value) return fallback;
    if (typeof value === 'string') {
      const hex = value.trim();
      if (/^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(hex)) {
        return hex.length === 7 ? `${hex}FF`.toUpperCase() : hex.toUpperCase();
      }

      const match = hex.match(/rgba?\(([^)]+)\)/i);
      if (match) {
        const parts = match[1].split(',').map(part => Number(part.trim()));
        if (parts.length >= 3 && parts.slice(0, 4).every(Number.isFinite)) {
          const alpha = parts.length > 3 ? byte(parts[3], parts[3] <= 1) : 255;
          return `#${byte(parts[0], false).toString(16).padStart(2, '0')}` +
            `${byte(parts[1], false).toString(16).padStart(2, '0')}` +
            `${byte(parts[2], false).toString(16).padStart(2, '0')}` +
            `${alpha.toString(16).padStart(2, '0')}`.toUpperCase();
        }
      }

      return fallback;
    }

    if (typeof value !== 'object') return fallback;
    const red = firstNumber([value.r, value.red], 0);
    const green = firstNumber([value.g, value.green], 0);
    const blue = firstNumber([value.b, value.blue], 0);
    const normalized = Math.max(red, green, blue) <= 1;
    const alphaValue = firstNumber([value.a, value.alpha, value.opacity], 1);
    const alphaNormalized = alphaValue <= 1;
    return `#${byte(red, normalized).toString(16).padStart(2, '0')}` +
      `${byte(green, normalized).toString(16).padStart(2, '0')}` +
      `${byte(blue, normalized).toString(16).padStart(2, '0')}` +
      `${byte(alphaValue, alphaNormalized).toString(16).padStart(2, '0')}`.toUpperCase();
  }

  function readOpacity(source) {
    let value = source && source.opacity;
    if (value === undefined && source && source.blendOptions) {
      value = source.blendOptions.opacity;
    }

    value = numberValue(value, 1);
    if (value > 1 && value <= 100) value /= 100;
    else if (value > 100) value /= 255;
    return Math.max(0, Math.min(1, value));
  }

  function readImageUrl(source, assetInfo) {
    const candidates = [];
    const append = (key, value) => {
      const url = normalizeHttpUrl(value);
      if (url) {
        candidates.push({ key: String(key || ''), value: url });
      } else if (value && typeof value === 'object') {
        append(key, value.url || value.src || value.href);
      }
    };

    const appendImageSource = (key, value) => {
      if (!value) return;
      if (Array.isArray(value)) {
        value.forEach((item, index) => appendImageSource(`${key}_${index}`, item));
        return;
      }

      if (typeof value !== 'object') {
        append(key, value);
        return;
      }

      append(key, value);
      for (const [childKey, childValue] of Object.entries(value)) {
        if (/(url|src|href|image|bitmap|png|jpe?g|webp|svg)/i.test(childKey)) {
          append(`${key}_${childKey}`, childValue);
        }
      }
    };

    if (typeof source.image === 'string') append('image', source.image);
    else if (source.image && typeof source.image === 'object') {
      for (const [key, value] of Object.entries(source.image)) append(key, value);
    }

    if (source.images && typeof source.images === 'object') {
      for (const [key, value] of Object.entries(source.images)) append(key, value);
    }

    append('format_url', source.format_url);
    append('imageUrl', source.imageUrl);
    append('svgUrl', source.svgUrl);
    append('svg', source.svg);
    append('url', source.sliceUrl);
    appendImageSource('fill', source.fill);
    appendImageSource('fills', source.fills);
    appendImageSource('backgroundImage', source.backgroundImage);
    appendImageSource('pattern', source.pattern);
    appendImageSource('asset', source.asset);
    appendImageSource('assetInfo', assetInfo);
    if (!candidates.length) return '';

    const priorities = ['png_xxxhd', 'png_4x', 'png_3x', 'png_2x', 'png', 'web', 'image', 'format_url', 'svgUrl'];
    candidates.sort((left, right) => {
      const leftIndex = priorities.findIndex(priority => left.key.toLowerCase().includes(priority.toLowerCase()));
      const rightIndex = priorities.findIndex(priority => right.key.toLowerCase().includes(priority.toLowerCase()));
      return (leftIndex < 0 ? priorities.length : leftIndex) -
        (rightIndex < 0 ? priorities.length : rightIndex);
    });
    return candidates[0].value;
  }

  function readBorder(source, assetInfo) {
    const value = source.border || source.capInsets || source.insets ||
      (assetInfo && (assetInfo.border || assetInfo.capInsets || assetInfo.insets));
    if (!value || typeof value !== 'object') {
      return { left: 0, bottom: 0, right: 0, top: 0 };
    }

    return {
      left: Math.max(0, firstNumber([value.left, value.x], 0)),
      bottom: Math.max(0, firstNumber([value.bottom, value.y], 0)),
      right: Math.max(0, firstNumber([value.right, value.z], 0)),
      top: Math.max(0, firstNumber([value.top, value.w], 0))
    };
  }

  function isTextLayer(source) {
    const type = String(source.type || source._class || '').toLowerCase();
    return type === 'text' || type === 'textlayer' || Boolean(source.textInfo || source.characters);
  }

  function textValue(source) {
    if (typeof source.characters === 'string') return source.characters;
    if (typeof source.text === 'string') return source.text;
    if (source.textInfo && typeof source.textInfo.text === 'string') return source.textInfo.text;
    return String(source.name || '');
  }

  function textColor(source) {
    const info = source.textInfo || source.fontStyle || source.style || {};
    return colorToHex(info.color || info.fontColor || source.fontColor, '#FFFFFFFF');
  }

  function textFontSize(source) {
    const info = source.textInfo || source.fontStyle || source.style || {};
    return Math.max(1, firstNumber([
      info.size,
      info.sizePt,
      info.fontSize,
      info.impliedFontSize,
      source.fontSize
    ], 24));
  }

  function textAlignment(source) {
    const info = source.textInfo || source.fontStyle || source.style || {};
    const value = String(info.justification || info.textAlignHorizontal || info.textAlignment || 'left').toLowerCase();
    if (value.includes('center')) return 'center';
    if (value.includes('right')) return 'right';
    if (value.includes('justif')) return 'justified';
    return 'left';
  }

  function textStyle(source) {
    const info = source.textInfo || source.fontStyle || source.style || {};
    return {
      fontName: String(info.fontName || info.fontFamily || source.fontName || ''),
      fontPostScriptName: String(info.fontPostScriptName || info.postScriptName || ''),
      fontStyleName: String(info.fontStyleName || info.fontStyle || ''),
      bold: Boolean(info.bold || /bold/i.test(String(info.fontStyleName || info.fontStyle || ''))),
      italic: Boolean(info.italic || /italic/i.test(String(info.fontStyleName || info.fontStyle || ''))),
      tracking: firstNumber([info.tracking, info.letterSpacing], 0),
      lineHeight: Math.max(0, firstNumber([info.leading, info.leadingPt, info.lineHeight], 0)),
      wordWrap: info.wordWrap !== false,
      overflow: String(info.overflow || 'overflow').toLowerCase()
    };
  }

  function layerName(source, fallback) {
    return String(source.name || source.layerName || source.type || fallback || 'Layer');
  }

  function makeContext(image, layerDocument, assets) {
    const assetInfo = new Map();
    const sourceAssets = Array.isArray(layerDocument.assets)
      ? layerDocument.assets.map(item => [item && item.id, item])
      : Object.entries(layerDocument.assets || {});
    for (const [key, item] of sourceAssets) {
      if (!item || typeof item !== 'object') continue;
      if (key !== undefined && key !== null) assetInfo.set(String(key), item);
      if (item.id !== undefined) assetInfo.set(String(item.id), item);
    }

    return {
      pageId: String(image.id),
      reverseChildren: String(layerDocument.type || '').toLowerCase() === 'ps',
      assets,
      assetInfo,
      idCounts: new Map()
    };
  }

  function uniqueNodeId(source, context, suffix = '') {
    const raw = String(source.web_id ?? source.id ?? source.objectID ?? source.nodeId ?? layerName(source, 'layer'));
    const base = `${context.pageId}:${raw}${suffix}`;
    const count = context.idCounts.get(base) || 0;
    context.idCounts.set(base, count + 1);
    return count ? `${base}:${count + 1}` : base;
  }

  function findAssetInfo(source, context) {
    const keys = [
      source.assetId,
      source.asset_id,
      source.imageRef,
      source.image_ref,
      source.imageId,
      source.image_id,
      source.id,
      source.web_id
    ];
    for (const key of keys) {
      if (key !== undefined && key !== null && context.assetInfo.has(String(key))) {
        return context.assetInfo.get(String(key));
      }
    }

    return null;
  }

  function readFillColor(source) {
    const fill = source.fill;
    if (fill && typeof fill === 'object' && fill.color) return fill.color;
    const solidFill = asArray(source.fills).find(item =>
      item && item.visible !== false && item.color &&
      !/image|bitmap|pattern/i.test(String(item.type || '')));
    return (solidFill && solidFill.color) || source.backgroundColor || source.fillColor || '';
  }

  function supportsRectangularFill(source, hasChildren) {
    if (hasChildren) return true;
    const type = String(source.type || source._class || '').toLowerCase();
    return ['frame', 'artboard', 'group', 'layersection', 'symbol', 'component', 'instance', 'rectangle', 'rect']
      .includes(type);
  }

  function baseNode(id, name, rect, parentRect, source) {
    return {
      id,
      name,
      kind: 'Container',
      x: rect.x - parentRect.x,
      y: rect.y - parentRect.y,
      width: rect.width,
      height: rect.height,
      visible: source.visible !== false,
      opacity: readOpacity(source),
      assetId: '',
      text: '',
      fontSize: 24,
      fontName: '',
      fontPostScriptName: '',
      fontStyleName: '',
      bold: false,
      italic: false,
      tracking: 0,
      lineHeight: 0,
      wordWrap: true,
      overflow: 'overflow',
      color: '#FFFFFFFF',
      backgroundColor: '',
      textAlignment: 'left',
      cornerRadius: Math.max(0, firstNumber([source.cornerRadius, source.radius], 0)),
      clipsContent: Boolean(source.clipsContent || source.clipContent || source.mask),
      nineSlice: false,
      shared: false,
      border: { left: 0, bottom: 0, right: 0, top: 0 },
      children: []
    };
  }

  function buildNode(source, parentRect, context) {
    if (!source || source.visible === false) return null;
    const rect = effectiveRect(source, parentRect);
    if (!rect || rect.width <= 0 || rect.height <= 0) return null;

    const id = uniqueNodeId(source, context);
    const node = baseNode(id, layerName(source, id), rect, parentRect, source);
    const assetInfo = findAssetInfo(source, context);
    const imageUrl = readImageUrl(source, assetInfo);
    if (imageUrl) {
      const border = readBorder(source, assetInfo);
      const explicitNineSlice = Boolean(
        source.nineSlice || source.nine_slice || source.isNineSlice ||
        (assetInfo && (assetInfo.nineSlice || assetInfo.nine_slice || assetInfo.isNineSlice)));
      const assetId = `${id}:asset`;
      node.kind = 'Image';
      node.assetId = assetId;
      node.border = border;
      node.nineSlice = explicitNineSlice || Object.values(border).some(value => value > 0);
      node.shared = Boolean(source.shared || (assetInfo && assetInfo.shared));
      context.assets.push({
        id: assetId,
        name: node.name,
        url: imageUrl,
        format: /\.svg(?:\?|$)/i.test(imageUrl) ? 'svg' : 'png',
        pixelScale: Math.max(0.01, firstNumber([source.pixelScale, source.imageScale], 1)),
        shared: node.shared
      });
    }

    if (!imageUrl && isTextLayer(source)) {
      node.kind = 'Text';
      node.text = textValue(source);
      node.fontSize = textFontSize(source);
      node.color = textColor(source);
      node.textAlignment = textAlignment(source);
      Object.assign(node, textStyle(source));
      return node;
    }

    let children = childrenOf(source);
    if (context.reverseChildren) children = children.slice().reverse();
    node.children = children
      .map(child => buildNode(child, rect, context))
      .filter(Boolean);

    if (!imageUrl && supportsRectangularFill(source, node.children.length > 0)) {
      const fill = readFillColor(source);
      if (fill) node.backgroundColor = colorToHex(fill, '');
    }

    return imageUrl || node.backgroundColor || node.children.length ? node : null;
  }

  function pageSize(layerDocument, rootSource, image) {
    const rootRect = effectiveRect(rootSource, null);
    if (rootRect && rootRect.width > 0 && rootRect.height > 0) {
      return { width: rootRect.width, height: rootRect.height, rect: rootRect };
    }

    const width = Math.max(1, firstNumber([
      layerDocument.width,
      layerDocument.board && layerDocument.board.width,
      image.width
    ], 1));
    const height = Math.max(1, firstNumber([
      layerDocument.height,
      layerDocument.board && layerDocument.board.height,
      image.height
    ], 1));
    return { width, height, rect: { x: 0, y: 0, width, height } };
  }

  function buildLayeredPage(image, detail, layerDocument, assets) {
    const rootSource = findLayerRoot(layerDocument);
    if (!rootSource) throw new Error('图层源没有可识别的根节点。');

    const size = pageSize(layerDocument, rootSource, detail || image);
    const context = makeContext(image, layerDocument, assets);
    let children = childrenOf(rootSource);
    if (context.reverseChildren) children = children.slice().reverse();
    const mappedChildren = children
      .map(child => buildNode(child, size.rect, context))
      .filter(Boolean);

    const rootName = String((detail && detail.name) || image.name || image.id);
    const root = baseNode(
      `${image.id}:root`,
      rootName,
      { x: 0, y: 0, width: size.width, height: size.height },
      { x: 0, y: 0, width: size.width, height: size.height },
      rootSource);
    root.children = mappedChildren;
    const rootFill = rootSource.fill && rootSource.fill.color
      ? rootSource.fill.color
      : rootSource.backgroundColor;
    if (rootFill) root.backgroundColor = colorToHex(rootFill, '');

    return {
      id: String(image.id),
      name: rootName,
      width: size.width,
      height: size.height,
      previewUrl: String((detail && detail.url) || image.url || ''),
      root
    };
  }

  function countNodes(node) {
    if (!node) return 0;
    return 1 + asArray(node.children).reduce((total, child) => total + countNodes(child), 0);
  }

  function normalizeHttpUrl(value) {
    if (typeof value !== 'string' || !value.trim()) return '';
    const url = value.trim();
    if (/^https?:\/\//i.test(url)) return url;
    if (url.startsWith('//')) return `https:${url}`;
    if (/^[a-z0-9.-]+\//i.test(url)) return `https://${url}`;
    return '';
  }

  function layerSourceUrl(detail) {
    const versions = asArray(detail && detail.versions);
    for (const version of versions) {
      const url = normalizeHttpUrl(version && (version.d2c_url || version.json_url));
      if (url) return url;
    }

    return normalizeHttpUrl(detail && (detail.d2c_url || detail.json_url));
  }

  function layerVersion(detail) {
    return asArray(detail && detail.versions).find(version =>
      normalizeHttpUrl(version && (version.d2c_url || version.json_url))) || detail || {};
  }

  function responseResult(payload) {
    if (!payload || typeof payload !== 'object') return null;
    if (payload.result) return payload.result;
    if (payload.data && payload.data.result) return payload.data.result;
    return payload.data || null;
  }

  async function fetchJson(url, credentials = 'omit', timeoutMs = 15000) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    try {
      const response = await fetch(url, { credentials, signal: controller.signal });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return response.json();
    } catch (error) {
      if (error && error.name === 'AbortError') {
        throw new Error(`请求超时（${Math.round(timeoutMs / 1000)} 秒）`);
      }

      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }

  async function mapConcurrent(items, limit, worker) {
    const result = new Array(items.length);
    let nextIndex = 0;
    async function run() {
      while (nextIndex < items.length) {
        const index = nextIndex++;
        result[index] = await worker(items[index], index);
      }
    }

    await Promise.all(Array.from({ length: Math.min(limit, items.length) }, run));
    return result;
  }

  const core = {
    buildLayeredPage,
    countNodes,
    findLayerRoot,
    layerSourceUrl,
    readImageUrl,
    readRect
  };
  if (testMode) {
    globalThis.__LANHU_EXPORTER_CORE__ = core;
    return core;
  }

  const query = new URLSearchParams(location.hash.split('?')[1] || '');
  const projectId = query.get('pid') || query.get('project_id');
  const teamId = query.get('teamId') || query.get('tid');
  if (!projectId || !teamId) {
    throw new Error('请先打开蓝湖项目页面。');
  }

  const loadedEndpoint = performance.getEntriesByType('resource')
    .map(entry => entry.name)
    .find(url => url.includes('/api/project/images?'));
  const endpoint = loadedEndpoint
    ? new URL(loadedEndpoint)
    : new URL('/api/project/images', location.origin);
  endpoint.searchParams.set('project_id', projectId);
  endpoint.searchParams.set('team_id', teamId);
  endpoint.searchParams.set('dds_status', '1');
  endpoint.searchParams.set('position', '1');
  endpoint.searchParams.set('show_cb_src', '1');
  endpoint.searchParams.set('comment', '1');

  console.info(`[蓝湖分层导出器 v${exporterVersion}] 正在读取项目...`);
  const projectPayload = await fetchJson(endpoint, 'include', 20000);
  if (projectPayload.code !== '00000' || !projectPayload.data) {
    throw new Error(projectPayload.msg || '蓝湖项目读取失败。');
  }

  const currentImageId = query.get('image_id');
  const images = asArray(projectPayload.data.images)
    .filter(image => image && image.id)
    .sort((left, right) => {
      if (!currentImageId) return 0;
      if (String(left.id) === currentImageId) return -1;
      if (String(right.id) === currentImageId) return 1;
      return 0;
    });
  const assets = [];
  const failures = [];
  const pages = await mapConcurrent(images, 8, async (image, index) => {
    try {
      const detailEndpoint = new URL('/api/project/image', location.origin);
      detailEndpoint.searchParams.set('dds_status', '1');
      detailEndpoint.searchParams.set('image_id', image.id);
      detailEndpoint.searchParams.set('team_id', teamId);
      detailEndpoint.searchParams.set('project_id', projectId);
      detailEndpoint.searchParams.set('all_versions', '0');
      const detailPayload = await fetchJson(detailEndpoint, 'include', 15000);
      const detail = responseResult(detailPayload);
      if (!detail) throw new Error(detailPayload.msg || '详情为空。');

      const version = layerVersion(detail);
      const sourceUrl = layerSourceUrl(detail);
      if (!sourceUrl) throw new Error('没有图层源，请确认该页面已上传标注数据。');
      const layerDocument = await fetchJson(sourceUrl, 'omit', 20000);
      const page = buildLayeredPage(image, detail, layerDocument, assets);
      page.revisionId = String(version.id || version.version_id || '');
      page.revisionTimestamp = String(
        version.updated_at || version.update_time || version.create_time || version.created_at || '');
      if (!page.root.children.length) throw new Error('图层源没有可生成的文本或切图。');
      console.info(`[蓝湖导出] ${index + 1}/${images.length} ${page.name}: ${countNodes(page.root)} 个节点`);
      return page;
    } catch (error) {
      failures.push({
        id: String(image.id),
        name: String(image.name || image.id),
        reason: error && error.message ? error.message : String(error)
      });
      console.warn(`[蓝湖导出] 已跳过 ${image.name || image.id}:`, error);
      return null;
    }
  });

  const validPages = pages.filter(Boolean);
  if (!validPages.length) {
    throw new Error('没有读取到可生成的真实图层。请在蓝湖标注页确认页面存在图层和切图后重试。');
  }

  const urlUseCount = new Map();
  for (const asset of assets) {
    urlUseCount.set(asset.url, (urlUseCount.get(asset.url) || 0) + 1);
  }
  for (const asset of assets) {
    if ((urlUseCount.get(asset.url) || 0) > 1) asset.shared = true;
  }

  const manifest = {
    schemaVersion,
    exporterVersion,
    id: projectPayload.data.id || projectId,
    name: projectPayload.data.name || document.title.replace(/\s*-\s*蓝湖$/, ''),
    source: 'Lanhu',
    teamId,
    sourceRevision: validPages
      .map(page => `${page.id}@${page.revisionId || page.revisionTimestamp || ''}`)
      .sort()
      .join('|'),
    sourceUpdatedAt: new Date().toISOString(),
    pages: validPages,
    assets
  };
  const result = {
    exporterVersion,
    project: manifest.name,
    pages: validPages.length,
    assets: assets.length,
    nodes: validPages.reduce((total, page) => total + countNodes(page.root), 0),
    skipped: failures
  };
  if (bridgeJobId) {
    window.postMessage({
      type: 'GDK_LANHU_SYNC_RESULT',
      jobId: bridgeJobId,
      manifest,
      summary: result
    }, location.origin);
    delete globalThis.__GDK_LANHU_SYNC_JOB__;
    return result;
  }

  const blob = new Blob([JSON.stringify(manifest, null, 2)], { type: 'application/json' });
  const anchor = document.createElement('a');
  anchor.href = URL.createObjectURL(blob);
  anchor.download = `${manifest.name || 'lanhu'}-layered-design-manifest.json`;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(anchor.href), 1000);
  return result;
})().catch(error => {
  const jobId = String(globalThis.__GDK_LANHU_SYNC_JOB__ || '');
  if (jobId) {
    window.postMessage({
      type: 'GDK_LANHU_SYNC_ERROR',
      jobId,
      error: error && error.message ? error.message : String(error)
    }, location.origin);
    delete globalThis.__GDK_LANHU_SYNC_JOB__;
  } else {
    console.error('[蓝湖分层导出器]', error);
  }
  throw error;
});

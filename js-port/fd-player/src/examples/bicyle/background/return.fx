(treeBaseY, groundLineY, bounds, zoomFactor) => {
  segmentLength: 300;

  chunkMin: math.floor(bounds.minX / segmentLength) - 1;
  chunkMax: math.ceil(bounds.maxX / segmentLength) + 1;
  chunkCount: chunkMax - chunkMin + 1;

  structures: range(0, chunkCount) map (chunkOffset) => {
    chunkIndex: chunkMin + chunkOffset;
    chunkStart: chunkIndex * segmentLength;

    world1: chunkStart - 120;
    visible1: if (world1 < bounds.minX - 80) then false else (if (world1 > bounds.maxX + 80) then false else true);
    structure1: if (visible1) then [
      tree([world1 - 12, treeBaseY + 0.4], 2.3),
      house([world1, treeBaseY], 1.2, 'house'),
      tree([world1 + 12, treeBaseY - 0.1], 1.6)
    ] else [];

    world2: chunkStart - 60;
    visible2: if (world2 < bounds.minX - 80) then false else (if (world2 > bounds.maxX + 80) then false else true);
    structure2: if (visible2) then [
      tree([world2 - 14, treeBaseY + 0.5], 2.4),
      house([world2, treeBaseY], 1.4, 'building'),
      tree([world2 + 14, treeBaseY - 0.2], 1.7)
    ] else [];

    world3: chunkStart;
    visible3: if (world3 < bounds.minX - 80) then false else (if (world3 > bounds.maxX + 80) then false else true);
    structure3: if (visible3) then [
      tree([world3 - 10, treeBaseY + 0.3], 2.1),
      house([world3, treeBaseY], 1, 'house'),
      tree([world3 + 10, treeBaseY - 0.1], 1.5)
    ] else [];

    world4: chunkStart + 60;
    visible4: if (world4 < bounds.minX - 80) then false else (if (world4 > bounds.maxX + 80) then false else true);
    structure4: if (visible4) then [
      tree([world4 - 12, treeBaseY + 0.6], 2.6),
      house([world4, treeBaseY], 1.3, 'tower'),
      tree([world4 + 12, treeBaseY - 0.2], 1.8)
    ] else [];

    world5: chunkStart + 120;
    visible5: if (world5 < bounds.minX - 80) then false else (if (world5 > bounds.maxX + 80) then false else true);
    structure5: if (visible5) then [
      tree([world5 - 12, treeBaseY + 0.4], 2.2),
      house([world5, treeBaseY], 1.1, 'house'),
      tree([world5 + 12, treeBaseY - 0.1], 1.6)
    ] else [];

    return [structure1, structure2, structure3, structure4, structure5];
  };

  fillerTrees: [];

  backgroundPadding: 200;
  sceneWidth: bounds.maxX - bounds.minX + backgroundPadding * 2;
  sceneStartX: bounds.minX - backgroundPadding;
  viewWidth: bounds.maxX - bounds.minX;
  viewHeight: bounds.maxY - bounds.minY;
  skyHeight: bounds.maxY - groundLineY;

  sky: {
    type: 'rect';
    data: {
      position: [sceneStartX, groundLineY];
      size: [sceneWidth, bounds.maxY - groundLineY];
      fill: '#bae6fd';
      stroke: '#93c5fd';
      width: 0.2;
    };
  };

  meadow: {
    type: 'rect';
    data: {
      position: [sceneStartX, bounds.minY];
      size: [sceneWidth, groundLineY - bounds.minY];
      fill: '#15803d';
      stroke: '#166534';
      width: 0.2;
    };
  };

  infiniteScale: zoomFactor ?? 1;

  sunCenter: [
    bounds.minX + viewWidth * 0.15,
    groundLineY + skyHeight * 0.85
  ];
  sunElement: sun(sunCenter, infiniteScale);

  cloudCount: 6;
  horizontalPadding: 0.12;
  cloudRows: [0.68, 0.78, 0.88];
  rowCount: cloudRows.length;
  clouds: range(0, cloudCount) map (index) => {
    fraction: if (cloudCount = 1) then 0.5 else index / (cloudCount - 1);
    usableWidth: 1 - horizontalPadding * 2;
    centerX: bounds.minX + viewWidth * (horizontalPadding + usableWidth * fraction);
    rowIndex: index % rowCount;
    baseFactor: cloudRows[rowIndex];
    sinusOffset: math.sin(index * 1.1) * 0.03;
    heightFactorRaw: baseFactor + sinusOffset;
    minCloudHeight: 0.4;
    maxCloudHeight: 0.95;
    heightFactor: if (heightFactorRaw < minCloudHeight) then minCloudHeight else (if (heightFactorRaw > maxCloudHeight) then maxCloudHeight else heightFactorRaw);
    baseY: groundLineY + skyHeight * heightFactor;
    return cloud(centerX, baseY, infiniteScale);
  };

  roadPadding: 120;
  roadWidth: 1.8;
  roadOffsetBelowGround: 0.4;
  roadLength: bounds.maxX - bounds.minX + roadPadding * 2;
  roadStartX: bounds.minX - roadPadding;
  roadY: groundLineY - roadOffsetBelowGround - roadWidth;

  asphalt: {
    type: 'rect';
    data: {
      position: [roadStartX, roadY];
      size: [roadLength, roadWidth];
      fill: '#111827';
      stroke: '#0f172a';
      width: 0.4;
    };
  };

  markerLength: 8;
  markerGap: 6;
  markerHeight: 0.2;
  markerStep: markerLength + markerGap;
  markerVisibleWidth: bounds.maxX - bounds.minX + roadPadding * 2;
  markerCount: math.ceil(markerVisibleWidth / markerStep) + 2;
  markerFirstX: math.floor((bounds.minX - roadPadding) / markerStep) * markerStep;
  laneMarkers: range(0, markerCount) map (index) => {
    startX: markerFirstX + index * markerStep;
    return {
      type: 'rect';
      data: {
        position: [startX, roadY + roadWidth / 2 - markerHeight / 2];
        size: [markerLength, markerHeight];
        fill: '#fef08a';
        stroke: '#fde047';
        width: 0.15;
      };
    };
  };

  roadElements: [asphalt, laneMarkers];

  return [sky, meadow, sunElement, clouds, roadElements, fillerTrees, structures];
};

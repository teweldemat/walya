(centerX, baseY, scale) => {
  sizeScale: scale ?? 1;
  puffRadius: 4.5 * sizeScale;
  verticalBump: 1.2 * sizeScale;
  horizontalOffset: 4 * sizeScale;
  tailOffset: 0.2 * sizeScale;
  colorFill: '#f8fafc';
  colorStroke: '#cbd5f5';
  strokeWidth: 0.2 * sizeScale;

  return [
    {
      type: 'circle';
      data: {
        center: [centerX - horizontalOffset, baseY];
        radius: puffRadius;
        fill: colorFill;
        stroke: colorStroke;
        width: strokeWidth;
      };
    },
    {
      type: 'circle';
      data: {
        center: [centerX, baseY + verticalBump];
        radius: puffRadius * 1.2;
        fill: colorFill;
        stroke: colorStroke;
        width: strokeWidth;
      };
    },
    {
      type: 'circle';
      data: {
        center: [centerX + horizontalOffset + tailOffset, baseY];
        radius: puffRadius;
        fill: colorFill;
        stroke: colorStroke;
        width: strokeWidth;
      };
    }
  ];
};

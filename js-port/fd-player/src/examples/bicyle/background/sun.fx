(center, scale) => {
  sizeScale: scale ?? 1;
  radius: 6 * sizeScale;
  strokeWidth: 0.35 * sizeScale;

  return {
    type: 'circle';
    data: {
      center,
      radius,
      fill: '#fde047';
      stroke: '#facc15';
      width: strokeWidth;
    };
  };
};

{
  baseColor: '#38bdf8';
  accent: '#f97316';
  background: '#0f172a';
  hand: (len, w, c, speed) => {
    angle: -t * speed;
    cos: Math.cos(angle);
    sin: Math.sin(angle);
    point: (x, y) => [x * cos - y * sin, x * sin + y * cos];
    base: -0.9;
    return {
      type: 'polygon',
      data: {
        points: [
          point(0, len),
          point(w / 2, base),
          point(-w / 2, base)
        ],
        fill: c
      }
    };
  };
  
  tickRadius: 7.5;
  ticks:range(0,12) map (i) => {
    angle: i * (2 * Math.PI / 12);
    return {
      type: 'circle',
      data: {
        center: [Math.sin(angle) * tickRadius, Math.cos(angle) * tickRadius],
        radius: 0.15,
        fill: baseColor
      }
    };
  };
  
  frame: [{
    type: 'circle',
    data: {
      center: [0, 0],
      radius: 8,
      stroke: baseColor,
      width: 0.3,
      fill: 'rgba(15, 23, 42, 0.3)'
    }
  },ticks];

  return [
    frame,
    hand(4, 0.6, accent, 0.5),
    hand(6, 0.4, baseColor, 6),
    hand(7, 0.2, accent, 360)
  ];
}

{
  outerRadius: 9.0;
  innerRadius: 1.0;
  wheelToWheel: 25.0;
  gearRadius: 2.0;
  gearTeeth: 12.0;
  gearRatio: 0.6;
  speed: 1;
  baseHalfViewWidth: 30;
  baseViewHeight: 40;
  groundOffsetFromBottom: 11;
  baseViewMinX: -baseHalfViewWidth;

  pedalAngle: speed * t;
  rearWheelToGear: wheelToWheel / 2;
  wheelAngle: pedalAngle / gearRatio;

  startZoomTime: 5.0;
  endZoomTime: 10.0;
  zoomRaw: (t - startZoomTime) / (endZoomTime - startZoomTime);
  zoomProgress: if (zoomRaw < 0) then 0 else if (zoomRaw > 1) then 1 else zoomRaw;
  zoomFactor: 1 + zoomProgress * 9;
  halfViewWidth: baseHalfViewWidth * zoomFactor;
  viewHeight: baseViewHeight * zoomFactor;
  viewWidth: halfViewWidth * 2;
  travelSpan: viewWidth + wheelToWheel * 2;
  traveledDistance: wheelAngle * outerRadius;
  wrapCount: math.floor(traveledDistance / travelSpan);
  offsetDistance: traveledDistance - wrapCount * travelSpan;
  leftWheelX: baseViewMinX - wheelToWheel + offsetDistance;
  groundY: 0;
  groundLineY: groundY - outerRadius;

  leftWheelCenter: [leftWheelX, groundY];
  rightWheelCenter: [leftWheelCenter[0] + wheelToWheel, groundY];
  frontGearCenter:[leftWheelCenter[0] + rearWheelToGear, groundY];

  viewCenterX: (leftWheelCenter[0] + rightWheelCenter[0]) / 2;
  viewMinX: viewCenterX - halfViewWidth;
  viewMaxX: viewCenterX + halfViewWidth;
  viewMinY: groundLineY - groundOffsetFromBottom;
  viewMaxY: viewMinY + viewHeight;
  viewBounds: {
    minX: viewMinX;
    maxX: viewMaxX;
    minY: viewMinY;
    maxY: viewMaxY;
  };

  frameHeight: outerRadius * 1.6;
  bicycleState: {
    leftWheelCenter,
    rightWheelCenter,
    frontGearCenter,
    outerRadius,
    innerRadius,
    gearRadius,
    gearTeeth,
    gearRatio,
    pedalAngle,
    wheelAngle,
    frameHeight
  };
  bicycleGraphics: bicycle.model(bicycleState);

  treeBaseY: groundLineY;
  backgroundElements: background(treeBaseY, groundLineY, viewBounds, zoomFactor);

  graphics: [
    backgroundElements,
    bicycleGraphics
  ];

  return {
    graphics,
    frameHeight,
    wheelAngle,
    pedalAngle,
    leftWheelCenter,
    rightWheelCenter,
    frontGearCenter,
    groundLineY,
    viewBounds
  };
}

(state) => {
  leftWheelCenter: state.leftWheelCenter;
  rightWheelCenter: state.rightWheelCenter;
  frontGearCenter: state.frontGearCenter;
  outerRadius: state.outerRadius;
  innerRadius: state.innerRadius;
  gearRadius: state.gearRadius;
  gearTeeth: state.gearTeeth;
  gearRatio: state.gearRatio;
  pedalAngle: state.pedalAngle;
  wheelAngle: state.wheelAngle;
  frameHeight: state.frameHeight;

  leftWheel: lib.wheel(
    leftWheelCenter,
    outerRadius,
    innerRadius,
    wheelAngle
  );

  rightWheel: lib.wheel(
    rightWheelCenter,
    outerRadius,
    innerRadius,
    wheelAngle
  );

  theDrive: drive(
    frontGearCenter,
    leftWheelCenter,
    gearRadius,
    gearTeeth,
    gearRatio,
    pedalAngle
  );

  theFrame: frame(
    leftWheelCenter,
    rightWheelCenter,
    frontGearCenter,
    frameHeight
  );

  return [
    theDrive.pedal1,
    leftWheel,
    rightWheel,
    theDrive.gear2,
    theDrive.gear1,
    theDrive.chain1,
    theDrive.chain2,
    theFrame,
    theDrive.pedal2
  ];
};

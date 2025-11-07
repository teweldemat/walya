# FuncScript Test Framework

The FuncScript Test Framework provides a lightweight way to validate FuncScript expressions by simulating input data and asserting on deterministic results.

## Overview

A FuncScript expression **A** can be tested using another FuncScript expression **T**. During a test run, the framework intercepts calls to `FsDataProvider.get` within **A** and replaces them with mock values supplied by **T**. This makes it possible to run repeatable tests without depending on upstream systems.

## Defining Tests

Each test script returns one or more `testSuit` objects (analogous to test suites). A `testSuit` defines:

- `cases`: Mock input values for intercepted symbols.
- `tests`: Assertion expressions executed on the evaluated result.

### Example
Script to test
```funcscript

```

```funcscript
{
  testSuit: {
    cases: [
      { "x": 10, "y": 20 },
      { "x": -5, "y": 15 }
    ],
    tests: [
      (resData,caseData) => assert.noerror(res),
      (resData,caseData) => assert.equal(res.sum, 30),
      (resData,caseData) => assert.isnotnull(res)
    ]
  }

  return [testSuit];
}
```

In this example:

- Each entry in `cases` defines a different input scenario.
- Each function in `tests` performs assertions on the evaluated result of expression **A**.

## Assertions

The framework provides a collection of built-in predicates under the `assert` namespace. You can combine them freely inside your test expressions.

### Standard Assertions

| Function | Description |
| --- | --- |
| `assert.equal(a, b)` | Passes if `a` is equal to `b`. |
| `assert.notEqual(a, b)` | Passes if `a` is not equal to `b`. |
| `assert.true(expr)` | Passes if `expr` is `true`. |
| `assert.false(expr)` | Passes if `expr` is `false`. |
| `assert.greater(a, b)` | Passes if `a > b`. |
| `assert.less(a, b)` | Passes if `a < b`. |
| `assert.approx(a, b, eps)` | Passes if the absolute difference between `a` and `b` is less than or equal to `eps`. |

### Error and Null Handling Assertions

| Function | Description |
| --- | --- |
| `assert.noerror(res)` | Passes if `res` does not represent an error. |
| `assert.iserror(res)` | Passes if `res` represents any error. |
| `assert.iserrortype(res, typeName)` | Passes if `res` is an error of the specified type. |
| `assert.hasErrorMessage(res, msg)` | Passes if the error message of `res` matches or contains `msg`. |
| `assert.isnull(value)` | Passes if `value` is `null`. |
| `assert.isnotnull(value)` | Passes if `value` is not `null`. |

These predicates make it easy to validate both normal and exceptional results from FuncScript expressions.

## Execution Flow

1. The framework intercepts `FsDataProvider.get` calls inside the tested expression **A**.
2. For each mock case defined in `cases`, the specified symbols are substituted with the provided mock values.
3. Expression **A** executes with the substituted data.
4. The resulting value is passed to each assertion function in `tests`.
5. Assertion outcomes are reported per case and per test.

## Return Structure

Each test script must return an array of `testSuit` objects:

```javascript
return [testSuit1, testSuit2, ...];
```

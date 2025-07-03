<p align="center">
<img src="https://raw.githubusercontent.com/dlidstrom/Sprout/refs/heads/main/logo.png" height="128" width="128" />
</p>

# 🌱 Sprout: BDD Testing for F#

> *Sprout is a lightweight F# test DSL with a clean, composable structure built on computation expressions. This allows your test suites to grow almost organically - i.e. sprout.*

[![Build](https://github.com/dlidstrom/Sprout/actions/workflows/build.yml/badge.svg)](https://github.com/dlidstrom/Sprout/actions/workflows/build.yml)
[![NuGet version](https://badge.fury.io/nu/dlidstrom.Sprout.svg)](https://badge.fury.io/nu/dlidstrom.Sprout)

🔥 Latest news

- Refactoring for improved extensibility.
  - Custom runner support has been added.
  - Test ordering is now customizable.

## ✅ Features

- Minimalist & expressive BDD-style syntax
- Nestable `describe` blocks for organizing tests to any depth or complexity
- Computation expressions for `it`, `beforeEach`, `afterEach`
- Asynchronous blocks supported
- Pending tests support
- Logging for improved traceability
- Built-in assertions: `shouldEqual`, `shouldBeTrue`, `shouldBeFalse`
  - These go along way to making your tests readable and expressive due to the
    descriptive text that goes along with each `describe` block and `it` test
    case. Note that you may use any exception-based constraints library if
    desired ([FsUnit.Light](https://github.com/Lanayx/FsUnit.Light) works
    beautifully).
- Support for parameterized tests using normal F# loops within `describe` blocks
- Pluggable reporters (console, silent, TAP, JSON)
- Built for F# — simple and idiomatic
  - No need for `<|` or functions wrapped in parentheses!
  - No need for complex combination of attributes - it's just all F# code!

Sprout provides a clean API for structuring tests. There are a minimum of
abstractions. `describe` blocks may be nested to any suitable depth. It is all
just a matter of *composing* the building blocks. Just like you would expect
coming from a functional programming background.

---

### 🚀 Getting Started

```bash
dotnet add package dlidstrom.Sprout
```

---

### 🧪 Example Test

```fsharp
#load "Sprout.fs"

open Sprout

let suite = describe "A test suite" {
  Info "Top level info message"

  // variables may be used to store state across tests
  let mutable b = false
  beforeEach { b <- true }
  it "should pass" {
    info "This test passes"

    // simple assertions included out-of-the-box
    b |> shouldBeTrue
  }

  it "should fail" {
    info "This test fails"
    failwith "Intentional failure"
  }

  // use pending to mark tests that are not yet implemented
  pending "This is a pending test"

  describe "Async works too" {
    Debug "Async test example"

    // asynchronous flows are supported
    it "should run asynchronously" {
      do! Async.Sleep 1000
      info "Async test completed"
    }
  }

  // use nested suites to organize tests
  describe "Arithmetic" {
    describe "Addition" {
      it "should add two numbers correctly" {
        let result = 2 + 2
        result |> shouldEqual 4
      }
    }

    describe "Multiplication" {
      it "should multiply two numbers correctly" {
        let result = 3 * 3
        result |> shouldEqual 9
      }
    }

    describe "Comparisons" {
      debug "Testing comparisons"
      it "should compare numbers correctly" {
        5 > 3 |> shouldBeTrue
      }
    }

    // parameterized tests are supported using regular F# loops
    // type-safe as expected without any special syntax
    describe "Parameterized Tests" {
      info "Simply embed test cases and loop over them"
      let numbers = [1; 2; 3; 4; 5]
      for n in numbers do
        it $"should handle number {n}" {
          n > 0 |> shouldBeTrue
        }
    }
  }
}

runTestSuiteCustom
  // id is used to order the tests (it blocks)
  // you can specify a custom ordering function if needed
  (DefaultRunner(Reporters.ConsoleReporter("v", "x", "?", "  "), id))
  suite
|> Async.RunSynchronously
```

Output:

![output](https://raw.githubusercontent.com/dlidstrom/Sprout/refs/heads/main/out.png)

---

### 📦 Blocks

| Name | Usage | Supported Expressions |
|-|-|-|
| `describe` | Declarative | `it`, `beforeEach`, `afterEach`, `it`, `Info`, `Debug` |
| `it` | Imperative | Any F# expressions, but typically exception-based assertions |
| `beforeEach`, `afterEach` | Imperative | Hook functions that run before and after test cases, respectively |

- **`describe`** - used to define a test suite, or a group of tests. Use the
  descriptive text to create descriptive sentences of expected behaviour.
- **`it`** - used to define test assertions, along with a descriptive text to
  define the expected behaviour
- **`beforeEach`/`afterEach`** - hook functions that run before and after test
  cases, respectively
- **`pending`** - used to denote a pending test case
- **`info`/`debug`** - tracing functions that may be used inside hook functions
  and `it` blocks
- **`Info`/`Debug`** - constructor functions that provide tracing inside
  `describe` blocks

> Tracing works slightly different in `describe` blocks due to limitations of me
> (most likely) and/or F# computation expressions.

---

### 🧩 Extending Sprout

You can plug in your own reporter:

```fsharp
type MyCustomReporter() =
  inherit TestReporter()
  with
    override _.Begin(totalCount) = ...
    override _.BeginSuite(name, path) = ...
    override _.ReportResult(result, path) = ...
    override _.EndSuite(name, path) = ...
    override _.Info(message, path) = ...
    override _.Debug(message, path) = ...
    override _.End(testResults) = ...
```

Use it like this:

```fsharp
let reporter = MyCustomReporter()

let failedCount =
  runTestSuiteCustom
    // id is used to order the tests (it blocks)
    // you can specify a custom ordering function if needed
    (DefaultRunner(reporter, id))
    suite
  |> Async.RunSynchronously
```

Available reporters (available in the `Sprout.Reporters` namespace):

| Reporter | Description |
| -------- | ----------- |
| `ConsoleReporter` | Outputs test results to the console |
| `TapReporter` | Outputs test results in TAP format |

---

It is also possible to modify the default or create your own runner. A simple
example is to modify the default runner to run tests in parallel:

```fsharp
let parallelRunner = {
  new DefaultRunner(Reporters.TapReporter(), id) with
    override _.SequenceAsync args = Async.Parallel args }
runTestSuiteCustom
  parallelRunner
  suite
|> Async.RunSynchronously
```

---

### 🏗️ Testing Sprout Itself

Sprout is tested using [Bash Automated Testing System](https://github.com/bats-core/bats-core).
You can run the tests using the following command:

```bash
bats .
```

### 📦 Package Info

|         |                             |
| ------- | --------------------------- |
| NuGet   | [`Sprout`](https://www.nuget.org/packages/dlidstrom.Sprout) |
| Target  | .NET Standard 2.0.          |
| License | MIT                         |
| Author  | Daniel Lidström             |

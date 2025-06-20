# Sprout: BDD Testing for F#

<img src="logo.png" height="128" width="128" />

## Usage

```fsharp
open Sprout

let suite = describe "A test suite" {
  beforeEach {
    debug "Before each test"
  }

  afterEach {
    debug "After each test"
  }

  it "should pass" {
    info "This test passes"
  }

  it "should fail" {
    info "This test fails"
    failwith "Intentional failure"
  }

  pending "This is a pending test"

  describe "Nested suite" {
    it "should also pass" {
      info "Nested test passes"
    }
  }

  describe "Arithmetic" {
    describe "Addition" {
      it "should add two numbers correctly" {
        let result = 2 + 2
        result |> shouldEqual 4
      }

      it "should handle negative numbers" {
        let result = -1 + -1
        result |> shouldEqual -2
      }
    }

    describe "Faulty Addition" {
      it "should fail when adding incorrect numbers" {
        let result = 2 + 2
        result |> shouldEqual 5
      }
    }
  }
}

runTestSuite suite
```

Output:

```txt
A test suite
Before each test
This test passes
After each test
  ✅ passed: should pass
Before each test
This test fails
After each test
  ❌ failed: should fail - Intentional failure
Before each test
After each test
  ❔ pending: This is a pending test
  Nested suite
  Before each test
  Nested test passes
  After each test
    ✅ passed: should also pass
  Arithmetic
    Addition
    Before each test
    After each test
      ✅ passed: should add two numbers correctly
    Before each test
    After each test
      ✅ passed: should handle negative numbers
    Faulty Addition
    Before each test
    After each test
      ❌ failed: should fail when adding incorrect numbers - Expected 5 but got 4
There were 2 test failures:
- A larger test suite / should fail - Intentional failure
- A larger test suite / Arithmetic / Faulty Addition / should fail when adding incorrect numbers - Expected 5 but got 4
Summary: 4 passed, 2 failed, 1 pending
```

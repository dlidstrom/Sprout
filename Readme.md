<img src="logo.png" height="128" width="128" />

# Sprout: BDD Testing for F#

[![Build](https://github.com/dlidstrom/Sprout/actions/workflows/build.yml/badge.svg)](https://github.com/dlidstrom/Sprout/actions/workflows/build.yml)

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

![output](out.png)

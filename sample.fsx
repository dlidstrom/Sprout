#load "Sprout.fs"

open Sprout

let suite = describe "A test suite" {
  Info "Top level info message"
  it "should pass" {
    info "This test passes"
  }

  it "should fail" {
    info "This test fails"
    failwith "Intentional failure"
  }

  pending "This is a pending test"

  describe "Nested suite" {
    Debug "Use beforeEach and afterEach for setup and teardown"
    beforeEach {
      debug "Before each test"
    }

    afterEach {
      debug "After each test"
    }
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
    }

    describe "Multiplication" {
      it "should multiply two numbers correctly" {
        let result = 3 * 3
        result |> shouldEqual 9
      }
    }
  }

  describe "Comparisons" {
    debug "Testing comparisons"
    it "should compare numbers correctly" {
      5 > 3 |> shouldBeTrue
    }
  }

  describe "Parameterized Tests" {
    info "Simply embed test cases and loop over them"
    let numbers = [1; 2; 3; 4; 5]
    for n in numbers do
      it $"should handle number {n}" {
        n > 0 |> shouldBeTrue
      }
  }
}

runTestSuiteWithContext suite { TestContext.Empty with Reporter = Reporters.ConsoleReporter("v", "x", "?") :> ITestReporter }

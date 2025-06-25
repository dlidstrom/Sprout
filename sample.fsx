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

runTestSuiteWithContext
  { TestContext.New with Reporter = Reporters.ConsoleReporter("v", "x", "?", "  ") :> ITestReporter }
  suite
|> Async.RunSynchronously

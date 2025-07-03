#load "Sprout.fs"

open Sprout

// This file contains a set of tests that demonstrate the features of the Sprout
// testing framework. It includes both synchronous and asynchronous tests, nested
// suites, and various assertions. The tests are designed to showcase the
// capabilities of the framework, including setup and teardown functions, logging,
// and custom runners.

let s1 = describe "Suite 1" {}
let s2 = describe "Suite 2" {
  beforeEach {
    debug "Before each test in Suite 2"
  }

  it "should pass in Suite 2" {
    info "This test passes in Suite 2"
  }
}

let suite = describe "A larger test suite" {
  Info "Top level info message"
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
    Debug "Use beforeEach and afterEach for setup and teardown"
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

let asyncSuite = describe "Async Tests" {
  beforeEach {
    let! x = async {
      debug "Before each async test"
      return 100
    }
    do! Async.Sleep x
  }

  it "should run an async test" {
    do! Async.Sleep 100
  }

  it "should handle async failure" {
    do! Async.Sleep 100
    failwith "Intentional async failure"
  }
}

[
  // run a suite on the fly, this one references the above suites
  runTestSuite (describe "Main Suite" { s1; s2 })

  // run the suite with a console reporter
  runTestSuite suite

  // run the suite with a tap reporter
  runTestSuiteCustom
    (DefaultRunner(Reporters.TapReporter(), id))
    suite

  // create a custom runner that runs tests in parallel
  let silentTapReporter = {
    new Reporters.TapReporter()
      with
        override _.ReportResult(_, _) = () }
  let parallelRunner = {
    new DefaultRunner(silentTapReporter, id) with
      override _.SequenceAsync args = Async.Parallel args }
  runTestSuiteCustom
    parallelRunner
    suite

  // run async tests
  runTestSuite asyncSuite
]
|> Async.Sequential
|> Async.RunSynchronously

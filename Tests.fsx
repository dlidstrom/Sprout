#load "Sprout.fs"

open Sprout

let s1 = describe "Suite 1" {}
let s2 = describe "Suite 2" {
  beforeEach {
    debug "Before each test in Suite 2"
  }

  it "should pass in Suite 2" {
    info "This test passes in Suite 2"
  }
}
runTestSuite (describe "Main Suite" { s1; s2 })

let suite = describe "A larger test suite" {
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

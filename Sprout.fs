module Sprout

let mutable info: string -> unit = ignore
let mutable debug: string -> unit = ignore

type LogLevel = Debug of string | Info of string

type HookFunction = unit -> Async<unit>
type EachFunction =
  | Before of f: HookFunction
  | After of f: HookFunction

type It = {
  Name: string
  Body: (unit -> Async<unit>) option
}
with
  static member Active name body = {
    Name = name
    Body = Some body
  }
  static member Pending name = {
    Name = name
    Body = None
  }

type Step =
  | ItStep of It
  | LogStatementStep of LogLevel

type Describe = {
  Name: string
  Steps: Step list
  Each: EachFunction list
  Children: Describe list
}
with
  member this.TotalCount =
    let rec count (d: Describe) =
      (d.Steps |> List.sumBy (function ItStep _ -> 1 | _ -> 0)) +
      (d.Children |> List.sumBy count)
    count this

  static member New name = {
    Name = name
    Steps = []
    Each = []
    Children = []
  }

type LogState = { Messages: string list }

type Path = Path of string list
with
  member this.Length =
    match this with
    | Path p -> List.length p
  member this.Value =
    match this with
    | Path p -> p
type TestOutcome =
  | Passed of Path * string
  | Failed of Path * string * exn
  | Pending of Path * string
type TestResult = {
  Outcome: TestOutcome
  Logs: LogLevel list
}
with
  member this.IsPassed =
    match this.Outcome with
    | Passed _ -> true
    | _ -> false
  member this.IsFailed =
    match this.Outcome with
    | Failed _ -> true
    | _ -> false
  member this.IsPending =
    match this.Outcome with
    | Pending _ -> true
    | _ -> false

type CollectedStep =
  | CollectedIt of Path * HookFunction list * HookFunction list * It
  | CollectedLog of Path * LogLevel

type ITestReporter =
  abstract Begin : totalCount:int -> unit
  abstract BeginSuite : name:string * path:Path -> unit
  abstract ReportResult : result:TestResult * path:Path -> unit
  abstract EndSuite : name:string * path:Path -> unit
  abstract Info : message:string * path:Path -> unit
  abstract Debug : message:string * path:Path -> unit
  abstract End : TestResult [] -> unit

module Builders =
  type EachBuilder(factory: (unit -> Async<unit>) -> EachFunction) =
    member _.Zero() = async { return () }
    member _.Return x = async { return x }
    member _.ReturnFrom(x: Async<unit>) = x
    member _.Delay(f: unit -> Async<unit>) = async.Delay f
    member _.Combine(a: Async<unit>, b: Async<unit>) = async {
      do! a
      do! b
    }
    member _.Bind(m: Async<'T>, f: 'T -> Async<unit>) = async.Bind(m, f)
    member _.Run(f: Async<unit>) = factory (fun() -> f)

  type ItBuilder(name: string) =
    member _.Zero() = async { return () }
    member _.Return x = async { return x }
    member _.ReturnFrom(x: Async<unit>) = x
    member _.Delay(f: unit -> Async<unit>) = async.Delay f
    member _.Combine(a: Async<unit>, b: Async<unit>) = async {
      do! a
      do! b
    }
    member _.Bind(m: Async<'T>, f: 'T -> Async<unit>) = async.Bind(m, f)
    member _.Run(f: Async<unit>) = It.Active name (fun () -> f)

  type DescribeBuilder(name) =
    member _.Zero() = Describe.New name
    member _.Yield(each: EachFunction) =
      { Describe.New name with Each = [each] }
    member _.Yield(tc: It) =
      { Describe.New name with Steps = [ItStep tc] }
    member _.Yield(log: LogLevel) =
      { Describe.New name with Steps = [LogStatementStep log] }
    member _.Yield(describe: Describe) =
      { Describe.New name with Children = [describe] }
    member _.Combine(a: Describe, b: Describe) =
      {
        Describe.New name
        with
          Each = a.Each @ b.Each
          Children = a.Children @ b.Children
          Steps = a.Steps @ b.Steps
      }
    member _.Delay(f: unit -> Describe) = f()
    member this.For(sequence: seq<'T>, body: 'T -> Describe) =
      let sb =
        sequence
        |> Seq.map body
        |> Seq.fold (fun s a -> this.Combine(s, a)) (Describe.New name)
      sb
    member _.Run(f: Describe) = f

let beforeEach = Builders.EachBuilder Before
let afterEach = Builders.EachBuilder After

let it name = Builders.ItBuilder name
let pending name = It.Pending name

let describe name = Builders.DescribeBuilder name

module Reporters =
  open System.Diagnostics

  module AnsiColours =
    let green = "\u001b[32m"
    let red = "\u001b[31m"
    let grey = "\u001b[90m"
    let white = "\u001b[37m"
    let reset = "\u001b[0m"

  type ConsoleReporter(passedChar, failedChar, pendingChar, indentString) =
    let sw = Stopwatch.StartNew()
    let indent (path: Path) = String.replicate (path.Length - 1) indentString

    new() =
      ConsoleReporter("✅", "❌", "❔", "  ")

    interface ITestReporter with
      member _.Begin totalCount =
        sw.Restart()
      member _.BeginSuite(name, path) =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.green}%s{name}%s{AnsiColours.reset}"

      member _.ReportResult(result, path) =
        let indent = indent path
        match result.Outcome with
        | Passed (_, name) ->
            printfn $"%s{indent}%s{AnsiColours.green}  %s{passedChar} passed: %s{name}%s{AnsiColours.reset}"
        | Failed (_, name, ex) ->
            printfn $"%s{indent}%s{AnsiColours.red}  %s{failedChar} failed: %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
        | Pending (_, name) ->
            printfn $"%s{indent}%s{AnsiColours.grey}  %s{pendingChar} pending: %s{name}%s{AnsiColours.reset}"

      member _.EndSuite(_, _) = ()
      member _.Debug(message: string, path: Path): unit =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.grey}%s{message}%s{AnsiColours.reset}"
      member _.Info(message: string, path: Path): unit =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.white}%s{message}%s{AnsiColours.reset}"
      member _.End(testResults: TestResult []): unit =
        let testFailures = testResults |> Array.filter _.IsFailed
        if Array.isEmpty testFailures then
          printfn $"All tests passed!"
        else
          printfn $"There were %d{Array.length testFailures} test failures:"
        testResults |> Array.iter (fun tr ->
          match tr.Outcome with
          | Failed (path, name, ex) ->
            let pathString = String.concat " / " path.Value
            printfn $"- %s{AnsiColours.red}%s{pathString} / %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
          | _ -> ())

        // Count results
        let passedCount = testResults |> Seq.filter _.IsPassed |> Seq.length
        let failedCount = testResults |> Seq.filter _.IsFailed |> Seq.length
        let pendingCount = testResults |> Seq.filter _.IsPending |> Seq.length

        printfn $"Summary: %d{passedCount} passed, %d{failedCount} failed, %d{pendingCount} pending"
        printfn $"Total time: %s{sw.Elapsed.ToString()}"

  type TapReporter() =
    interface ITestReporter with
      member this.Begin(totalCount: int): unit =
        printfn "TAP version 14"
        printfn "1..%d" totalCount
      member this.BeginSuite(name: string, path: Path): unit =
        ()
      member this.Debug(message: string, path: Path): unit =
        ()
      member this.End(arg1: TestResult []): unit = ()
      member this.EndSuite(name: string, path: Path): unit =
        ()
      member this.Info(message: string, path: Path): unit =
        ()
      member this.ReportResult(result: TestResult, path: Path): unit =
        match result.Outcome with
        | Passed (_, name) ->
          printf "ok %s\n" name
        | Failed (_, name, ex) ->
          printf
            "not ok %s\n  ---\n  message: %s\n  severity: fail\n  ...\n"
            name
            ex.Message
        | Pending (_, name) ->
          printf "ok %s # SKIP\n" name

type TestContext = {
  Path: Path
  ParentBeforeHooks: HookFunction list
  ParentAfterHooks: HookFunction list
  Reporter: ITestReporter
  Log: string -> unit
}
with
  static member New = {
    Path = Path []
    ParentBeforeHooks = []
    ParentAfterHooks = []
    Reporter = Reporters.ConsoleReporter() :> ITestReporter
    Log = printfn "%s"
  }

module Runner =
  let private runTestCase path (testCase: It) beforeHooks afterHooks: Async<TestResult> =
    async {
      // setup logging functions
      let info', debug' = info, debug
      use _ = { new System.IDisposable with
        member _.Dispose() = info <- info'; debug <- debug' }
      let mutable logs = []
      info <- fun s -> logs <- Info s :: logs
      debug <- fun s -> logs <- Debug s :: logs
      for hookFunction in beforeHooks do
        do! hookFunction()
      let! result =
        match testCase.Body with
        | Some body ->
          async {
            try
              do! body()
              return Passed (path, testCase.Name)
            with ex ->
              return Failed (path, testCase.Name, ex)
          }
        | None ->
          async {
            return Pending (path, testCase.Name)
          }
      for hookFunction in afterHooks do
        do! hookFunction()
      return {
        Outcome = result
        Logs = List.rev logs
      }
    }

  let collectSteps (root: Describe) =
    let rec loop (parentPath: string list) (parentBefore: HookFunction list) (parentAfter: HookFunction list) (d: Describe) =
      let beforeHooks, afterHooks =
        d.Each
        |> List.fold (fun (be, af) hook ->
          match hook with
          | Before hookFunction -> hookFunction :: be, af
          | After hookFunction -> be, hookFunction :: af
        ) ([], [])
      let beforeHooks = List.rev beforeHooks @ parentBefore
      let afterHooks = parentAfter @ List.rev afterHooks
      let path = parentPath @ [d.Name]
      let steps =
        d.Steps
        |> List.map (function
          | ItStep it -> CollectedIt (Path path, beforeHooks, afterHooks, it)
          | LogStatementStep log -> CollectedLog (Path path, log))
      let children =
        d.Children
        |> List.collect (loop path beforeHooks afterHooks)
      steps @ children
    loop [] [] [] root

  let runCollectedSteps (context: TestContext) (steps: CollectedStep list) (order: CollectedStep list -> CollectedStep list) =
    async {
      let orderedSteps = order steps
      let! results =
        orderedSteps
        |> List.map (function
          | CollectedIt (path, beforeHooks, afterHooks, it) ->
              async {
                let! result = runTestCase path it beforeHooks afterHooks
                // Report logs and result
                for log in result.Logs do
                  match log with
                  | Info message -> context.Reporter.Info(message, path)
                  | Debug message -> context.Reporter.Debug(message, path)
                context.Reporter.ReportResult(result, path)
                return Some result
              }
          | CollectedLog (path, log) ->
              async {
                match log with
                | Info message -> context.Reporter.Info(message, path)
                | Debug message -> context.Reporter.Debug(message, path)
                return None
              })
        |> Async.Sequential
      let testResults = results |> Array.choose id
      context.Reporter.End testResults
      return testResults
    }

  let rec doRunTestSuite (suite: Describe) (context: TestContext): Async<TestResult []> =
    async {
      context.Reporter.BeginSuite(suite.Name, context.Path)

      let beforeHooks, afterHooks =
        suite.Each
        |> List.fold (fun (be, af) hook ->
          match hook with
          | Before hookFunction -> hookFunction :: be, af
          | After hookFunction -> be, hookFunction :: af
        ) ([], [])
      let beforeHooks = List.rev beforeHooks |> List.append context.ParentBeforeHooks
      let afterHooks = context.ParentAfterHooks |> List.append (List.rev afterHooks)

      let! testResults =
        suite.Steps
        |> List.map (function
          | ItStep itCase ->
            async {
              let! itResult = runTestCase context.Path itCase beforeHooks afterHooks
              return Some itResult
            }
          | LogStatementStep (Info message) ->
            async {
              context.Reporter.Info(message, context.Path)
              return None
            }
          | LogStatementStep (Debug message) ->
            async {
              context.Reporter.Debug(message, context.Path)
              return None
            })
        |> Async.Sequential

      let itResults = testResults |> Array.choose id
      for itResult in itResults do
        for log in itResult.Logs do
          match log with
          | Info message -> context.Reporter.Info(message, context.Path)
          | Debug message -> context.Reporter.Debug(message, context.Path)
        context.Reporter.ReportResult(itResult, context.Path)

      let! childrenResults =
        suite.Children
        |> Seq.map (fun child ->
          let childContext =
            { context with
                ParentBeforeHooks = beforeHooks
                ParentAfterHooks = afterHooks
                Path = Path (context.Path.Value @ [child.Name]) }
          doRunTestSuite
            child
            childContext)
        |> Async.Sequential
      context.Reporter.EndSuite(suite.Name, context.Path)
      let allResults = Array.concat [| itResults; Array.concat childrenResults |]
      return allResults
    }

let runTestSuiteWithContext (context: TestContext) (suite: Describe) (order: CollectedStep list -> CollectedStep list) =
  async {
    context.Reporter.Begin suite.TotalCount
    let steps = Runner.collectSteps suite
    let! testResults = Runner.runCollectedSteps { context with Path = Path (context.Path.Value @ [suite.Name]) } steps order
    context.Reporter.EndSuite(suite.Name, context.Path)
    context.Reporter.End testResults
    return testResults |> Array.filter _.IsFailed
  }

let runTestSuite (describe: Describe) =
  runTestSuiteWithContext
    TestContext.New
    describe
    id

[<AutoOpen>]
module Constraints =
  let shouldEqual expected actual =
    if expected <> actual then
      failwithf "Expected %A but got %A" expected actual

  let shouldNotEqual unexpected actual =
    if unexpected = actual then
      failwithf "Expected not to be %A but got %A" unexpected actual

  let shouldBeTrue condition =
    if not condition then
      failwith "Expected condition to be true"

  let shouldBeFalse condition =
    if condition then
      failwith "Expected condition to be false"

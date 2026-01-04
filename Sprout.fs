module Sprout

let mutable info: string -> unit = ignore
let mutable debug: string -> unit = ignore

type LogLevel = Debug of string | Info of string

type HookFunction = unit -> Async<unit>
type HookFunctions = {
  Before: HookFunction list
  After: HookFunction list
}
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
  member this.HookFunctions =
    let beforeHooks, afterHooks =
      this.Each
      |> List.fold (fun (be, af) hook ->
        match hook with
        | Before hookFunction -> hookFunction :: be, af
        | After hookFunction -> be, hookFunction :: af
      ) ([], [])
    {
      Before = List.rev beforeHooks
      After = List.rev afterHooks
    }

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

type CollectedStep =
  | CollectedIt of Path * HookFunctions * It
  | CollectedLog of Path * LogLevel

type CollectedDescribe = {
  Name: string
  Path: Path
  Steps: CollectedStep list
  Children: CollectedDescribe list
}

[<AbstractClass>]
type TestReporter() =
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
    member _.Using(resource: 'T when 'T :> System.IDisposable, binder: 'T -> Async<unit>) = async.Using(resource, binder)
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
    member _.Using(resource: 'T when 'T :> System.IDisposable, binder: 'T -> Async<unit>) = async.Using(resource, binder)
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
    inherit TestReporter() with
      let sw = Stopwatch.StartNew()
      let indent (path: Path) = String.replicate (path.Length - 1) indentString

      new() =
        ConsoleReporter("✅", "❌", "❔", "  ")
      override _.Begin totalCount =
        sw.Restart()
      override _.BeginSuite(name, path) =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.green}%s{name}%s{AnsiColours.reset}"

      override _.ReportResult(result, path) =
        let indent = indent path
        match result.Outcome with
        | Passed (_, name) ->
            printfn $"%s{indent}%s{AnsiColours.green}  %s{passedChar} passed: %s{name}%s{AnsiColours.reset}"
        | Failed (_, name, ex) ->
            printfn $"%s{indent}%s{AnsiColours.red}  %s{failedChar} failed: %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
        | Pending (_, name) ->
            printfn $"%s{indent}%s{AnsiColours.grey}  %s{pendingChar} pending: %s{name}%s{AnsiColours.reset}"

      override _.EndSuite(_, _) = ()
      override _.Debug(message: string, path: Path): unit =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.grey}%s{message}%s{AnsiColours.reset}"
      override _.Info(message: string, path: Path): unit =
        let indent = indent path
        printfn $"%s{indent}%s{AnsiColours.white}%s{message}%s{AnsiColours.reset}"
      override _.End(testResults: TestResult []): unit =
        let testFailures = testResults |> Array.filter _.Outcome.IsFailed
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
        let passedCount = testResults |> Seq.filter _.Outcome.IsPassed |> Seq.length
        let failedCount = testResults |> Seq.filter _.Outcome.IsFailed |> Seq.length
        let pendingCount = testResults |> Seq.filter _.Outcome.IsPending |> Seq.length

        printfn $"Summary: %d{passedCount} passed, %d{failedCount} failed, %d{pendingCount} pending"
        printfn $"Total time: %s{sw.Elapsed.ToString()}"

  type TapReporter() =
    inherit TestReporter() with
      override this.Begin(totalCount: int): unit =
        printfn "TAP version 14"
        printfn "1..%d" totalCount
      override this.BeginSuite(name: string, path: Path): unit =
        ()
      override this.Debug(message: string, path: Path): unit =
        ()
      override this.End(arg1: TestResult []): unit = ()
      override this.EndSuite(name: string, path: Path): unit =
        ()
      override this.Info(message: string, path: Path): unit =
        ()
      override this.ReportResult(result: TestResult, path: Path): unit =
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

[<AbstractClass>]
type Runner() =
  abstract member Run: Describe -> Async<TestResult[]>
  abstract member CollectDescribes: Describe -> CollectedDescribe
  abstract member RunTestCase: Path -> It -> HookFunctions -> Async<TestResult>
  abstract member RunCollectedDescribe: CollectedDescribe -> Async<TestResult[]>
  abstract member SequenceAsync: Async<'T> list -> Async<'T array>

type StepsOrderingDelegate = CollectedStep list -> CollectedStep list
type LogDelegate = string -> unit

type DefaultRunner(reporter: TestReporter, order: StepsOrderingDelegate) =
  inherit Runner()
  override _.SequenceAsync xs = Async.Sequential xs
  override this.Run suite =
    async {
      reporter.Begin suite.TotalCount
      let collected = this.CollectDescribes suite
      let! testResults = this.RunCollectedDescribe collected
      reporter.EndSuite(suite.Name, Path [suite.Name])
      reporter.End testResults
      return testResults
    }
  override _.CollectDescribes describe =
    let rec loop parentPath parentHookFunctions (d: Describe) =
      let hookFunctions = d.HookFunctions
      let hookFunctions' = {
        Before = parentHookFunctions.Before @ hookFunctions.Before
        After = hookFunctions.After @ parentHookFunctions.After
      }
      let path = parentPath @ [d.Name]
      let steps =
        d.Steps
        |> List.map (function
          | ItStep it -> CollectedIt (Path path, hookFunctions', it)
          | LogStatementStep log -> CollectedLog (Path path, log))
      let children =
        d.Children
        |> List.map (loop path hookFunctions')
      {
          Name = d.Name
          Path = Path path
          Steps = steps
          Children = children
      }
    loop [] { Before = []; After = [] } describe

  override _.RunTestCase(path: Path) (testCase: It) hookFunctions: Async<TestResult> =
    async {
      // setup logging functions
      let info', debug' = info, debug
      use _ = { new System.IDisposable with
        member _.Dispose() = info <- info'; debug <- debug' }
      let mutable logs = []
      info <- fun s -> logs <- Info s :: logs
      debug <- fun s -> logs <- Debug s :: logs
      for hookFunction in hookFunctions.Before do
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
      for hookFunction in hookFunctions.After do
        do! hookFunction()
      return {
        Outcome = result
        Logs = List.rev logs
      }
    }

  override this.RunCollectedDescribe(cd: CollectedDescribe): Async<TestResult array> =
    async {
      reporter.BeginSuite(cd.Name, cd.Path)
      let! stepResults =
        cd.Steps
        |> order
        |> List.map (function
          | CollectedIt (path, hookFunctions, it) ->
            async {
              let! result = this.RunTestCase path it hookFunctions
              for log in result.Logs do
                match log with
                | Info message -> reporter.Info(message, path)
                | Debug message -> reporter.Debug(message, path)
              reporter.ReportResult(result, path)
              return Some result
            }
          | CollectedLog (path, log) ->
            async {
              match log with
              | Info message -> reporter.Info(message, path)
              | Debug message -> reporter.Debug(message, path)
              return None
            })
        |> this.SequenceAsync
      let! childResults =
        cd.Children
        |> List.map this.RunCollectedDescribe
        |> this.SequenceAsync
      reporter.EndSuite(cd.Name, cd.Path)
      let allResults =
        Array.concat [
          Array.choose id stepResults
          Array.concat childResults
        ]
      return allResults
    }

let runTestSuiteCustom (runner: Runner) (describe: Describe) =
  async {
    return! runner.Run describe
  }

let runTestSuite (describe: Describe) =
  async {
    let reporter = Reporters.ConsoleReporter()
    return! runTestSuiteCustom (DefaultRunner(reporter, id)) describe
  }

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

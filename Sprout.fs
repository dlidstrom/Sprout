module Sprout

let mutable info: string -> unit = ignore
let mutable debug: string -> unit = ignore

type HookFunction = unit -> unit
type EachFunction = Before of f: HookFunction | After of f: HookFunction
type LogLevel = Debug of string | Info of string

type EachBuilder(factory: (unit -> unit) -> EachFunction) =
  member _.Zero() = ()
  member _.Delay(f: unit -> unit) = f
  member _.Run(f: (unit -> unit)) = factory f
let beforeEach = EachBuilder Before
let afterEach = EachBuilder After

type It = {
  Name: string
  Body: (unit -> unit) option
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

type ItBuilder(name: string) =
  member _.Zero() = ()
  member _.Delay(f: unit -> unit) = f
  member _.Run (f: unit -> unit) = It.Active name f

let it name = ItBuilder name
let pending name = It.Pending name

type DescribeStep =
  | It of It
  | LogStatement of LogLevel

type Describe = {
  Name: string
  Steps: DescribeStep list
  Each: EachFunction list
  Children: Describe list
}
with
  static member Empty name = {
    Name = name
    Steps = []
    Each = []
    Children = []
  }

type DescribeBuilder(name) =
  member _.Zero() = Describe.Empty name
  member _.Yield(each: EachFunction) =
    { Describe.Empty name with Each = [each] }
  member _.Yield(tc: It) =
    { Describe.Empty name with Steps = [It tc] }
  member _.Yield(log: LogLevel) =
    { Describe.Empty name with Steps = [LogStatement log] }
  member _.Yield(describe: Describe) =
    { Describe.Empty name with Children = [describe] }
  member _.Combine(a, b) =
    {
      Describe.Empty name
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
      |> Seq.fold (fun s a -> this.Combine(s, a)) (Describe.Empty name)
    sb
  member _.Run(f: Describe) = f

let describe name = DescribeBuilder name

type LogState = { Messages: string list }

type Path = Path of string list
with
  member this.Length =
    match this with
    | Path p -> List.length p
  member this.Value =
    match this with
    | Path p -> p
type TestResult =
  | Passed of Path * string
  | Failed of Path * string * exn
  | Pending of Path * string

type ITestReporter =
  abstract BeginSuite : name:string * path:Path -> unit
  abstract ReportResult : result:TestResult * path:Path -> unit
  abstract EndSuite : name:string * path:Path -> unit
  abstract Info : message:string * path:Path -> unit
  abstract Debug : message:string * path:Path -> unit
  abstract End : TestResult list -> unit

module Reporters =
  open System.Diagnostics

  module AnsiColours =
    let green = "\u001b[32m"
    let red = "\u001b[31m"
    let grey = "\u001b[90m"
    let white = "\u001b[37m"
    let reset = "\u001b[0m"

  type ConsoleReporter(passedChar, failedChar, pendingChar) =
    let sw = Stopwatch.StartNew()
    let indent (path: Path) = String.replicate ((path.Length - 1) * 2) " "

    new() =
      ConsoleReporter("✅", "❌", "❔")

    interface ITestReporter with
      member _.BeginSuite(name, path) =
        let indent = indent path
        printfn $"%s{indent}{AnsiColours.green}{name}{AnsiColours.reset}"

      member _.ReportResult(result, path) =
        let indent = indent path
        match result with
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
      member _.End(testResults: TestResult list): unit =
        let testFailures = testResults |> List.filter (function Failed _ -> true | _ -> false)
        if not (List.isEmpty testFailures) then
          printfn $"There were %d{List.length testFailures} test failures:"
        else
          printfn $"All tests passed!"
        testResults |> List.iter (function
          | Failed (path, name, ex) ->
            let pathString = String.concat " / " path.Value
            printfn $"- %s{AnsiColours.red}%s{pathString} / %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
          | _ -> ())

        // Count results
        let passedCount = testResults |> List.filter (function Passed _ -> true | _ -> false) |> List.length
        let failedCount = testResults |> List.filter (function Failed _ -> true | _ -> false) |> List.length
        let pendingCount = testResults |> List.filter (function Pending _ -> true | _ -> false) |> List.length

        printfn $"Summary: {passedCount} passed, {failedCount} failed, {pendingCount} pending"
        printfn $"Total time: %s{sw.Elapsed.ToString()}"

type TestContext = {
  Path: Path
  ParentBeforeHooks: HookFunction list
  ParentAfterHooks: HookFunction list
  Reporter: ITestReporter
  Log: string -> unit
}
with
  static member Empty = {
    Path = Path []
    ParentBeforeHooks = []
    ParentAfterHooks = []
    Reporter = Reporters.ConsoleReporter() :> ITestReporter
    Log = printfn "%s"
  }

type TestSuiteRunner = Describe -> TestContext -> unit

let private runTestCase path (testCase: It) beforeHooks afterHooks =
  // setup logging functions
  let info', debug' = info, debug
  use _ = { new System.IDisposable with
    member _.Dispose() = info <- info'; debug <- debug' }
  let logs = ResizeArray<LogLevel>()
  info <- fun s -> logs.Add (Info s)
  debug <- fun s -> logs.Add (Debug s)
  beforeHooks |> Seq.iter (fun hookFunction -> hookFunction())
  let result =
    match testCase.Body with
    | Some body ->
      try
        body()
        Passed (path, testCase.Name)
      with ex ->
        Failed (path, testCase.Name, ex)
    | None ->
      Pending (path, testCase.Name)
  afterHooks |> Seq.iter (fun hookFunction -> hookFunction())
  result, logs

let rec private doRunTestSuite (suite: Describe) (context: TestContext): TestResult list =
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

  let testResults = [
    for testCase in suite.Steps do
      match testCase with
      | It itCase ->
        runTestCase context.Path itCase beforeHooks afterHooks
      | LogStatement logStatement ->
        match logStatement with
        | Info message -> context.Reporter.Info(message, context.Path)
        | Debug message -> context.Reporter.Debug(message, context.Path)
  ]

  for result, logs in testResults do
    for log in logs do
      match log with
      | Info message -> context.Reporter.Info(message, context.Path)
      | Debug message -> context.Reporter.Debug(message, context.Path)
    context.Reporter.ReportResult(result, context.Path)

  let childrenResults =
    suite.Children
    |> List.map (fun child ->
      let childContext =
        { context with
            ParentBeforeHooks = beforeHooks
            ParentAfterHooks = afterHooks
            Path = Path (context.Path.Value @ [child.Name]) }
      doRunTestSuite
        child
        childContext)
  let allResults = (testResults |> List.map fst) @ List.concat childrenResults
  allResults

let runTestSuiteWithContext (sb: Describe) (context: TestContext) =
  let testResults = doRunTestSuite sb { context with Path = Path (context.Path.Value @ [sb.Name]) }
  context.Reporter.EndSuite(sb.Name, context.Path)
  context.Reporter.End testResults

let runTestSuite (sb: Describe) = runTestSuiteWithContext sb TestContext.Empty

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

module Sprout
open System.Diagnostics

let mutable info: string -> unit = ignore
let mutable debug: string -> unit = ignore

type HookFunction = unit -> unit
type EachFunction = Before of f: HookFunction | After of f: HookFunction

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

module AnsiColours =
  let green = "\u001b[32m"
  let red = "\u001b[31m"
  let grey = "\u001b[90m"
  let reset = "\u001b[0m"

type Describe = {
  Name: string
  TestCases: It list
  Each: EachFunction list
  Children: Describe list
}
with
  static member Empty name = {
    Name = name
    TestCases = []
    Each = []
    Children = []
  }

type DescribeBuilder(name) =
  member _.Zero() = Describe.Empty name
  member _.Yield(each: EachFunction) =
    { Describe.Empty name with Each = [each] }
  member _.Yield(tc: It) =
    { Describe.Empty name with TestCases = [tc] }
  member _.Yield(sub: Describe) =
    { Describe.Empty name with Children = [sub] }
  member _.Combine(a, b) =
    {
      Describe.Empty name
      with
        Each = a.Each @ b.Each
        Children = a.Children @ b.Children
        TestCases = a.TestCases @ b.TestCases
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

type TestContext = {
  Path: Path
  ParentBeforeHooks: HookFunction list
  ParentAfterHooks: HookFunction list
  IndentString: string
  Log: string -> unit
}
with
  static member Empty = {
    Path = Path []
    ParentBeforeHooks = []
    ParentAfterHooks = []
    IndentString = " "
    Log = printfn "%s"
  }

type TestSuiteRunner = Describe -> TestContext -> unit

let runTestCase path (testCase: It): TestResult =
  match testCase.Body with
  | Some body ->
    try
      body()
      Passed (path, testCase.Name)
    with ex ->
      Failed (path, testCase.Name, ex)
  | None ->
    Pending (path, testCase.Name)

let rec doRunTestSuite (sb: Describe) (context: TestContext) =
  let indentStr = String.replicate context.Path.Length context.IndentString

  // setup logging functions
  let info', debug' = info, debug
  use _ = { new System.IDisposable with
    member _.Dispose() = info <- info'; debug <- debug' }
  info <- fun s -> context.Log $"%s{indentStr}%s{s}"
  debug <- fun s -> context.Log $"%s{indentStr}%s{AnsiColours.grey}%s{s}%s{AnsiColours.reset}"

  info $"%s{indentStr}%s{sb.Name}"

  let beforeHooks, afterHooks =
    sb.Each
    |> List.fold (fun (be, af) hook ->
      match hook with
      | Before hookFunction -> hookFunction :: be, af
      | After hookFunction -> be, hookFunction :: af
    ) ([], [])
  let beforeHooks = List.rev beforeHooks |> List.append context.ParentBeforeHooks
  let afterHooks = context.ParentAfterHooks |> List.append (List.rev afterHooks)

  let testResults = [
    for testCase in sb.TestCases do
      beforeHooks |> Seq.iter (fun hookFunction -> hookFunction())
      runTestCase context.Path testCase
      afterHooks |> Seq.iter (fun hookFunction -> hookFunction())
  ]

  for result in testResults do
    match result with
    | Passed (_, name) ->
      context.Log $"%s{indentStr}%s{AnsiColours.green}  ✅ passed: %s{name}%s{AnsiColours.reset}"
    | Failed (_, name, ex) ->
      context.Log $"%s{indentStr}%s{AnsiColours.red}  ❌ failed: %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
    | Pending (_, name) ->
      context.Log $"%s{indentStr}%s{AnsiColours.grey}  ❔ pending: %s{name}%s{AnsiColours.reset}"

  let childrenResults =
    sb.Children
    |> List.map (fun child ->
      let childContext =
        { context with
            ParentBeforeHooks = beforeHooks
            ParentAfterHooks = afterHooks
            Path = Path (context.Path.Value @ [child.Name]) }
      doRunTestSuite
        child
        childContext)
  testResults @ List.concat childrenResults

let runTestSuite (sb: Describe) (context: TestContext) =
  let sw = Stopwatch.StartNew()
  let testResults = doRunTestSuite sb { context with Path = Path (context.Path.Value @ [sb.Name]) }
  context.Log $"got %d{List.length testResults} test results"
  testResults |> List.iter (function
    // | Passed (path, name) ->
    //   let pathString = String.concat " / " path.Value
    //   context.Log $"Test passed: %s{AnsiColours.green}%s{pathString} - %s{name}%s{AnsiColours.reset}"
    | Failed (path, name, ex) ->
      let pathString = String.concat " / " path.Value
      context.Log $"Test failed: %s{AnsiColours.red}%s{pathString} / %s{name} - %s{ex.Message}%s{AnsiColours.reset}"
    // | Pending (path, name) ->
    //   let pathString = String.concat " / " path.Value
    //   context.Log $"Test pending: %s{AnsiColours.grey}%s{pathString} / %s{name}%s{AnsiColours.reset}")
    | _ -> ())

  // Count results
  let passedCount = testResults |> List.filter (function Passed _ -> true | _ -> false) |> List.length
  let failedCount = testResults |> List.filter (function Failed _ -> true | _ -> false) |> List.length
  let pendingCount = testResults |> List.filter (function Pending _ -> true | _ -> false) |> List.length

  context.Log $"Summary: {passedCount} passed, {failedCount} failed, {pendingCount} pending"
  context.Log $"Total time: %s{sw.Elapsed.ToString()}"
  ()

[<AutoOpen>]
module Constraints =
  let shouldEqual expected actual =
    if expected <> actual then
      failwithf "Expected %A but got %A" expected actual
    else
      info $"Expected %A{expected} and got %A{actual} - test passed"
  let shouldNotEqual unexpected actual =
    if unexpected = actual then
      failwithf "Expected not to be %A but got %A" unexpected actual
    else
      info $"Expected not to be %A{unexpected} and got %A{actual} - test passed"

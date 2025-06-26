#!/bin/bash

@test "Tests.fsx" {
  dotnet fsi Tests.fsx > current.log
  run diff <(grep -v "Total time" < expected.log) <(grep -v "Total time" < current.log)
  [ "$status" -eq 0 ]
  printf 'Lines:\n'
  printf 'lines %s\n' "${lines[@]}" >&2
  printf 'output %s\n' "${output[@]}" >&2
}

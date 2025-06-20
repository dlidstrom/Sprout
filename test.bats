#!/bin/bash

@test "Tests.fsx" {
  dotnet fsi Tests.fsx > current.log
  run diff <(cat expected.log | grep -v "Total time") <(cat current.log | grep -v "Total time")
  [ "$status" -eq 0 ]
  printf 'Lines:\n'
  printf 'lines %s\n' "${lines[@]}" >&2
  printf 'output %s\n' "${output[@]}" >&2
}

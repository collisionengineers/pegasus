# Plan — PR-019

## Approach
Normalize whitespace to no search, render an inline invalid-search state for terms over 200 characters without invoking Core, and render explicit no-match copy whenever a valid active retained search has zero rows. Estimate: 3 files, under 70 lines.

## Governing docs
FRD-08 and design conventions require honest filter outcomes and supported accessible GET states; scope/pagination remain unchanged.

## Steps
1. Add page validation state and honest Razor branches.
2. Prove blank, overlong and populated-no-match requests; simplify.

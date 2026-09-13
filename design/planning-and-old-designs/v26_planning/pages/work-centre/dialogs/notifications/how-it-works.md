# Notifications — how it works

Read from the live source on 13 September 2026 (`src/Pegasus.Web/Pages/Shared/_Layout.cshtml`, `Pages/Index.cshtml.cs`, `Presentation/RailCountsPageFilter.cs`).

The bell opens a dialog listing the first ten Needs attention rows of the same office-wide snapshot the Work Centre reads (`GetOperationsSnapshot.MaximumAttentionRows = 10`, passed as `ViewData["AttentionRows"]`; on other pages the rail filter fetches the same ten). It is not per person, records nothing as read, and carries no event: it is a shortcut to the top of the Work Centre list.

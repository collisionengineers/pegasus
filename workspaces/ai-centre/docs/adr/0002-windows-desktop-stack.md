# ADR 0002: Windows desktop technology stack

- **Status:** Proposed — decision required before app bootstrap
- **Date:** 21 July 2026

## Context

The product needs a rich case workspace, image/PDF/document viewing, drag-and-drop, delegated Microsoft
authentication, background agent activity, secure local state, accessibility, controlled updates,
diagnostics, and reliable Windows packaging. Existing service code is TypeScript, but that alone must
not decide the desktop shell.

## Options to spike

1. WinUI 3 / .NET with WebView2 only where useful.
2. Tauri with a TypeScript UI and a Rust/native shell.
3. Electron with a TypeScript UI.

## Required evidence

Build the same small case-workspace spike in the leading options and compare:

- signed install, update, rollback, and enterprise deployment;
- accessibility, keyboard navigation, high DPI, multi-window, and screen-reader behaviour;
- PDF/image viewing, file handling, secure token storage, delegated Microsoft login, and deep links;
- background work, crash recovery, offline state, memory/start-up footprint, and diagnostics;
- automated unit, contract, UI, and packaged-app testing; and
- team maintainability and reuse of shared TypeScript contracts.

Record the choice, rejected alternatives, measured results, and packaging/security implications here.

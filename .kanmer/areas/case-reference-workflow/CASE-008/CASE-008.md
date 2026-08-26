---
id: CASE-008
type: ticket
title: >-
  Automatic DVSA vehicle lookup on any known registration, with mileage estimate
  prefill
status: done
area: case-reference-workflow
order: 820
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-20T18:22:51.244Z'
  review: '2026-08-20T18:41:32.103Z'
  verifying: '2026-08-20T19:11:46.731Z'
  done: '2026-08-20T20:52:07.315Z'
labels:
  - vehicle
  - automation
  - operator-reported
  - assessment
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
deployment: production
archived: false
created: '2026-08-20T18:22:48.653Z'
updated: '2026-08-26T14:34:43.986Z'
---

## Why

Operator, 2026-08-20, verbatim: *"It also autopopulates. So when a case comes in, or a vehicle, or anything with a reg, we do automatic DVSA MOT check and calculate the mileage as an estimate"* — and on the assessment page: *"Odometer Source should just be mileage"* (one Mileage input + Source dropdown, hints deleted).

Today a vehicle lookup only happens when staff click "Request vehicle lookup" under an edit lease, and only after the registration is Confirmed — so most cases never get DVSA evidence, and the assessment mileage field starts empty.

## What

- Any active case with a known current registration (Confirmed, else extracted Fact) and no lookup yet for that registration gets one enqueued automatically — the existing worker path (external-work queue → ProcessQueuedVehicleLookup → DVSA/DVLA adapters → VehicleMileagePolicy estimate) does the rest. Idempotent per case+registration; a corrected registration triggers one new lookup.
- Assessment vehicle section: one "Mileage" input + "Source" dropdown, hint sentences deleted; prefilled from the vehicle-evidence mileage estimate (source preselected) when staff haven't recorded values; other vehicle inputs prefill from lookup details where empty.

## How to verify

Allocate a case with a registration and run the worker sweep: a lookup work item exists without any staff action, and after processing the case's vehicle evidence holds the observation and estimate; the assessment page shows the estimated mileage prefilled. Re-running the sweep enqueues nothing new.

## Outcome

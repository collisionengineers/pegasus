# Plan — PR-016

## Approach
List at most 101 metadata candidates per selected mailbox, combine and sort them globally newest-first, take 100, then fetch/parse MIME only for those candidates. One extra candidate per mailbox is bounded and suffices to report truncation. Estimate: 2 files, about 100 lines.

## Governing docs
FRD-08's all-mailbox refinement and fixed bound are met without persistence, mutation or history reconstruction.

## Steps
1. Refactor the existing Graph adapter around bounded candidate collection.
2. Prove a later mailbox's newer match and global truncation/MIME bound; simplify.

## Simplification pass — 2026-08-20

- Reuse: applied — existing Graph folder metadata calls and canonical MIME reader are reused.
- Simplification: the two phases use one local candidate tuple; no new public abstraction or store.
- Efficiency: at most 101 metadata rows per selected mailbox and 100 MIME reads globally; newest-first selection prevents mailbox-order starvation.
- Altitude: bounding/fairness stays in the Graph adapter; Core continues to own the global 100-message policy.

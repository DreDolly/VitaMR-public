# Architecture

## Core Authority Model

Desktop VitaMR WPF is the chart authority. The Android companion is a phone-first front end and cache, not the record authority.

C# owns routing, validation, persistence, scoring, safety gates, raw capture, and final chart writes.

AI assists with reading, summarizing, proposing, and explaining. AI does not own the record.

## Evidence Model

- User memory provides context.
- Accepted records provide evidence.
- Raw source files remain sacred.
- A clue is not evidence.
- A claim is not evidence.
- Best-practice research is not a verified chart fact.

## Companion Model

The phone can display packets, send pending context, show Data Hunter state, and collect phone-first inputs. Direct offline phone writes into the medical record are blocked by design.


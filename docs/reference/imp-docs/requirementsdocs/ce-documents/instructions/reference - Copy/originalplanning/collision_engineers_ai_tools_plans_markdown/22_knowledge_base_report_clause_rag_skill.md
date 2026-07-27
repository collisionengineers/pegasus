# Knowledge Base / Report Clause RAG Skill

Generated: 2026-05-22

**Type:** File-search/RAG skill
**Priority:** Medium

## Objective

Let staff and engineers search approved internal guidance, standard clauses, prior decisions, SOPs, and report wording without free-form hallucination.

## Why it matters for Collision Engineers

The whiteboard shows SOP guides and the handover references AI assessment bots and CE documents. A controlled knowledge base can support consistency without replacing engineering judgment.

## Proposed shape

A retrieval-augmented skill indexes approved documents and returns grounded excerpts or draft wording with source references.

## Candidate tools / MCP methods / skill actions

- `search_internal_guidance(query)`
- `retrieve_standard_clause(topic)`
- `draft_internal_note_from_guidance(work_item_id, topic)`
- `summarize_sop(topic)`
- `flag_no_source_answer(query)`

## Inputs

- Approved SOPs
- communication style profile
- prior report clauses if permitted
- internal guidance
- query

## Outputs

- Grounded answer
- source document links
- draft clause
- no-source warning

## Guardrails

- Answer only from approved sources.
- No final expert opinions.
- Do not expose privileged/sensitive prior cases broadly.
- Version sources and retain citations.

## MVP implementation path

1. Create source approval list.
2. Index SOPs and communication profile.
3. Add retrieval-only responses.
4. Add draft clause mode with approval.

## Test / acceptance criteria

- Queries return relevant source-backed answers.
- No-source cases refuse to invent.
- Access controls by role.
- Source/version visible.

## Risks and open questions

- Sensitive prior reports.
- Source maintenance.
- RAG can retrieve irrelevant snippets if indexing poor.

## Project source basis

- CE Communication Style & Tone Profile.docx
- Collision Engineers Whiteboard.jam
- handover.docx
- OpenAI file search docs

## External reference basis

- OpenAI file search/vector store documentation: https://developers.openai.com/api/docs/assistants/tools/file-search

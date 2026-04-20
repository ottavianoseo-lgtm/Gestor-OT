---
name: context7-docs
description: |-
  Use this skill to fetch up-to-date documentation and code examples for any programming library, framework, SDK, or API.
  It should be used proactively before generating code that involves third-party technologies to ensure the most current syntax and best practices are followed.
  Example usage:
  - User asks to implement a feature using a specific library (e.g., "Use Polotno to edit images").
  - User asks about .NET 10 features.
  - User asks for a code snippet for a library like React, Next.js, or Prisma.
---

# Context7 Documentation Skill

This skill allows the agent to access a massive database of documentation and snippets via the Context7 MCP.

## Workflow

1. **Identify Technology:** When a task involves a specific library or framework, identify its name.
2. **Resolve Library ID:** Always start by calling `context7_resolve-library-id` with the library name and the specific query.
3. **Select Best Match:** Pick the most relevant library ID (format: `/org/project`).
4. **Query Documentation:** Call `context7_query-docs` using the resolved ID and the detailed user query to get precise instructions and snippets.
5. **Implement with Confidence:** Use the returned documentation to generate accurate, idiomatic, and up-to-date code.

## Guidelines
- Do not rely solely on training data for popular libraries; documentation might be outdated.
- Use this proactively when the user mentions a library name you are not 100% familiar with in its latest version.
- Prefer this tool over general web search for library-specific questions.

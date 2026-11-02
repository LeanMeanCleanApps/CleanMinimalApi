---
name: writing-a-skill
description: How to add or change a skill in .agents/skills - the open SKILL.md format, the house layout (rule, steps, example, don't, check), scope metadata, size limits, validation, and carrying shared skills to all five templates. Use when a skill is missing, wrong or out of date, or when the way we do something changes.
metadata:
  scope: maintainer
---

# Writing a skill

## The rule

A skill is a folder in `.agents/skills` named for the job, holding a `SKILL.md` in the open Agent Skills format, so any agent can load it. Skills describe how things are done here today. When the way we do something changes, the skill changes in the same pull request as the code.

Frontmatter:

| Field | Rules |
| --- | --- |
| `name` | Lowercase letters, digits and hyphens, at most 64 characters, the same as the folder |
| `description` | What it covers, then "Use when..." with the situations that should load it. At most 1,024 characters |
| `metadata.scope` | `shared` (identical in all five, shipped), `presentation` (this template only, shipped), `repository` (template repository workflow, not shipped) or `maintainer` (running the family, not shipped) |

The body uses the same sections every time: **The rule**, **Steps**, **Example** (real code with its path), **Don't** (mistakes we've actually made), **Check** (commands that prove it). Keep it under 500 lines; move long material into a `references/` folder beside `SKILL.md`.

## Steps

1. Look for an existing skill first; extend it rather than adding a near-duplicate.
2. Write the skill from the code as it is. Copy examples from real files and give their paths.
3. Add it to the skills table in `AGENTS.md` with a one-line "Use it when you...".
4. A `repository` or `maintainer` skill is left out of the template package in `template/template.csproj`.
5. Check the format by eye against the table above: the name matches the folder, the description says when to use it, and the file opens with the `---` frontmatter block.

6. A `shared`, `repository` or `maintainer` skill goes to all five templates (`family-sync`).

## Don't

- Write a skill for a plan or a pattern the code doesn't follow yet.
- Cite decisions, tickets or dates. The skill is the current answer, not its history.
- Put jokes in a skill.
- Give a skill a vague description; the description is what makes an agent load it.

## Check

``` bash
head -6 .agents/skills/<name>/SKILL.md   # name, description and scope are there
wc -l .agents/skills/<name>/SKILL.md     # under 500 lines
```

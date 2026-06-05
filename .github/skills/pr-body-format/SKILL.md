---
name: pr-body-format
description: "Use when creating or editing GitHub pull request descriptions so Markdown renders correctly every time. Keywords: PR summary format, gh pr create body, gh pr edit body, escaped newline issue, body-file template."
---

# PR Body Format

## Purpose

Ensure PR descriptions are consistently well-formed Markdown by using a real body file, never escaped newline strings.

## Primary Targets

- Pull request description fields created with `gh pr create` or updated with `gh pr edit`
- Temporary body file in `/tmp` (for example `/tmp/pr_body.md`)

## When To Use

- Any time creating a PR from the terminal.
- Any time fixing a malformed PR body that shows literal `\\n` sequences.
- Any time a PR needs standardized sections for summary and validation.

## Workflow

1. Build the PR body in a heredoc file with literal newlines:
   - `cat > /tmp/pr_body.md <<'EOF'`
   - Write headings and bullets as normal Markdown.
   - `EOF`
2. Create or edit PR using `--body-file`:
   - create: `gh pr create --base <base> --head <branch> --title "..." --body-file /tmp/pr_body.md`
   - edit: `gh pr edit <number> --body-file /tmp/pr_body.md`
3. Verify rendered content source is clean:
   - `gh pr view <number> --json body`
   - Confirm the body contains real line breaks and no literal `\\n` text.

## Required Template

Use this structure unless the user asks for a different format:

```markdown
## Summary
- Change 1.
- Change 2.
- Change 3.

## Validation
- `command 1`
- `command 2`
- `command 3`
```

## Output Format

- "PR body update: SUCCESS" or "PR body update: FAILED"
- Include PR URL and a one-line statement that formatting was verified.

## Guardrails

- Do not pass multiline bodies with escaped `\n` in a single shell string.
- Prefer sentence-case bullets ending with periods.
- Keep sections short and factual.
- Preserve user-provided wording when possible; only normalize formatting unless asked to rewrite content.
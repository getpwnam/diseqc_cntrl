---
name: github-project-issue-subissue-workflow
description: "Use when creating project issues/sub-issues and wiring hierarchy correctly in GitHub Projects. Keywords: gh issue create, gh project item-add, addSubIssue, parent issue, project hierarchy, stable-firmware project."
---

# GitHub Project Issue + Sub-Issue Workflow

## Purpose

Create issues and sub-issues correctly the first time for this repository's project board, including the parent-child hierarchy that GitHub Projects derives from issue relationships.

## Primary Targets

- Repository: `getpwnam/diseqc_cntrl`
- Project: `DiSEqC Stable Firmware + Interop Program`
- Project number: `1`
- Project owner: `getpwnam`

## When To Use

- User asks to create one or more issues for planned work.
- User asks for sub-issues under an existing parent issue.
- User asks to add issues to the project board and preserve hierarchy.

## Critical Gotcha

`gh project item-edit` cannot directly set the `Parent issue` field for project items.

Attempting this returns:
- `GraphQL: The field of type parent_issue is currently not supported. (updateProjectV2ItemFieldValue)`

Therefore, set hierarchy at the issue level with `addSubIssue` GraphQL mutation. The project `Parent issue` view derives from that relationship.

## Workflow

1. Create or update the parent issue.
- Use `gh issue create` or `gh issue edit`.
- Include scope, acceptance criteria, and dependencies.

2. Create child issues.
- Use `gh issue create` with consistent labels.
- Capture physical verification checkpoints in each child when relevant.

3. Add parent and child issues to project 1.
- `gh project item-add 1 --owner getpwnam --url "https://github.com/getpwnam/diseqc_cntrl/issues/<N>"`

4. Fetch issue node IDs.
- Parent ID:
  - `gh issue view <PARENT_NUM> --json id,number,title`
- Child IDs:
  - `gh issue view <CHILD_NUM> --json id,number,title`

5. Link each child under parent using GraphQL.
- `gh api graphql -f query='mutation($issueId:ID!,$subIssueId:ID!){ addSubIssue(input:{issueId:$issueId,subIssueId:$subIssueId}) { issue { number } subIssue { number } } }' -F issueId='<PARENT_ID>' -F subIssueId='<CHILD_ID>'`

6. Verify hierarchy with GraphQL (source of truth).
- `gh api graphql -f query='query($owner:String!,$repo:String!,$parent:Int!){ repository(owner:$owner,name:$repo){ issue(number:$parent){ number subIssues(first:50){ nodes{ number title } } } } }' -F owner='getpwnam' -F repo='diseqc_cntrl' -F parent=<PARENT_NUM>`
- Optional child parent check:
  - `gh api graphql -f query='query($owner:String!,$repo:String!,$n:Int!){ repository(owner:$owner,name:$repo){ issue(number:$n){ number parent{ number title } } } }' -F owner='getpwnam' -F repo='diseqc_cntrl' -F n=<CHILD_NUM>`

7. Mirror links in parent body for readability.
- Keep a `Sub-Issues` checklist in the parent body.
- Keep `Dependencies` explicit in parent and children.

## Recommended Label Set

Use phase/program taxonomy when applicable:
- `program:stable-firmware`
- `phase:<A-F>`
- `type:work-package`
- Area labels as needed:
  - `area:interop`
  - `area:firmware`
  - `area:managed`
  - `area:docs`
  - `area:testing`

## Body Skeleton

Use this issue body baseline:

```markdown
## Context
...

## Scope
- ...

## Acceptance Criteria
- ...

## Dependencies
Blocked by: #

## Physical Verification Checkpoints
- [ ] ...

## Notes
...
```

## Output Contract

After execution, report:
- Parent issue number and URL.
- Child issue numbers and URLs.
- Confirmation that all items were added to project 1.
- Confirmation that GraphQL parent/sub-issue verification succeeded.

## Guardrails

- Do not rely on project field editing for `Parent issue`.
- Prefer deterministic CLI commands and GraphQL over manual UI linking.
- Keep issue wording factual and test-oriented.
- Keep dependencies explicit and non-circular.

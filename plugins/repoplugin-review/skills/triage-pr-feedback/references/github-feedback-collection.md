# GitHub feedback collection

Re-probe first. Read-only commands below require an authenticated `gh` context and are templates, not authority to write.

```powershell
gh pr view <number> --json number,url,title,headRefName,baseRefName,state
gh api --paginate "repos/<owner>/<repo>/pulls/<number>/reviews?per_page=100"
gh api --paginate "repos/<owner>/<repo>/pulls/<number>/comments?per_page=100"
gh api --paginate "repos/<owner>/<repo>/issues/<number>/comments?per_page=100"
```

For GraphQL-only fields, keep pagination explicit:

```powershell
gh api graphql -f owner=<owner> -f name=<repo> -F number=<number> -f query='<query with pageInfo/endCursor>'
```

Do not use `gh pr review`, `gh pr comment`, GraphQL mutations, thread-resolution mutations, `git push`, or PR-state commands unless the user separately authorizes that exact external action.

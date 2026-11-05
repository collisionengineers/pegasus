# Running the classifier agent against the test instance

`src/pegasus-classifier-agent` is the mail classifier (MAIL-26): a Node
program that signs in to the Automation MCP server as `pegasus-automation`,
reads mail that is Unclassified or has no Case, and proposes a category and a
link. In Azure it is a Function App on a timer; here it runs as one sweep per
command, on the instance's machine, in dry run until the operator says
otherwise.

## Prerequisites on the instance

The settings file carries `Features__AutomationMcp=true`,
`AutomationMcp__UseDevelopmentKeys=true`, `AutomationMcp__ClientId=pegasus-automation`
and an `AutomationMcp__ClientSecret` of at least 32 characters (section 2 of
the skill); the Web refuses to start with the flag on and either of the last
two missing. The lifecycle sets
`AutomationMcp__PublicOrigin` to the run's public origin. The client
`pegasus-automation` is created, enabled with every scope, on its first token
request; development keys are made at each Web start, so a token does not
survive a restart and the agent simply fetches a new one.

## The .env

In `src/pegasus-classifier-agent/`, copy `.env.example` to `.env` (ignored)
and fill it:

```text
ANTHROPIC_API_KEY=<the operator's key>
PEGASUS_MCP_URL=https://<host>.<tailnet>.ts.net/mcp
PEGASUS_TOKEN_URL=https://<host>.<tailnet>.ts.net/connect/token
PEGASUS_CLIENT_ID=pegasus-automation
PEGASUS_CLIENT_SECRET=<the AutomationMcp__ClientSecret value>
CLASSIFIER_MODEL=claude-sonnet-5-5
CLASSIFIER_DRY_RUN=true
CLASSIFIER_BATCH=20
CLASSIFIER_SCAN_PAGES=10
CLASSIFIER_MAX_BUDGET_USD=2
```

Both URLs use the public origin: OpenIddict issues tokens for the host a
request arrived on, so a token fetched on one origin is refused on another.
Leave `CLASSIFIER_STORAGE_ACCOUNT` and `AZURE_CLIENT_ID` unset: the reviewed
record is kept in memory for the run, which means every sweep re-reads all
eligible mail. That is the cost of having no storage account, and it is the
right cost here.

## Run

```bash
cd src/pegasus-classifier-agent
npm ci && npm run build
npm start            # one sweep, then exit
```

Dry run logs each proposal and writes nothing to Pegasus. Turning
`CLASSIFIER_DRY_RUN` to `false` is the operator's call; in write mode the agent
sets categories and links and creates Cases (R714) on the instance only. The
operator chose write mode for the instance on 5 November 2026.

A systemd user timer stands in for the Azure timer (every 15 minutes), in
`~/.config/systemd/user/`:

```ini
# pegasus-classifier.service
[Service]
Type=oneshot
WorkingDirectory=<primary checkout>/src/pegasus-classifier-agent
ExecStart=<node> --env-file-if-exists=.env dist/cli.js
Environment=PATH=<node's directory>:/usr/local/bin:/usr/bin:/bin
TimeoutStartSec=14min

# pegasus-classifier.timer
[Timer]
OnCalendar=*:0/15
[Install]
WantedBy=timers.target
```

`systemctl --user enable --now pegasus-classifier.timer` starts it;
`journalctl --user -u pegasus-classifier` is its log. Rebuild (`npm ci && npm
run build`) after each move of the checkout (skill section 6).

## Claude Code on the instance's MCP

The MCP serves tailnet agents as well as the classifier (R679), with no
connector redirect, so Claude Code reaches it with a client-credentials token
from a `headersHelper`. The helper prints one JSON object of headers:

```bash
#!/usr/bin/env bash
set -euo pipefail
origin=https://<host>.<tailnet>.ts.net
curl -fsS -X POST "$origin/connect/token" \
  -d grant_type=client_credentials -d client_id=pegasus-automation \
  --data-urlencode "scope=${PEGASUS_TEST_MCP_SCOPES:-automation.cases automation.intake automation.mail automation.documents automation.assessment automation.jobs}" \
  --data-urlencode "client_secret=$PEGASUS_TEST_MCP_SECRET" \
  | python3 -c 'import json,sys; print(json.dumps({"Authorization": "Bearer " + json.load(sys.stdin)["access_token"]}))'
```

```bash
claude mcp add-json -s user pegasus-test \
  '{"type":"http","url":"https://<host>.<tailnet>.ts.net/mcp","headersHelper":"<path to the helper>"}'
claude mcp list    # pegasus-test ... Connected
```

A token without a scope is refused (403), so the helper names them.
`automation.send` is left out by default because a send from the instance
reaches real recipients; add it to `PEGASUS_TEST_MCP_SCOPES` only for a send
the operator names. Tokens last ten minutes and the development keys change
at each Web start; the helper runs again on reconnect (`/mcp` in Claude
Code). Calls are attributed to `pegasus-automation`, as the classifier's are.

## What to watch

- The Web log shows the token request and the MCP calls on the public origin.
- `CLASSIFIER_MAX_BUDGET_USD` bounds one sweep's Anthropic spend; the key is
  the operator's and the spend is theirs.
- The agent never reaches production: its MCP URL is the instance's.

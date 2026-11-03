# Running the classifier agent against the test instance

`src/pegasus-classifier-agent` is the mail classifier (MAIL-26): a Node
program that signs in to the Automation MCP server as `pegasus-automation`,
reads mail that is Unclassified or has no Case, and proposes a category and a
link. In Azure it is a Function App on a timer; here it runs as one sweep per
command, on the instance's machine, in dry run until the operator says
otherwise.

## Prerequisites on the instance

The settings file carries `Features__AutomationMcp=true`,
`AutomationMcp__UseDevelopmentKeys=true` and an `AutomationMcp__ClientSecret`
of at least 32 characters (section 2 of the skill). The lifecycle sets
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

Dry run logs each proposal and writes nothing to Pegasus. Read the log with
the operator; turning `CLASSIFIER_DRY_RUN` to `false` is their call, and in
write mode the agent sets categories and links on the instance only. For a
repeating run, a cron line or a systemd timer calling `npm start` stands in
for the Azure timer; the Azure cron is every 15 minutes.

## What to watch

- The Web log shows the token request and the MCP calls on the public origin.
- `CLASSIFIER_MAX_BUDGET_USD` bounds one sweep's Anthropic spend; the key is
  the operator's and the spend is theirs.
- The agent never reaches production: its MCP URL is the instance's.

# Codex tools: complete source-level inventory

**Snapshot date:** 19 July 2026  
**Codex source snapshot:** [`openai/codex` commit `0fb559f0f6e231a88ac02ea002d3ecd248e2b515`](https://github.com/openai/codex/commit/0fb559f0f6e231a88ac02ea002d3ecd248e2b515)  
**Scope:** Tools that the Codex model can call, plus the two tools exposed when Codex itself runs as an MCP server.

## Executive answer

Codex does **not** have one fixed tool list that appears in every session. It builds the model-visible list for each turn from:

1. Codex's built-in tool handlers.
2. Codex's first-party extensions, including goals, memories, skills, web search, and image generation.
3. Hosted and Responses-runtime tools, such as hosted web search and parallel tool calling.
4. Tools supplied by configured MCP servers, apps, connectors, and plugins.
5. Tools injected by the client or host application.
6. Dynamic tools supplied when an app-server thread starts.

The current source performs that assembly in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L579-L618). Feature flags, the model and provider, the operating system, authentication, configured services, the selected Codex mode, and host policy decide which entries are actually shown.

This report is therefore "complete" in two precise senses:

- It lists every fixed agent-callable name defined by, specially recognized in, or explicitly named as a Responses-runtime wrapper by the inspected Codex source snapshot.
- It identifies every open-ended tool family whose individual names cannot be listed globally because users, servers, plugins, apps, or host clients define them at runtime.

It does **not** count CLI subcommands, slash commands, skill packages, hooks, configuration keys, UI actions, or implementation class names as tools unless the model can call them as a tool. The `skills.list` and `skills.read` functions are tools; an individual skill package is not.

## 1. Shell, process, file, and image-inspection tools

Source: [`shell_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/shell_spec.rs), [`apply_patch_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/apply_patch_spec.rs), and [`view_image_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/view_image_spec.rs).

| Tool | Plain-English use case | Availability |
|---|---|---|
| `shell_command` | Run a command in the computer's normal shell and return the result. Typical uses are searching files, running tests, using Git, or invoking project tools. | The legacy/default command route. Codex normally exposes this **or** `exec_command`, depending on the model, platform, and configuration. |
| `exec_command` | Start a command with support for interactive or long-running work. It can return a session ID so Codex can come back to the same running process. | The unified PTY-backed command route. Stable and enabled by default except on Windows in the inspected source. |
| `write_stdin` | Send keystrokes or text to a process started by `exec_command`, or check that process for more output. | Available with `exec_command`; it is not an independent command runner. |
| `apply_patch` | Create, edit, rename, or delete files by applying a structured patch. | Available when the selected model declares an `apply_patch` tool type and the turn has an execution environment. |
| `view_image` | Open an image already stored on disk so Codex can visually inspect it. | Available when the turn has an execution environment. |

There is no separate callable tool named `shell` in this source snapshot. Official configuration documentation informally calls the default route the "shell tool," but the callable source name is `shell_command`.

## 2. Planning, questions, permissions, context, time, and environment tools

The normal-turn planner adds these utilities in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L702-L784).

| Tool | Plain-English use case | Availability |
|---|---|---|
| `update_plan` | Create or update the checklist Codex is following for a task. | Added in normal sessions. It can be hidden or wrapped by a special mode. |
| `request_user_input` | Show the user one to three short questions and wait for an answer. | Conditional on the client setting and the current collaboration mode; only the root agent may use it. |
| `request_permissions` | Ask the user to grant extra file or network access for the current turn or session. | Under-development feature; requires an execution environment. |
| `get_context_remaining` | Check how much space remains in the model's current context window. | Under-development token-budget feature. |
| `new_context` | Start a fresh model context window without resetting files, terminals, or other environment state. | Under-development token-budget feature. |
| `clock.curr_time` | Read the current UTC time. | Under-development current-time-reminder feature and a provider that supports namespace tools. The leaf tool name is `curr_time`. |
| `clock.sleep` | Pause for a requested period, while allowing new user input to wake the turn early. | Optional part of the current-time-reminder feature; `sleep_tool` must also be enabled. |
| `wait_for_environment` | Wait for a selected execution environment that is still starting to become ready. | Under-development deferred-environment feature. |

The exact `clock` names and behavior are defined in [`current_time.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/current_time.rs) and [`sleep.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/sleep.rs). `wait_for_environment` is defined in [`wait_for_environment.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/wait_for_environment.rs).

## 3. Tool discovery, plugin installation, and MCP resource tools

| Tool | Plain-English use case | Availability |
|---|---|---|
| `tool_search` | Search the catalogue of tools that were not loaded up front, then make the best matches available to Codex on its next model call. | Only when the model and provider support deferred tool search and at least one deferred tool is available. |
| `list_available_plugins_to_install` | Show the known plugins or connectors that may satisfy the user's explicit request. | Only in the plugin-suggestion flow, and only for the presentation variant that uses a separate list step. |
| `request_plugin_install` | Ask the user to install a particular matching plugin or connector. | Only when apps, plugins, and tool suggestions are enabled and a matching install candidate exists. |
| `list_mcp_resources` | List readable data items offered by configured MCP servers, such as documents, schemas, or application records. | Added when at least one MCP server is configured. |
| `list_mcp_resource_templates` | List parameterized MCP data sources—for example, a resource that takes a project or record identifier. | Added when at least one MCP server is configured. |
| `read_mcp_resource` | Read one MCP resource using the server name and resource URI returned by the listing tools. | Added when at least one MCP server is configured. |

`tool_search` is defined in [`tool_search_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/tool_search_spec.rs). The MCP resource definitions are in [`mcp_resource_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/mcp_resource_spec.rs). Plugin-install tools are defined in [`list_available_plugins_to_install_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/list_available_plugins_to_install_spec.rs) and [`request_plugin_install_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/request_plugin_install_spec.rs).

MCP resources are different from MCP tools: a resource is data Codex reads, while a tool is an action Codex asks the server to perform. The official [MCP documentation](https://learn.chatgpt.com/docs/extend/mcp) covers this distinction.

## 4. Multi-agent tools

Official documentation says current Codex releases enable subagent workflows by default, although individual tools still depend on the selected version, policy, depth, and session source. See [Subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents).

### Stable v1 tool set

The source places these tools in the `multi_agent_v1` namespace. A client may display the leaf name alone, but the qualified names are shown here.

| Tool | Plain-English use case |
|---|---|
| `multi_agent_v1.spawn_agent` | Start a subagent on a clearly defined piece of work. |
| `multi_agent_v1.send_input` | Send more information to an existing v1 subagent, optionally interrupting its current work. |
| `multi_agent_v1.resume_agent` | Reopen a previously closed v1 subagent. |
| `multi_agent_v1.wait_agent` | Wait until one or more v1 subagents finish or the wait times out. |
| `multi_agent_v1.close_agent` | Shut down a v1 subagent and its open descendants when they are no longer needed. |

### Under-development v2 tool set

The default v2 namespace is `collaboration`, but configuration can rename or remove the namespace. Only one multi-agent generation is selected for a session.

| Default qualified tool | Plain-English use case |
|---|---|
| `collaboration.spawn_agent` | Start a named subagent task, optionally carrying over some or all recent conversation context. |
| `collaboration.send_message` | Deliver a message to an existing subagent without starting a new turn for an idle agent. |
| `collaboration.followup_task` | Give an existing subagent another task and start it again if it is idle. |
| `collaboration.wait_agent` | Wait for any live subagent's mailbox or status to change. |
| `collaboration.interrupt_agent` | Stop a subagent's current turn without deleting the subagent. |
| `collaboration.list_agents` | List the live subagents in the current task tree. |

The schemas and descriptions for both generations are in [`multi_agents_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/multi_agents_spec.rs). The planner selects v1 or v2 in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L786-L878). The default v2 namespace is set to `collaboration` in [`config/mod.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/config/mod.rs#L251-L252).

### CSV fan-out tools

| Tool | Plain-English use case | Availability |
|---|---|---|
| `spawn_agents_on_csv` | Read a CSV file and run one subagent job for each row, collecting the results into an output CSV. | Under-development fan-out feature; also requires multi-agent support. |
| `report_agent_job_result` | Let a CSV worker submit its structured result to the parent fan-out job. | Worker-only; shown only inside a subagent created for a CSV job. |

These definitions are in [`agent_jobs_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/agent_jobs_spec.rs).

## 5. Tool-orchestration wrappers

These tools help Codex coordinate calls to other tools.

| Tool | Plain-English use case | Availability |
|---|---|---|
| `multi_tool_use.parallel` | Run several independent tool calls at the same time and return all their results together. | Responses-runtime wrapper. It is available only on surfaces and models that support parallel tool calls; Codex sends that capability in the model request rather than dispatching this wrapper through an ordinary core handler. |
| `exec` | Run JavaScript in an isolated code-mode runtime to call and combine other Codex tools. This is orchestration JavaScript, not Node.js and not a direct shell. | Under-development code mode. |
| `wait` | Continue waiting for a still-running `exec` program, or stop it. | Under-development code mode; useful only after `exec` yields a running cell ID. |

The base prompt explicitly names `multi_tool_use.parallel` in [`gpt_5_2_prompt.md`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/gpt_5_2_prompt.md#L252), while Codex sends the provider-level `parallel_tool_calls` setting from [`client.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/client.rs#L897). The code-mode callable names are constants in [`code-mode-protocol/src/lib.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/code-mode-protocol/src/lib.rs#L45-L46), and their behavior is described in [`description.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/code-mode-protocol/src/description.rs#L12-L44).

`CodeModeExecuteHandler` and `CodeModeWaitHandler` are implementation class names. The actual model-callable names are `exec` and `wait`.

## 6. Web search and image generation

Codex has two possible web-search routes and one specially recognized image-generation route.

| Tool | Plain-English use case | Route and availability |
|---|---|---|
| `web_search` | Search the web using the model provider's hosted search service. | Hosted Responses tool. It is omitted when search is disabled or when the standalone `web.run` route is active. |
| `web.run` | Search, open, and inspect web pages through Codex's standalone web route. | First-party extension tool. The core planner recognizes this exact namespace and leaf name and uses it instead of hosted `web_search` when the standalone route is active. |
| `image_gen.imagegen` | Generate a new image or edit an existing image from instructions. | First-party extension tool. It requires compatible OpenAI authentication, provider image-generation capability, image input support, namespace support, and the image-generation feature. |

Hosted search is built in [`hosted_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/hosted_spec.rs). The standalone executors are defined in the [`web-search`](https://github.com/openai/codex/tree/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/web-search) and [`image-generation`](https://github.com/openai/codex/tree/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/image-generation) extensions. The core planner's special handling for their exact names is in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L960-L1008). Official documentation describes cached, indexed, live, and disabled [web-search modes](https://learn.chatgpt.com/docs/config-file/config-basic).

## 7. Goal, memory, and skill extension tools

These fixed-name tools are contributed by first-party extensions in the Codex app server. They are not assembled by the core planner itself, which is why a core-only search misses them. The app server registers all three extensions in [`extensions.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/app-server/src/extensions.rs#L57-L101).

### Goal tools

| Tool | Plain-English use case | Availability |
|---|---|---|
| `get_goal` | Check the current task goal, its status, time and token usage, and any remaining token budget. | App-server goal extension; goals are stable and enabled by default, but the thread must have a state database and the goal tools must be visible. |
| `create_goal` | Start tracking a specific goal for the current task, with an optional token budget when the user explicitly requests one. | Same goal extension. It refuses to replace a goal that is still unfinished. |
| `update_goal` | Mark the current goal as completed or genuinely blocked. | Same goal extension. It does not pause, resume, or change the budget. |

The exact names and schemas are defined in [`goal/src/spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/goal/src/spec.rs).

### Memory tools

| Tool | Plain-English use case | Availability |
|---|---|---|
| `memories.add_ad_hoc_note` | Add a new, append-only memory note after the user explicitly asks Codex to remember, update, or forget something. | Only when memories and their dedicated tool interface are enabled. |
| `memories.list` | Browse the files and folders in Codex's memory store. | Same memory-extension gate. |
| `memories.read` | Open a particular memory file, optionally from a chosen line and with a line limit. | Same memory-extension gate. |
| `memories.search` | Search memory files for words or phrases, with options for matching and surrounding lines. | Same memory-extension gate. |

The namespace and fixed leaf names are declared in [`memories/src/lib.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/memories/src/lib.rs#L18-L22), and their schemas are assembled in [`memories/src/tools`](https://github.com/openai/codex/tree/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/memories/src/tools).

### Skill-catalogue tools

| Tool | Plain-English use case | Availability |
|---|---|---|
| `skills.list` | List enabled skills supplied by Codex's orchestrator, including the handles needed to open one. | Only when an orchestrator skill provider exists and orchestrator-owned skills are enabled. |
| `skills.read` | Read one complete instruction or resource file from an enabled orchestrator skill. | Same skill-extension gate; it uses the exact handles returned by `skills.list`. |

The names and schemas are defined in [`skills/src/tools`](https://github.com/openai/codex/tree/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/ext/skills/src/tools). These functions expose a skill catalogue to the model; they do not turn every installed skill into a new function tool.

## 8. Open-ended runtime tool families

The following are real Codex tool sources, but they do not have a universal finite name list.

| Family | Plain-English use case | Naming |
|---|---|---|
| MCP server tools | Let Codex read from or take actions in an external system such as GitHub, Figma, a browser, an issue tracker, a database, or an internal service. | Determined by every configured MCP server. Codex sanitizes and namespaces names; a common flattened form is `mcp__<server>__<tool>`. |
| App and connector tools | Work with an authorized service or private workspace, such as mail, calendars, files, repositories, or business systems. | Supplied by the installed and authorized app, commonly through MCP. |
| Plugin-provided tools | Use tools bundled or configured by an installed plugin. | Determined by the plugin's apps and MCP servers. A plugin itself is a package, not one tool. |
| Extension tools | Use capabilities contributed by a first-party extension or injected by the Codex client or host surface. | Fixed first-party extension names are listed in sections 6 and 7; other hosts may define arbitrary names. |
| Dynamic tools | Call functions supplied by an app-server client when a thread starts. | Arbitrary caller-defined names, either plain or namespaced. |

The planner appends dynamic and extension tools in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L880-L926). Dynamic tools are an experimental app-server API documented in [`app-server/README.md`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/app-server/README.md). MCP tool naming and dispatch are implemented in [`handlers/mcp.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/mcp.rs#L29-L69).

This open-ended design is why no honest report can enumerate every possible GitHub, Slack, browser, Figma, database, email, calendar, or private-company tool and call that a global Codex list. The complete statement is the family plus the tools actually advertised to a particular session.

## 9. Internal/test-only tool

| Tool | Plain-English use case | Availability |
|---|---|---|
| `test_sync_tool` | Coordinate timing in Codex integration tests so a test can deliberately pause or release a run. | Internal/test-only. It appears only when the selected model explicitly advertises this experimental supported tool. |

Source: [`test_sync_spec.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/handlers/test_sync_spec.rs) and its gate in [`spec_plan.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/core/src/tools/spec_plan.rs#L761-L767).

## 10. Tools exposed when Codex runs as an MCP server

These two tools point in the opposite direction: they are not tools an ordinary Codex agent receives. They are the tools that another MCP client receives after starting:

```bash
codex mcp-server
```

| MCP tool | Plain-English use case |
|---|---|
| `codex` | Start a new Codex session with a prompt and optional configuration overrides. |
| `codex-reply` | Continue an existing Codex session using its thread ID and a new prompt. |

The source creates exactly these two definitions in [`codex_tool_config.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/mcp-server/src/codex_tool_config.rs#L106-L237). The official [Codex MCP-server documentation](https://learn.chatgpt.com/docs/mcp-server) also says that `tools/list` returns these two tools.

## 11. Condensed fixed-name index

This index contains every fixed name defined by, specially recognized in, or explicitly named as a Responses-runtime wrapper by the inspected source. A dotted name shows its namespace and leaf tool name.

```text
apply_patch
clock.curr_time
clock.sleep
codex                              # exposed by `codex mcp-server`
codex-reply                        # exposed by `codex mcp-server`
collaboration.followup_task        # v2 default namespace
collaboration.interrupt_agent      # v2 default namespace
collaboration.list_agents          # v2 default namespace
collaboration.send_message         # v2 default namespace
collaboration.spawn_agent          # v2 default namespace
collaboration.wait_agent           # v2 default namespace
create_goal
exec                               # code-mode wrapper
exec_command
get_goal
get_context_remaining
image_gen.imagegen                 # first-party extension specially recognized by core
list_available_plugins_to_install
list_mcp_resource_templates
list_mcp_resources
memories.add_ad_hoc_note
memories.list
memories.read
memories.search
multi_agent_v1.close_agent
multi_agent_v1.resume_agent
multi_agent_v1.send_input
multi_agent_v1.spawn_agent
multi_agent_v1.wait_agent
multi_tool_use.parallel              # Responses-runtime parallel-call wrapper
new_context
read_mcp_resource
report_agent_job_result
request_permissions
request_plugin_install
request_user_input
shell_command
skills.list
skills.read
spawn_agents_on_csv
test_sync_tool                    # internal/test-only
tool_search
update_goal
update_plan
view_image
wait                              # code-mode wrapper
wait_for_environment
web.run                           # first-party extension specially recognized by core
web_search                        # hosted provider tool
write_stdin
```

The list has 50 qualified entries because the two multi-agent generations have separate namespaced surfaces. Only one generation is normally active, and the v2 namespace is configurable. This index deliberately does not invent fixed names for MCP, app, plugin, general extension, or dynamic tool families.

## 12. What is not an agent-callable tool

The following are often discussed alongside tools but should not be mixed into the list:

- **CLI subcommands**, such as `codex exec`, `codex review`, or `codex mcp-server`.
- **Slash commands**, such as `/plan`, `/goal`, `/mcp`, or `/plugins`.
- **Individual skills**, which are reusable instruction-and-resource workflows. The catalogue functions `skills.list` and `skills.read` are tools, but the skill packages they return are not function tools.
- **Plugins**, which are installable packages that may contain skills, apps, MCP servers, hooks, and assets.
- **Hooks**, which observe or control lifecycle events around tool use.
- **Feature flags**, such as `artifact`.
- **Implementation types**, such as `CodeModeExecuteHandler`.

For example, the source has an under-development `artifact` feature flag, but the inspected core tool planner defines no fixed agent-callable artifact tool name. It is therefore incorrect to add a generic "Artifact support" row to a list of named tools without a surface-specific tool manifest.

## 13. Reconciliation of the supplied ChatGPT conversations

### Conversation: "Codex tools list request"

The supplied answer got the central architectural point right: Codex assembles tools per session, and MCP, apps, plugins, extensions, and dynamic tools make the overall surface open-ended.

Corrections and additions required by the 19 July source snapshot:

1. The actual code-mode callable names are `exec` and `wait`; handler class names are not tool names.
2. The earlier condensed list omitted `clock.curr_time`, `clock.sleep`, and `wait_for_environment`.
3. It described only `web_search`; the current planner also specially recognizes the standalone host route `web.run`.
4. Multi-agent tools now have explicit namespaces in the source: `multi_agent_v1` for v1 and `collaboration` by default for v2.
5. "Artifact support" is a feature, not a fixed named tool in the inspected core planner.
6. `list_available_plugins_to_install` is not guaranteed to precede every installation suggestion. The current source has two presentation variants; one uses the list tool and the other uses recommendation context supplied by the host.
7. `shell` is documentation shorthand, while the callable legacy/default name is `shell_command`.
8. A core-planner-only inventory misses the app-server extensions. The complete current source also defines `get_goal`, `create_goal`, `update_goal`, four `memories.*` tools, and two `skills.*` tools.
9. The Responses-runtime wrapper `multi_tool_use.parallel` was also absent from the earlier condensed list.

### Conversation: "Codex Tools Report"

The supplied conversation excerpt contains the research request but no assistant report. There is therefore no second answer to fact-check beyond carrying forward its stated scope: the `openai/codex` repository and official OpenAI documentation.

## 14. Availability summary

Even a fixed name from this report may be absent from a real session. The source checks combinations of:

- Operating system and shell configuration.
- Whether an execution environment exists and is ready.
- Model-declared tool support.
- Provider support for hosted search, namespaces, and image generation.
- Provider and model support for parallel tool calls.
- OpenAI authentication route.
- Feature maturity and enabled flags.
- Current Codex mode, including guardian-review and code-mode restrictions.
- Multi-agent version, task depth, and worker status.
- Configured MCP servers, apps, plugins, and their allow/deny lists.
- Client-injected extension tools and app-server-supplied dynamic tools.
- Workspace or enterprise policy.

Relevant feature maturity and defaults are defined in [`features/src/lib.rs`](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/features/src/lib.rs#L803-L852) and later entries in the same [`FEATURES` table](https://github.com/openai/codex/blob/0fb559f0f6e231a88ac02ea002d3ecd248e2b515/codex-rs/features/src/lib.rs#L1020-L1270).

## Bottom line

The complete answer is not "these names are always present." It is:

- The fixed names in sections 1–7 and 9 are the complete repository-defined or specially recognized agent-tool inventory for the inspected snapshot.
- Section 8 is the complete set of open-ended sources that can add any number of session-specific tools.
- Section 10 contains the two separate tools Codex exposes to other agents when run as an MCP server.
- The actual tool list for one Codex session is the subset produced after all of that session's gates and runtime contributions are applied.

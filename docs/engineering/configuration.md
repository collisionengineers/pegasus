# Configuration reference

This file describes setting meanings. Operations owns observed deployed values; the runbook and existing release/wipe skills own operational procedures.

## Configuration and secrets

Configuration ownership is:

| Boundary | Owner |
| --- | --- |
| Web composition and named SQL Server connection | `src/Pegasus.Web/Program.cs` and environment configuration |
| Development profile and launch path | `src/Pegasus.Web/Properties/launchSettings.json` |
| Ignored local state | `artifacts/` |
| Target Azure parameters and topology | `infra/` and `azure.yaml` |
| Which mailboxes inbound Intake polls, and their exact tenant identities | The `ApprovedMailboxes` allowlist, edited on `/Administration/Mailboxes` ([ADR-0024](../adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md)). v1 poll and Sent claims use each persisted mailbox identity, folder, capability and generation; a global Graph coordinate is not the current mailbox authority. Historical deployment settings are recorded in operations. |

Tool availability does not authorize external action.

## Document intelligence configuration

The optional Production Worker OCR adapter reads `DocumentIntelligence:Endpoint`
as an absolute HTTPS URI and reuses the credential selected by
`AzureIdentity:WorkerClientId`. It adds no API-key setting. An absent endpoint
leaves the provider and processor unregistered; an OCR work row then fails
closed through the existing external-work dispatcher. `DevelopmentOffline`
rejects the endpoint setting and composes no OCR provider, unless
`Features:LiveOcr` is on (11 November 2026, R931; see
[local opt-in features](#local-opt-in-features)): then the endpoint is
required, validated as in Production, and the same adapter and processor are
composed over the hosted test instance's Graph app registration. The durable
OCR store remains available to both hosts. Configuration, resource permission,
deployment and live provider proof require their separately approved targets;
local composition tests do not establish any of them.

The production Bicep module declares one `FormRecognizer` S0 account with a
custom subdomain, disabled local authentication and a resource-scoped
`Cognitive Services User` assignment for the existing Worker identity only
([ADR-0040](../adr/0040-qualified-document-intelligence-ocr.md)). It supplies the
Worker endpoint; Web receives neither the setting nor that role. The
`DOCUMENT_INTELLIGENCE_ACCOUNT_ID` and `DOCUMENT_INTELLIGENCE_ENDPOINT`
deployment outputs identify the exact account for release readback. Current
target, pricing and activation evidence belong in
[operations](../operations.md).

Use the existing authorized release and preview procedure. Read back the
account's SKU, custom subdomain, `disableLocalAuth`, scoped assignment and
Worker endpoint before the approved qualified-page canary. Retain the OCR
operation/API/model identity and result hash; replay the same operation to
prove retained-output recovery, not a new provider submission. Web denial
needs an actual identity-scoped check, not just an absent setting. If OCR
activation must be rolled back, remove the Worker endpoint through the
approved configuration route while preserving the account and retained
operation evidence. Keep local authentication disabled; never use keys as a
fallback. Infrastructure readback alone does not prove accepted extraction.

Use managed identity and scoped RBAC. General application secrets retain their approved vault owner. The scoped
[ADR-0043](../adr/0043-per-engineer-vendor-credential-protection.md) exception
protects per-staff-account vendor credentials and session material with Data
Protection in existing SQL, with the matching key-ring recovery contract. Never commit secret values, connection strings, readable passwords, generated credentials, or data not approved for public source control.

## Problem reports

`GitHub:ProblemReports:Token` and `GitHub:ProblemReports:Repository` connect
Report a problem ([ADR-0055](../adr/0055-github-issues-as-the-problem-report-sink.md))
to one repository's issues; `GitHub:ProblemReports:Labels` is an optional
comma-separated label list applied to every issue. The two required
settings are configured together or not at all: with neither, every report
is kept as Not sent with the reason and an Administrator retries once the
settings exist; with only one, the host refuses to start. The token is a
fine-grained personal access token scoped to that repository with Issues
read and write only, held in Key Vault and delivered to Web as
`GitHub__ProblemReports__Token` through a versioned secret URI reference;
the Worker receives neither setting. Rotating the token is a new secret
version and a configuration read-back, not a release. The selected repository
  is `collisionengineers/pegasus`. Each issue carries the reporter's complete
  submitted text and the captured diagnostic snapshot, including a server
  exception and stack when available. The report is also retained in Pegasus.

## Photo capture

Guided photo capture ([FRD-28](../frd/frd-28-guided-photo-capture.md),
[ADR-0090](../adr/0090-public-photo-capture-surface.md)) has four settings.

| Setting | Effect |
| --- | --- |
| `Features:PhotoCapture` | The composition gate. Off, nothing of photo capture is registered: `/c`, `/s`, `/capture` and `/PhotoCapture` answer 404 before routing, and the rail item, the Case ribbon's Send photo link, the contact's Photo links tab and Administration › Photo links are absent (R911). Bicep sets it on Web and on the Worker, which settles captures. |
| `PhotoCapture:PublicOrigin` | The absolute `https` origin printed in photo links and standing links. Required when the gate is on. Production's is `https://pegasus-prod-web-252ow37gij.azurewebsites.net/` (R779); the hosted test instance's is its `:8443` Funnel origin (R785). Bicep sets it on Web. |
| `TextMessages:Endpoint` | The Azure Communication Services endpoint a photo link text goes through ([FRD-28](../frd/frd-28-guided-photo-capture.md#the-photo-link-text); R918). Bicep sets it on Web from the resource it creates; the Web's managed identity holds Contributor on that resource. |
| `TextMessages:From` | The SMS-enabled UK mobile number, in E.164, texts are sent from. It is bought on the resource in the portal and set through `PEGASUS_PHOTO_LINK_TEXT_FROM`; while it is empty, or outside Production, no page offers Send by text. |

None is a secret, so there is no Key Vault change. The capture limits are
constants in Core, not configuration. Send a photo link's starting values and
the lifetimes it offers are Administration › Photo links settings an
Administrator changes, not deployment settings
([FRD-17](../frd/frd-17-administration-workspace.md#photo-links)).

## Local opt-in features

The `DevelopmentOffline` profile accepts seven opt-ins that a hosted local test
instance uses to reach the vendors it is allowed to. Each is read only in that
profile; a Production host carrying one refuses to start naming the key, as it
does for `Features:LocalIntake`.

| Setting | Effect |
| --- | --- |
| `Features:LiveVehicleLookup` | The Worker composes the live DVLA/DVSA adapter from `Dvla:*` and `Dvsa:*` (parsed at start) instead of the replay folder; the Web records requests as `production_live`. |
| `Features:LiveBoxCustody` | Both hosts compose the production storage shape (blob-backed intake artifacts, cached document reads, Box case custody) over the run's Azurite `transient-intake` container, with the fenced local-test Box root `425169015650`. It cannot be combined with `Features:LocalDocumentCustody`. |
| `Features:LiveGlass` | The Web composes the Glass's gateway from `Glass:*` at start. Without it, or in any host without Glass's, the Case page offers no Glass's control and refuses the command. |
| `Features:LiveGuideValuation` | The Web composes the Glass's, Cazana and Brego guide valuations, as Production does, with the logins the run's own database holds (R709, R1288; [integration logins](#integration-logins)); it requires `Features:LiveGlass`, whose options Glass's valuation reads. A login not set leaves its card unavailable. Without it an offline host offers no Get valuation. |
| `Features:LiveExperian` | The Web composes the Experian vehicle history check and CAP's guide valuation, as Production does, from `Experian:BaseUri` and `Experian:TokenUri`, each act with its own login from the run's database (read on each call; ADR-0089, R1201, R1215, R1288), so the hosted test instance can run them against Experian. Without it an offline host says Experian is not connected and its CAP card is unavailable. |
| `Features:LiveGraphMail` | Both hosts compose Production's Graph mail side (the mail client, the Inbox and Sent sources and their pollers, staff and report send and the answered-instruction move in the Worker; the mailbox resolver, access check, search and staff send in the Web) over a `ClientSecretCredential` built from `Graph:TenantId`, `Graph:ClientId` and `Graph:ClientSecret`, parsed at start. The Worker maintains change subscriptions only when `Graph:ChangeNotificationUrl` (the Funnelled webhook path on the instance's public host) and `Graph:ChangeNotificationClientState` are both set, checked at start: one without the other fails the start naming the missing key, and neither leaves the five-minute recovery poll to carry mail (ADR-0088 as amended, R675 to R677). |
| `Features:LiveOcr` | The Worker composes Production's Document Intelligence OCR adapter and processor against `DocumentIntelligence:Endpoint`, production's account, which is otherwise a Production-only setting the profile refuses; the endpoint is required and validated as in Production. The adapter authenticates with a `ClientSecretCredential` built from `Graph:TenantId`, `Graph:ClientId` and `Graph:ClientSecret`, the registration `Features:LiveGraphMail` uses, which needs `Cognitive Services User` on that account; it is handed to the adapter alone. A missing endpoint or key fails the start naming it. No OCR opt-in existed before this one: without it an offline host composes no OCR provider and OCR work fails closed (ADR-0088 as amended, R931). |
| `Features:PasswordSignIn` | The automatic offline sign-in stops answering; the Identity cookie and `/Account/SignIn` are used, and `--initialize-development` sets `DevelopmentOffline:AdministratorPassword` on the local Administrator each time it runs. The lifecycle requires it for a run with a public origin. |

`Features:AutomationMcp` with `AutomationMcp:UseDevelopmentKeys` and
`Features:PrincipalApi` already compose offline and need no further switch.
The lifecycle passes these settings to the hosts from the ignored local
settings file described in the [runbook](../runbook.md#local-live-integration-run);
no tracked file carries them.

## Integration logins

The logins of the Glass's valuation, Cazana Companion, Brego and two Experian
accounts are not settings: they are held in the database, protected with the
Web host's key ring, and an Administrator replaces each on Administration >
Integrations (R1288,
[ADR-0091](../adr/0091-integration-logins-in-the-database.md)). Each provider
reads its login on each call, never at startup, so a login not set or
unreadable makes that card or check unavailable rather than stopping the host.
The Worker reads none.

The settings that held them until R1288 (`Glass:ValuationAccount:*`,
`Cazana:CompanionAccount:*`, `Brego:ValuationAccount:*`,
`Experian:HistoryAccount:*` and `Experian:CapAccount:*`, Key Vault references
in Production) are no longer required at start-up. The release that moves the
logins reads them once, with Brego's stored sign-in from
`authentication-ring/brego-session.json`, to fill each login not yet set, and
never over one that is; the next release removes the settings and their
secrets (#2177, #2163).

## Glass's valuation

The Glass's valuation account
([ADR-0060](../adr/0060-glass-valuation-account-and-valuation-report.md)) is
an [integration login](#integration-logins): a username and a password. The
valuation uses the repair estimate's `Glass:MarketValueAssessorBaseUri` and
`Glass:RequestTimeoutSeconds`. `DevelopmentOffline` composes no Glass's
valuation unless `Features:LiveGuideValuation` is set, so its Glass's card
says the source is unavailable.

## Cazana valuation

The Cazana Companion account
([ADR-0079](../adr/0079-cazana-companion-account-and-companion-report.md)) is
an [integration login](#integration-logins): an e-mail address and a
password. The Cazana API origin (`https://api.cazana.com`) and a 30-second
timeout are fixed in the adapter. `DevelopmentOffline` composes no Cazana
valuation unless `Features:LiveGuideValuation` is set, so its Cazana card says
the source is unavailable.

## Brego valuation

The Brego valuation account
([ADR-0084](../adr/0084-brego-valuation-account-and-valuation-report.md)) is
an [integration login](#integration-logins): its e-mail address and, when
it has one, its password (optional, R1290), with the stored sign-in (the
Cognito refresh token and the `brego-session` cookie) kept and renewed beside
it. The account is linked on Administration > Integrations, by its e-mailed
sign-in link or its saved password and the six-digit code Brego e-mails when
it asks to approve the device; no setting, script or storage role is involved
([runbook](../runbook.md#linking-the-brego-valuation-account)). Where the Web
composes its Graph mail reads, Refresh login and a Get valuation that finds
the sign-in gone read Brego's sign-in e-mail from the account's own mailbox
(R1294, R1295), which needs the host's Graph identity to have Exchange access
to that mailbox; it adds no setting.
`DevelopmentOffline` composes no Brego valuation unless
`Features:LiveGuideValuation` is set, so its Brego card says the source is
unavailable.

## Experian accounts

Experian issues an account per permission, so Pegasus holds two
([ADR-0089](../adr/0089-experian-vehicle-history-check.md), R1207, R1215).
Each act signs in with its own account only; neither reads the other's. Each
account is an [integration login](#integration-logins): a client id, a client
secret, a username and a password.

| Purpose | Settings until R1288 | Key Vault secrets until R1288 | Production user | UAT user (test instance) |
|---|---|---|---|---|
| Run Experian check (condition records) | `Experian:HistoryAccount:ClientId`, `ClientSecret`, `Username`, `Password` | `experian-history-client-id`, `-client-secret`, `-username`, `-password` | `pattersonaapi` | `pattersonaapiuat` |
| CAP's Get valuation (CAP valuations, R1201) | `Experian:CapAccount:ClientId`, `ClientSecret`, `Username`, `Password` | `experian-cap-client-id`, `-client-secret`, `-username`, `-password` | `merceraapi` | `merceraapiuat` |

`Experian:BaseUri` and `Experian:TokenUri`, Experian's API root and OAuth2
token endpoint, are shared by both accounts: production uses
`https://uk-api.experian.com/ukci/automotive` and
`https://uk-api.experian.com/oauth2/v1/token`, UAT the same paths on
`uat-uk-api.experian.com`. Both must be HTTPS on an `experian.com` host. Both
are Production required settings (R749): the Web host checks at start-up that
each is present, and stops naming the key when one is not (R1252). They are
plain settings (`Experian__BaseUri`, `Experian__TokenUri`); the Worker
receives neither. The accounts' eight settings stopped being required on 26
November 2026 (R1288). `DevelopmentOffline` composes no
Experian unless `Features:LiveExperian` is set, so its Vehicle history area
says Experian is not connected and its CAP card says the source is
unavailable; the hosted test instance sets it, with both UAT accounts as its
own integration logins.

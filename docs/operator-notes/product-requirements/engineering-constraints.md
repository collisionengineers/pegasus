# Engineering and interface constraints

## Environment and tools

All work is carried out in a Windows environment using PowerShell. The approved
tool set includes:

- GitHub
- PowerShell and necessary modules
- Azure CLI and Azure Developer CLI
- approved Azure skills and tools
- Box CLI where applicable

Tool availability does not itself authorize an external or cloud operation.

## Interface language

Never include any “dev copy” or similar internal/weird wording. Functionality should be obvious from buttons and labels, without random explanatory sentences throughout the app. The app should not narrate its own functions with sentences. Never mention any internal Azure functions or wording.

## Development data boundary

All e-mails, PDFs, documents, images, and data are permissible for use in development. PII, DPIA, retention, or other concerns around data are not in scope for development.

Do not create synthetic e-mails, images, or instructions for test data. Use only
the examples provided in the repository.

## Naming and authority

All functions, code files, and azure services and resources must be labelled with logical names that would identify their purpose at a glance.

Treat everything in `docs/operator-notes/` as authoritative operator truth.

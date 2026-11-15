# The image every repository-check job runs in (.gitlab-ci.yml names its tag).
# It holds what the lanes used on the hosted Ubuntu 24.04 runner: the .NET 10
# SDK, PowerShell 7, Python 3.13, Node 22, git, zip and the Azure CLI with
# Bicep, which Test-AzureDeploymentPlan.ps1 compiles the templates with.
#
# Built and pushed by hand when this file changes, then the tag in
# .gitlab-ci.yml moves (docs/runbook.md, "CI runner and image"):
#   docker build -t registry.gitlab.com/collisionengineers-group/pegasus/ci:<n> .gitlab/ci
#   docker push registry.gitlab.com/collisionengineers-group/pegasus/ci:<n>
FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    POWERSHELL_TELEMETRY_OPTOUT=1 \
    AZURE_CORE_COLLECT_TELEMETRY=0

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl git gnupg procps software-properties-common unzip zip \
 && curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb \
 && dpkg -i /tmp/packages-microsoft-prod.deb \
 && rm /tmp/packages-microsoft-prod.deb \
 && add-apt-repository -y ppa:deadsnakes/ppa \
 && apt-get update \
 && apt-get install -y --no-install-recommends powershell python3.13 python3.13-venv \
 && curl -fsSL https://aka.ms/InstallAzureCLIDeb | bash \
 && az bicep install \
 && rm -rf /var/lib/apt/lists/*

# `python` and `python -m pip` are Python 3.13, as actions/setup-python gave.
RUN python3.13 -m venv /opt/python
ENV PATH=/opt/python/bin:$PATH

COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
 && ln -s ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

RUN dotnet --version && pwsh -Version && python --version && node --version \
 && npm --version && az bicep version && git --version

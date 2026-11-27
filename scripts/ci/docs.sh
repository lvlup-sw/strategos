#!/usr/bin/env bash
# Build the docs site with Node 22, independent of the Contracts toolchain's
# Node 24. The CI image keeps Node 22 in /opt/node22 beside the default Node 24.
source "$(dirname "$0")/lib.sh"

export PATH="/opt/node22/bin:$PATH"
cd docs
/opt/node22/bin/node /usr/local/lib/node_modules/npm/bin/npm-cli.js ci
/opt/node22/bin/node /usr/local/lib/node_modules/npm/bin/npm-cli.js run build
